using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Sprache;
using Newtonsoft.Json;

using Utils;
using ParseProcs.Datasets;

namespace ParseProcs
{
	public class ModuleContext
	{
		public string ModuleName { get; }

		protected Dictionary<string, DbTable> _TablesDict;
		public IReadOnlyDictionary<string, DbTable> TablesDict => _TablesDict;

		protected Dictionary<string, NamedTyped> _VariablesDict;
		public IReadOnlyDictionary<string, NamedTyped> VariablesDict => _VariablesDict;

		protected Dictionary<string, PSqlType> _FunctionsDict;
		public IReadOnlyDictionary<string, PSqlType> FunctionsDict => _FunctionsDict;

		protected List<string> _SchemaOrder;
		public IReadOnlyList<string> SchemaOrder => _SchemaOrder;

		public ModuleContext (
			string ModuleName,
			IEnumerable<string> SchemaOrder,
			IReadOnlyDictionary<string, DbTable> TablesDict,
			IReadOnlyDictionary<string, PSqlType> FunctionsDict,
			IReadOnlyDictionary<string, NamedTyped> VariablesDict
			)
		{
			this.ModuleName = ModuleName.ToLower ();
			_SchemaOrder = new List<string> (SchemaOrder);
			_TablesDict = new Dictionary<string, DbTable> (TablesDict);
			_FunctionsDict = new Dictionary<string, PSqlType> (FunctionsDict);
			_VariablesDict = new Dictionary<string, NamedTyped> (VariablesDict);
		}

		protected T GetSchemaEntity<T> (IReadOnlyDictionary<string, T> Dict, string[] NameSegments)
		{
			string Key = NameSegments.PSqlQualifiedName ();

			T Result;
			if (!Dict.TryGetValue (Key, out Result))
			{
				if (NameSegments.Length == 1)
				{
					foreach (string sch in SchemaOrder)
					{
						string SchKey = sch.ToTrivialArray ().Concat (NameSegments).PSqlQualifiedName ();
						if (Dict.TryGetValue (SchKey, out Result))
						{
							break;
						}
					}
				}
			}

			return Result;
		}

		public NamedTyped GetFunction (string[] NameSegments)
		{
			string Name = NameSegments[^1].ToLower ();
			PSqlType Type = GetSchemaEntity (FunctionsDict, NameSegments) ?? PSqlType.Null;

			return new NamedTyped (Name, Type);
		}

		public DbTable GetTable (string[] NameSegments)
		{
			return GetSchemaEntity (TablesDict, NameSegments);
		}
	}

	public class RequestContext
	{
		public ModuleContext ModuleContext { get; }
		public IReadOnlyDictionary<string, ITable>[] TableRefChain { get; }
		public IReadOnlyDictionary<string, NamedTyped> NamedDict { get; }
		public IReadOnlyDictionary<string, IReadOnlyList<NamedTyped>> Asterisks { get; }

		public RequestContext (ModuleContext ModuleContext)
		{
			this.ModuleContext = ModuleContext;

			// table, as accessible in ModuleContext, considering schema order
			this.TableRefChain = (ModuleContext
					.TablesDict.Select (t => new { name = t.Key, table = t.Value })
					.Concat (ModuleContext.TablesDict.Values.Where (t =>
							!ModuleContext.SchemaOrder.TakeWhile (s => s != t.Schema).Any (s => ModuleContext.TablesDict.ContainsKey (s + "." + t.Name)))
						.Select (t => new { name = t.Name, table = t })
					)
					.ToDictionary (t => t.name, t => (ITable)t.table))
					.ToTrivialArray ()
				;

			this.NamedDict = ModuleContext.VariablesDict;
			// asterisks only appear in in-select contexts
			this.Asterisks = new Dictionary<string, IReadOnlyList<NamedTyped>> ();
		}

		public RequestContext (RequestContext ParentContext,
			IReadOnlyDictionary<string, ITable> TableRefsToPrepend = null,
			IReadOnlyDictionary<string, NamedTyped> NamedDictToOverride = null,
			IReadOnlyDictionary<string, IReadOnlyList<NamedTyped>> Asterisks = null
		)
		{
			this.ModuleContext = ParentContext.ModuleContext;

			this.TableRefChain = TableRefsToPrepend == null
				? ParentContext.TableRefChain
				: TableRefsToPrepend.ToTrivialArray ()
					.Concat (ParentContext.TableRefChain)
					.ToArray ()
				;

			this.NamedDict = NamedDictToOverride ?? ModuleContext.VariablesDict;
			this.Asterisks = Asterisks ?? ParentContext.Asterisks;
		}

		public IReadOnlyList<NamedTyped> GetAsterisk (string AsteriskEntry)
		{
			return Asterisks[AsteriskEntry];
		}
	}

	public class OpenDataset
	{
		public string Name { get; }
		public string[] Comments { get; }

		public OpenDataset (string Name, string[] Comments)
		{
			this.Name = Name;
			this.Comments = Comments;
		}
	}

	public class FromTableExpression
	{
		public ITableRetriever TableRetriever { get; }
		public string Alias { get; }

		public FromTableExpression (ITableRetriever TableRetriever, string Alias)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
		}
	}

	public class OrdinarySelect
	{
		public Func<RequestContext, IReadOnlyList<NamedTyped>> List { get; }
		public IOption<FromTableExpression[]> FromClause { get; }

		public OrdinarySelect (Func<RequestContext, IReadOnlyList<NamedTyped>> List, IOption<FromTableExpression[]> FromClause)
		{
			this.List = List;
			this.FromClause = FromClause;
		}
	}

	public class SelectStatement : ITableRetriever
	{
		public Func<RequestContext, IReadOnlyList<NamedTyped>> List { get; }
		// can be null
		public FromTableExpression[] Froms { get; }
		public string Name { get; }

		public SelectStatement (Func<RequestContext, IReadOnlyList<NamedTyped>> List, FromTableExpression[] Froms,
			string Name = null)
		{
			this.List = List;
			this.Froms = Froms;
			this.Name = Name;
		}

		public SelectStatement (SelectStatement Core, string Name)
			: this (Core.List, Core.Froms, Name)
		{
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			List<NamedTyped> AllColumns = new List<NamedTyped> ();
			Dictionary<string, IReadOnlyList<NamedTyped>> Asterisks =
				new Dictionary<string, IReadOnlyList<NamedTyped>> ();
			List<NamedTyped> AllAsteriskedEntries = new List<NamedTyped> ();

			if (Froms != null && Froms.Length > 0)
			{
				foreach (var f in Froms)
				{
					ITable Table = f.TableRetriever.GetTable (Context);
					var Refs = Table.GetAllColumnReferences (Context.ModuleContext, f.Alias);
					AllColumns.AddRange (Refs.Columns);

					foreach (var ast in Refs.Asterisks)
					{
						if (ast.Key == "*")
						{
							AllAsteriskedEntries.AddRange (ast.Value);
						}

						if (ast.Key != "*")
						{
							Asterisks[ast.Key] = ast.Value;
						}
					}
				}
			}

			// found immediate columns
			// + variables
			var AllNamedDict = AllColumns
				.Concat (Context.ModuleContext.VariablesDict.Values)
				.ToLookup (c => c.Name)
				.Where (g => g.Count () == 1)
				.ToDictionary (g => g.Key, g => g.First ())
				;

			Asterisks["*"] = AllAsteriskedEntries
					.ToLookup (c => c.Name)
					.Where (g => g.Count () == 1)
					.Select (g => g.First ())
					.ToArray ()
				;

			RequestContext NewContext = new RequestContext (Context, null, AllNamedDict, Asterisks);

			SortedSet<string> FoundNames = new SortedSet<string> ();
			Table Result = new Table (Name);
			foreach (var nt in List (NewContext))
			{
				if (nt.Name != null && !FoundNames.Contains (nt.Name))
				{
					FoundNames.Add (nt.Name);
					Result.AddColumn (nt);
				}
				else if (!OnlyNamed)
				{
					Result.AddColumn (nt);
				}
			}

			return Result;
		}
	}

	public class FullSelectStatement : ITableRetriever
	{
		// can be null
		public IOption<IEnumerable<SelectStatement>> Cte { get; }
		public SelectStatement SelectBody { get; }

		public FullSelectStatement (IOption<IEnumerable<SelectStatement>> Cte, SelectStatement SelectBody)
		{
			this.Cte = Cte;
			this.SelectBody = SelectBody;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			RequestContext CurrentContext = Context;

			if (Cte != null && Cte.IsDefined)
			{
				var Levels = Cte.Get ();
				if (Levels != null)
				{
					foreach (var l in Levels)
					{
						ITable t = l.GetTable (CurrentContext, true);
						CurrentContext = new RequestContext (CurrentContext, new Dictionary<string, ITable> { [l.Name] = t });
					}
				}
			}

			var Result = SelectBody.GetTable (CurrentContext, OnlyNamed);
			return Result;
		}
	}

	public class ValuesBlock : ITableRetriever
	{
		public SPolynom[] Values;
		public string TableName;
		public string[] ColumnNames;

		public ValuesBlock (SPolynom[] Values, string TableName, string[] ColumnNames)
		{
			this.Values = Values;
			this.TableName = TableName;
			this.ColumnNames = ColumnNames;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			Table t = new Table (TableName);

			foreach (var ValueExp in Values.Indexed ())
			{
				int Pos = ValueExp.Index;
				string ColName = Pos < ColumnNames.Length ? ColumnNames[Pos] : null;

				if (ColName != null || !OnlyNamed)
				{
					t.AddColumn (ValueExp.Value.GetResult (Context).WithName (ColName));
				}
			}

			return t;
		}
	}

	public class NamedDataReturn
	{
		public string Name;
		public ITable Table;
		public string[] Comments;
	}

	public class DataReturnStatement
	{
		public static readonly DataReturnStatement Void = null;

		public OpenDataset Open { get; }
		public FullSelectStatement FullSelect { get; }

		public DataReturnStatement (OpenDataset Open, FullSelectStatement FullSelect)
		{
			this.Open = Open;
			this.FullSelect = FullSelect;
		}

		public NamedDataReturn GetResult (RequestContext rc)
		{
			var Result = new NamedDataReturn
			{
				Name = Open.Name,
				Comments = Open.Comments,
				Table = FullSelect.GetTable (rc)
			};

			return Result;
		}
	}

	public class CaseBase<T>
	{
		public string CaseH { get; }
		public IEnumerable<T> Branches { get; }
		public IOption<SPolynom> ElseC { get; }

		public CaseBase (string CaseH, IEnumerable<T> Branches, IOption<SPolynom> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}

	partial class Program
	{
		static Parser<CaseBase<T>> GetCase<T> (Parser<SPolynom> PExpressionST, Parser<T> Then)
		{
			return
				from case_h in SpracheUtils.SqlToken ("case")
				from _2 in PExpressionST.Optional ()
				from branches in
				(
					from _1 in SpracheUtils.SqlToken ("when")
					from _2 in PExpressionST.CommaDelimitedST ().AtLeastOnce ()
					from _3 in SpracheUtils.SqlToken ("then")
					from value in Then
					select value
				).AtLeastOnce ()
				from else_c in
				(
					from _1 in SpracheUtils.SqlToken ("else")
					from value in PExpressionST
					select value
				).Optional ()
				from _3 in SpracheUtils.AnyTokenST ("end case", "end")
				select new CaseBase<T> (case_h, branches, else_c)
				;
		}

		static void Main (string[] args)
		{
			string ConnectionString = args[0];
			string OutputFileName = args[1];

			// postfix ST means that the result is 'SQL token',
			// i.e. duly processes comments and whitespaces

			// https://www.postgresql.org/docs/12/sql-syntax-lexical.html
			var PDoubleQuotedString =
					from _1 in Parse.Char ('"')
					from s in Parse.CharExcept ("\"\\").Or (Parse.Char ('\\').Then (c => Parse.AnyChar)).Many ()
					from _2 in Parse.Char ('"')
					select new string (s.ToArray ())
				;

			var PSingleQuotedString =
					from _1 in Parse.Char ('\'')
					from s in Parse.CharExcept ("'\\").Or (Parse.Char ('\\').Then (c => Parse.AnyChar)).Many ()
					from _2 in Parse.Char ('\'')
					select new string (s.ToArray ())
				;

			var PNull = Parse.IgnoreCase ("null").Text ();
			var PInteger = Parse.Number;
			var PDecimal =
					from i in Parse.Number.Optional ()
					from frac in
					(
						from p in Parse.Char ('.').AtLeastOnce ()
						where p.Count () == 1
						from f in Parse.Number.Optional ()
						select p + f.GetOrElse ("")
					).Optional ()
					from exp in
					(
						from e in Parse.Chars ('e', 'E')
						from s in Parse.Chars ('+', '-').Optional ()
						from d in Parse.Number
						select $"{e}{s}{d}"
					).Optional ()
					where frac.IsDefined || exp.IsDefined
					select i.GetOrElse ("") + frac.GetOrElse ("")
					                        + exp.GetOrElse ("")
				;

			var PBooleanLiteral = Parse.IgnoreCase ("true")
					.Or (Parse.IgnoreCase ("false"))
					.Text ()
				;

			// any id readable without quotes
			// lowercase
			var PAlphaNumericL =
					from n in Parse.Char (c => char.IsLetterOrDigit (c) || c == '_', "").AtLeastOnce ()
					where !char.IsDigit (n.First ())
					select new string (n.ToArray ()).ToLower ()
				;

			// valid for column name
			var PColumnNameLST = PAlphaNumericL
					.Where (n => n.NotROrT ())
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;

			// valid for schema name
			//var PSchemaNameLST = PColumnNameLST;

			var PDirectColumnAliasLST = PAlphaNumericL
					.Where (n => !n.IsKeyword ())
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;

			var PAsColumnAliasLST = PAlphaNumericL
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;

			// direct or 'as'
			var PTableAliasLST = PColumnNameLST;

			// lowercase
			var PQualifiedIdentifierLST =
					from k1 in PColumnNameLST
					from kn in
					(
						from d in SpracheUtils.SqlToken (".")
						from n in PAlphaNumericL.Or (PDoubleQuotedString.ToLower ()).SqlToken ()
						select n
					).Many ()
					select k1.ToTrivialArray ().Concat (kn).ToArray ()
				;

			var PSignPrefix =
					from c in Parse.Chars ('+', '-')
					from sp in Parse.Chars ('+', '-').Or (Parse.WhiteSpace).Many ().Text ()
					let res = c + sp
					where !res.Contains ("--")
					select res
				;

			Ref<SPolynom> PExpressionRefST = new Ref<SPolynom> ();
			Ref<FullSelectStatement> PFullSelectStatementRefST = new Ref<FullSelectStatement> ();

			var PParentsST = PExpressionRefST.Get.InParentsST ();
			var PBracketsST = PExpressionRefST.Get.Contained (Parse.Char ('[').SqlToken (), Parse.Char (']').SqlToken ());

			var PBinaryAdditionOperatorsST = SpracheUtils.AnyTokenST ("+", "-");
			var PBinaryMultiplicationOperatorsST = SpracheUtils.AnyTokenST ("/", "*", "%");
			var PBinaryExponentialOperatorsST = SpracheUtils.AnyTokenST ("^");

			var PBinaryComparisonOperatorsST = SpracheUtils.AnyTokenST (
				">=", ">", "<=", "<>", "<", "=", "!="
				);

			var PBinaryJsonOperatorsST = SpracheUtils.AnyTokenST (
				"->>", "->", "#>>", "#>"
			);

			var PBinaryRangeOperatorsST = SpracheUtils.AnyTokenST (
				"like"
			);

			var PBetweenOperatorST = SpracheUtils.SqlToken ("between");

			var PBinaryGeneralTextOperatorsST = SpracheUtils.AnyTokenST (
				"||"
			);

			var PBinaryIncludeOperatorsST = SpracheUtils.AnyTokenST (
				"in", "not in"
			);
			var PBinaryMatchingOperatorsST = SpracheUtils.AnyTokenST (
				"is"
			);
			var PNullMatchingOperatorsST = SpracheUtils.AnyTokenST ("isnull", "notnull");

			var PNegationST = SpracheUtils.AnyTokenST ("not");
			var PBinaryConjunctionST = SpracheUtils.AnyTokenST ("and");
			var PBinaryDisjunctionST = SpracheUtils.AnyTokenST ("or");

			var PBaseTypeST =
					from t in SpracheUtils.AnyTokenST (PSqlType.GetAllKeys ())
					from p in Parse.Number.SqlToken ()
						.CommaDelimitedST ()
						.InParentsST ()
						.Optional ()
					select t
				;

			var PTypeST =
					(
						from _tn in PQualifiedIdentifierLST
						from _ps in SpracheUtils.SqlToken ("%")
						from _rt in SpracheUtils.SqlToken ("rowtype")
						select (string)null
					)
					.Or (
						from t in PBaseTypeST
						from array in
							(
								from _1 in Parse.String ("[").SqlToken ()
								from _2 in Parse.String ("]").SqlToken ()
								select 1
							)
							.AtLeastOnce ()
							.Optional ()
						select t + (array.IsDefined ? "[]" : "")
					)
				;

			var PSimpleTypeCastST =
					from op in Parse.String ("::").SqlToken ()
					from t in PTypeST
					select t
				;

			var PSelectFirstColumnST = PFullSelectStatementRefST.Get.InParentsST ()
				.Select<FullSelectStatement, Func<RequestContext, NamedTyped>> (fss => rc =>
					fss.GetTable (rc, false).Columns[0]
				);

			var PArrayST =
					from array_kw in SpracheUtils.SqlToken ("array")
					from body in PExpressionRefST.Get.CommaDelimitedST ().InBracketsST ()
						.Select<IEnumerable<SPolynom>, Func<RequestContext, NamedTyped>> (arr =>
							rc =>
								arr.Select (it => it.GetResult (rc))
									.FirstOrDefault (nt => nt.Type != PSqlType.Null) ??
								new NamedTyped (PSqlType.Null))
						.Or (PSelectFirstColumnST)
					select (Func<RequestContext, NamedTyped>)(rc =>
						body (rc).ToArray ().WithName ("array"))
				;

			var PFunctionCallST =
					from n in PQualifiedIdentifierLST
					from arg in PExpressionRefST.Get
						.CommaDelimitedST (true)
						.InParentsST ()
						.SqlToken ()
					select n
				;

			//

			var PAsteriskSelectEntryST =
					from qual in
					(
						from qual in PQualifiedIdentifierLST
						from dot in Parse.Char ('.').SqlToken ()
						select qual
					).Optional ()
					from ast in Parse.Char ('*').SqlToken ()
					select (Func<RequestContext, IReadOnlyList<NamedTyped>>)(rc => rc.GetAsterisk (
						qual.IsDefined
							? qual.Get ().JoinDot () + ".*"
							: "*"
						))
				;

			var PGroupByClauseOptionalST =
				(
					from kw_groupby in SpracheUtils.AnyTokenST ("group by")
					from grp in PExpressionRefST.Get.CommaDelimitedST ()
					select 0
				).Optional ()
				;

			var POrderByClauseOptionalST =
				(
					from f in SpracheUtils.AnyTokenST ("order by")
					from grp in
						(
							from _1 in PExpressionRefST.Get
							from _2 in SpracheUtils.AnyTokenST ("asc", "desc").Optional ()
							select 0
						)
						.CommaDelimitedST ()
					select 0
				).Optional ()
				;

			var PUnnestST =
					from f in SpracheUtils.AnyTokenST ("unnest")
					from _1 in SpracheUtils.AnyTokenST ("(")
					from exp in PExpressionRefST.Get
					from _3 in SpracheUtils.AnyTokenST (")")
					select (Func<RequestContext, NamedTyped>)(rc =>
						new NamedTyped (f, exp.GetResult (rc).Type.BaseType))
				;

			var PBaseAtomicST =
					PNull.SqlToken ().ProduceType (PSqlType.Null)
						.Or (PDecimal.SqlToken ().ProduceType (PSqlType.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.SqlToken ().ProduceType (PSqlType.Int))
						.Or (PBooleanLiteral.SqlToken ().ProduceType (PSqlType.Bool))
						.Or (PSingleQuotedString.SqlToken ().ProduceType (PSqlType.VarChar))
						.Or (PParentsST.Select<SPolynom, Func<RequestContext, NamedTyped>> (p =>
							rc => p.GetResult (rc)))
						.Or (PFunctionCallST.Select<string[], Func<RequestContext, NamedTyped>> (p => rc =>
							rc.ModuleContext.GetFunction (p)
							))
						// PQualifiedIdentifier must be or-ed after PFunctionCall
						.Or (PQualifiedIdentifierLST
							.Select<string[], Func<RequestContext, NamedTyped>> (p => rc =>
							rc.NamedDict[p.JoinDot ()]
							))
						.Or (PSelectFirstColumnST)
				;

			var PAtomicST =
					(
						from rn in SpracheUtils.SqlToken ("row_number")
						from _1 in SpracheUtils.AnyTokenST ("( ) over (")
						from _2 in
						(
							from _3 in SpracheUtils.AnyTokenST ("partition by")
							from _4 in PExpressionRefST.Get.CommaDelimitedST ()
							select 0
						).Optional ()
						from _5 in POrderByClauseOptionalST
						from _6 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (rn, PSqlType.Int))
					)
					.Or (
						from f in SpracheUtils.AnyTokenST ("sum", "min", "max")
						from _1 in SpracheUtils.SqlToken ("(")
						from _2 in SpracheUtils.SqlToken ("distinct").Optional ()
						from exp in PExpressionRefST.Get
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => exp.GetResult (rc).WithName (f))
					)
					.Or (
						from f in SpracheUtils.SqlToken ("count")
						from _1 in SpracheUtils.SqlToken ("(")
						from _2 in SpracheUtils.SqlToken ("distinct").Optional ()
						from exp in PAsteriskSelectEntryST.Return (0).Or (PExpressionRefST.Get.Return (0))
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (f, PSqlType.Int))
					)
					.Or (
						from f in SpracheUtils.SqlToken ("array_agg")
						from _1 in SpracheUtils.SqlToken ("(")
						from _2 in SpracheUtils.SqlToken ("distinct").Optional ()
						from exp in PExpressionRefST.Get
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => exp.GetResult (rc).ToArray ().WithName (f))
					)
					.Or (PUnnestST)
					.Or (
						from f in SpracheUtils.SqlToken ("coalesce")
						from _1 in SpracheUtils.SqlToken ("(")
						from exp in PExpressionRefST.Get
						from _2 in SpracheUtils.SqlToken (",")
						from subst in PExpressionRefST.Get
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc =>
						{
							var ExpRes = exp.GetResult (rc);
							return (ExpRes.Type == PSqlType.Null ? subst.GetResult (rc) : ExpRes).WithName (f);
						})
					)
					.Or (
						from kw in PAlphaNumericL.SqlToken ()
						let type = kw.GetExpressionType ()
						where type != null
						select (Func<RequestContext, NamedTyped>)(rc =>
							new NamedTyped (kw, PSqlType.GetForSqlTypeName (kw.GetExpressionType ())))
					)
					.Or (
						(
							from kw in SpracheUtils.AnyTokenST ("all", "any", "some")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (PSqlType.Null)
					)
					.Or (
						(
							from kw in SpracheUtils.SqlToken ("exists")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (PSqlType.Bool)
					)
					.Or (
						from case_c in GetCase (PExpressionRefST.Get, PExpressionRefST.Get)
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (
							case_c.ElseC.GetOrDefault ()?.GetResult (rc).Name ?? case_c.CaseH,
							case_c.Branches.First ().GetResult (rc).Type
						))
					)
					.Or (PArrayST)
					.Or (PBaseAtomicST)
				;

			var PAtomicPrefixGroupOptionalST =
					PSignPrefix.SqlToken ()
						.Or (PNegationST)
						.Many ()
						.Optional ()
				;

			var PAtomicPostfixOptionalST =
					PBracketsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.None, false,
							(l, r) => rc =>
							{
								var NamedTyped = l (rc);
								return NamedTyped.WithType (NamedTyped.Type.BaseType);
							}))
						.Or (PSimpleTypeCastST.Select (tc => new OperatorProcessor (PSqlOperatorPriority.Typecast, false,
							(l, r) => rc => l (rc).WithType (PSqlType.GetForSqlTypeName(tc)))))
						.Or (PNullMatchingOperatorsST.Select (m => new OperatorProcessor (PSqlOperatorPriority.Is, false,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Many ()
						.Optional ()
				;

			var PBinaryOperatorsST =
						PBinaryJsonOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.General,
							true,
							OperatorProcessor.GetForBinaryOperator (b)))
						.Or (PBinaryMultiplicationOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.MulDiv,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryAdditionOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.AddSub,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryExponentialOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Exp,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryComparisonOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.Comparison, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryIncludeOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.In, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryRangeOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Like, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryMatchingOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Is, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryConjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.And, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool), IsAnd: true)))
						.Or (PBinaryDisjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Or, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryGeneralTextOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.General, true,
							(l, r) => rc => new NamedTyped (PSqlType.VarChar))))
						.Or (PBetweenOperatorST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Between, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool), true)))
				;

			var PPolynomST =
					from pref1 in PAtomicPrefixGroupOptionalST
					from at1 in PAtomicST
					from post1 in PAtomicPostfixOptionalST
					from rest in
					(
						from op in PBinaryOperatorsST

						from prefN in PAtomicPrefixGroupOptionalST
						from atN in PAtomicST
						from postN in PAtomicPostfixOptionalST
						select new { op, atN, postN }
					).Many ()
					select new SPolynom
					{
						Operators = rest.Select (e => e.op).ToList (),
						Operands = new SPolynom.Operand { Atomic = at1, Postfixes = post1 }.ToTrivialArray ()
							.Concat (rest.Select (e => new SPolynom.Operand { Atomic = e.atN, Postfixes = e.postN }))
							.ToList ()
					}
				;

			PExpressionRefST.Parser = PPolynomST;

			//
			var PSingleSelectEntryST =
					from exp in PExpressionRefST.Get
					from alias_cl in
						(
							from kw_as in SpracheUtils.AnyTokenST ("as")
							from id in PAsColumnAliasLST
							select id
						)
						.Or
						(
							PDirectColumnAliasLST
						).Optional ()
					select (Func<RequestContext, IReadOnlyList<NamedTyped>>)(rc =>
							{
								var nt = exp.GetResult (rc);
								var res = alias_cl.IsDefined
									? nt.WithName (alias_cl.Get ())
									: nt;

								return res.ToTrivialArray ();
							}
						)
				;

			var PSelectListST =
					PAsteriskSelectEntryST
						.Or (PSingleSelectEntryST)
						.CommaDelimitedST ()
						.Select<IEnumerable<Func<RequestContext, IReadOnlyList<NamedTyped>>>, Func<RequestContext, IReadOnlyList<NamedTyped>>> (
							list => rc => list
								.SelectMany (e => e (rc))
								.ToArray ()
							)
				;

			Func<IEnumerable<string>, Parser<IOption<string>>> PTableAliasClauseOptionalST = excl =>
				(
					from kw_as in SpracheUtils.SqlToken ("as").Optional ()
					from id in PTableAliasLST
					where kw_as.IsDefined || excl == null || excl.All (s => s != id)
					select id
				).Optional ()
				;

			var PValuesClauseST =
					from _1 in SpracheUtils.SqlToken ("values")
					from v in PExpressionRefST.Get
						.CommaDelimitedST ()
						.InParentsST ()
						.CommaDelimitedST ()
					select v.First ()
				;

			var PValuesSourceST =
					from v in PValuesClauseST.InParentsST ()
					from kw_as in SpracheUtils.SqlToken ("as").Optional ()
					from table_name in
						PTableAliasLST // here: use PTableAliasClauseOptionalST to exclude selected keywords
					from column_names in PColumnNameLST.CommaDelimitedST ().InParentsST ().Optional ()
					select new ValuesBlock (v.ToArray (),
						table_name,
						column_names.GetOrDefault ()?.ToArray () ?? new string[0])
				;

			var PFromTableExpressionST =
					from table in
					(
						PUnnestST.Select<Func<RequestContext, NamedTyped>, ITableRetriever> (p =>
								new UnnestTableRetriever (p))
							// or-ed after unnest
							.Or (PFunctionCallST.Select (qi => new NamedTableRetriever (qi) // stub
							))
							// or-ed after function calls
							.Or (PQualifiedIdentifierLST.Select (qi => new NamedTableRetriever (qi)))
							.Or (PFullSelectStatementRefST.Get.InParentsST ())
							.Or (PValuesSourceST)
					)
					from alias_cl in PTableAliasClauseOptionalST (
						new[]
						{
							"loop", "on", "inner", "left", "right", "cross", "join", "where", "group", "order", "limit",
							"having"
						}
					) // stub, for cases like 'for ... in select * from mytable loop ... end loop'
					select new FromTableExpression (table, alias_cl.GetOrDefault ())
				;

			var PFromClauseOptionalST =
				(
					from kw_from in SpracheUtils.SqlToken ("from")
					from t1 in PFromTableExpressionST
					from tail in
						(
							from kw_joinN in SpracheUtils.AnyTokenST ("join", "inner join", "left join", "right join")
							from tN in PFromTableExpressionST
							from kw_onN in SpracheUtils.SqlToken ("on")
							from condexpN in PExpressionRefST.Get
							select tN
						)
						.Or (
							from kw_joinN in SpracheUtils.AnyTokenST ("cross join")
							from tN in PFromTableExpressionST
							select tN
						)
						.Many ()
					select t1.ToTrivialArray ().Concat (tail).ToArray ()
				).Optional ()
				;

			var PWhereClauseOptionalST =
				(
					from kw_where in SpracheUtils.SqlToken ("where")
					from cond in PExpressionRefST.Get
					select 0
				).Optional ()
				;

			var POrdinarySelectST =
					from kw_select in SpracheUtils.SqlToken ("select")
					from distinct in
					(
						from _1 in SpracheUtils.SqlToken ("distinct")
						from _2 in
						(
							from _3 in SpracheUtils.SqlToken ("on")
							from _4 in PExpressionRefST.Get.InParentsST ()
							select 0
						).Optional ()
						select 0
					).Optional ()
					from list in PSelectListST
					from into_t1 in
					(
						from _1 in SpracheUtils.SqlToken ("into")
						from _2 in PQualifiedIdentifierLST.CommaDelimitedST ()
						select 0
					).Optional ()
					from from_cl in PFromClauseOptionalST
					from into_t2 in
					(
						from _1 in SpracheUtils.SqlToken ("into")
						from _2 in PQualifiedIdentifierLST.CommaDelimitedST ()
						select 0
					).Optional ()
					from _w in PWhereClauseOptionalST
					from _g in PGroupByClauseOptionalST
					select new OrdinarySelect (list, from_cl)
				;

			var PSelectST =
					from seq in POrdinarySelectST
						.DelimitedBy (SpracheUtils.AnyTokenST ("union all", "union", "except", "subtract"))
						.Select (ss => ss.ToArray ())
					from ord in POrderByClauseOptionalST
					from limit in SpracheUtils.SqlToken ("limit").Then (p => Parse.Number.SqlToken ()).Optional ()
					select new SelectStatement (seq[0].List, seq[0].FromClause.GetOrDefault ())
				;

			var PCteLevelST =
					from name in PColumnNameLST
					from kw_as in SpracheUtils.SqlToken ("as")
					from select_exp in PSelectST.InParentsST ()
					select new SelectStatement (select_exp, name)
				;

			var PCteTopOptionalST =
				(
					from kw_with in SpracheUtils.SqlToken ("with")
					from kw_recursive in SpracheUtils.SqlToken ("recursive").Optional ()
					from levels in PCteLevelST.CommaDelimitedST ()
					select levels
				).Optional ()
				;

			var PSelectFullST =
					from cte in PCteTopOptionalST
					from select_body in PSelectST
					select new FullSelectStatement (cte, select_body)
				;
			PFullSelectStatementRefST.Parser = PSelectFullST;

			var PInsertFullST =
					// insert
					from cte in PCteTopOptionalST
					from _1 in SpracheUtils.AnyTokenST ("insert into")
					from table_name in PQualifiedIdentifierLST
					from _3 in PColumnNameLST.CommaDelimitedST ().AtLeastOnce ()
						.InParentsST ()
						.Optional ()
					from _4 in PValuesClauseST.Return (0)
						.Or (PSelectST.Return (0))
					from conflict in
					(
						from _on in SpracheUtils.AnyTokenST ("on conflict")
						from trg in PExpressionRefST.Get.InParentsST ()
						from _1 in SpracheUtils.SqlToken ("do")
						from act in SpracheUtils.SqlToken ("nothing").Return (0)
							.Or (
								from act in SpracheUtils.AnyTokenST ("update set")
								from _set in
								(
									from col in PColumnNameLST
									from eq in SpracheUtils.SqlToken ("=")
									from val in PExpressionRefST.Get
									select 0
								).CommaDelimitedST ()
								from wh in PWhereClauseOptionalST
								select 0
							)
						select 0
					).Optional ()
					from returning in
					(
						from _1 in SpracheUtils.SqlToken ("returning")
						from _sel in PSelectListST
						select _sel
					).Optional ()
					select returning.IsDefined
						? new FullSelectStatement (null,
							new SelectStatement (returning.Get (),
								new FromTableExpression (new NamedTableRetriever (table_name), null).ToTrivialArray ()))
						: null
				;

			var PDeleteFullST =
					// delete
					from cte in PCteTopOptionalST
					from _1 in SpracheUtils.AnyTokenST ("delete from")
					from table_name in PQualifiedIdentifierLST
					from al in PTableAliasClauseOptionalST (null)
					from _3 in PFromClauseOptionalST
					from _4 in PWhereClauseOptionalST
					from returning in
					(
						from _1 in SpracheUtils.SqlToken ("returning")
						from _sel in PSelectListST
						select _sel
					).Optional ()
					select returning.IsDefined
						? new FullSelectStatement (null,
							new SelectStatement (returning.Get (),
								new FromTableExpression (new NamedTableRetriever (table_name), null).ToTrivialArray ()))
						: null
				;

			var POpenDatasetST =
					from kw_open in SpracheUtils.SqlToken ("open")
					from name in PColumnNameLST
					from _cm1 in SpracheUtils.AllCommentsST ()
					from kw_for in Parse.IgnoreCase ("for")
					from _cm2 in SpracheUtils.AllCommentsST ()
					select new OpenDataset (name, _cm2.ToArray ())
				;

			var PDataReturnStatementST =
					from open in POpenDatasetST
					from p_select in PSelectFullST
						.Or (PInsertFullST)
						.Or (PDeleteFullST)
					select new DataReturnStatement (open, p_select)
				;

			//
			Ref<DataReturnStatement[]> PInstructionRefST = new Ref<DataReturnStatement[]> ();

			var PLoopST =
					from _1 in SpracheUtils.SqlToken ("loop")
					from ins in PInstructionRefST.Get.AtLeastOnce ()
					from _2 in SpracheUtils.AnyTokenST ("end loop")
					select ins.SelectMany (e => e).ToArray ()
				;

			var PLoopExST =
					from header in
					(
						(
							from _1 in SpracheUtils.SqlToken ("while")
							from _2 in PExpressionRefST.Get
							select 0
						)
						.Or
						(
							from _1 in SpracheUtils.SqlToken ("for")
							from _2 in PColumnNameLST
							from _3 in SpracheUtils.SqlToken ("in")
							from _4 in SpracheUtils.SqlToken ("reverse").Optional ()
							from in_c in
								(
									from _5 in PExpressionRefST.Get
									from _6 in SpracheUtils.SqlToken ("..")
									from _7 in PExpressionRefST.Get
									from _8 in
									(
										from _1 in SpracheUtils.SqlToken ("by")
										from _2 in PExpressionRefST.Get
										select 0
									).Optional ()
									select 0
								)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								// just one level of () is provided currently
								.Or (PFullSelectStatementRefST.Get.InParentsST ().Return (0))
							select 0
						)
						.Or
						(
							from _1 in SpracheUtils.SqlToken ("foreach")
							from _2 in PColumnNameLST
							from _3 in
							(
								from _4 in SpracheUtils.SqlToken ("slice")
								from _5 in PExpressionRefST.Get
								select 0
							).Optional ()
							from _6 in SpracheUtils.AnyTokenST ("in array")
							from _7 in PExpressionRefST.Get
							select 0
						)
					).Optional ()
					from body in PLoopST
					select body
				;

			var PExceptionBlockST =
					from _exc in SpracheUtils.SqlToken ("exception")
					from _blocks in
					(
						from _when in SpracheUtils.SqlToken ("when")
						from _cond in PExpressionRefST.Get
						from _then in SpracheUtils.SqlToken ("then")
						from _inst in PInstructionRefST.Get.AtLeastOnce ()
						select _inst
					).AtLeastOnce ()
					select _blocks
						.SelectMany (b => b)
						.SelectMany (b => b)
						.ToArray ()
				;

			var PBeginEndST =
					from _1 in SpracheUtils.SqlToken ("begin")
					from inst in PInstructionRefST.Get.Many ()
					from exc in PExceptionBlockST.Optional ()
					from _2 in SpracheUtils.SqlToken ("end")
					select inst
						.SelectMany (i => i)
						.ConcatIfNotNull (exc.GetOrDefault ())
						.ToArray ()
				;

			var PInstructionST =
					from body in
						(
							from drs in
								// open-for-select
								PDataReturnStatementST
									.Or (
										(
											// variable assignment
											from _1 in PColumnNameLST
											from _2 in SpracheUtils.AnyTokenST (":=", "=")
											from _3 in PExpressionRefST.Get
											select 0
										)
										.Or (PSelectFullST.Return (0))
										.Or (SpracheUtils.SqlToken ("null").Return (0))
										.Or (PInsertFullST.Return (0))
										.Or (PDeleteFullST.Return (0))
										.Or
										(
											// update
											from cte in PCteTopOptionalST
											from _1 in SpracheUtils.SqlToken ("update")
											from _2 in PQualifiedIdentifierLST
											from al in PTableAliasClauseOptionalST ("set".ToTrivialArray ())
											from _3 in SpracheUtils.SqlToken ("set")
											from _4 in
											(
												from _1 in PColumnNameLST
												from _2 in SpracheUtils.SqlToken ("=")
												from _3 in PExpressionRefST.Get
												select 0
											).CommaDelimitedST ().AtLeastOnce ()
											from _5 in PFromClauseOptionalST
											from _6 in PWhereClauseOptionalST
											select 0
										)
										.Or
										(
											from _1 in SpracheUtils.SqlToken ("return")
											from _2 in PExpressionRefST.Get.Optional ()
											select 0
										)
										.Or
										(
											from _1 in SpracheUtils.SqlToken ("call")
											from _2 in PQualifiedIdentifierLST
											from _3 in PExpressionRefST.Get.CommaDelimitedST (true).InParentsST ()
											select 0
										)
										.Or
										(
											from _1 in SpracheUtils.AnyTokenST ("raise exception")
											from _2 in PExpressionRefST.Get
											select 0
										)
										.Or (GetCase (PExpressionRefST.Get, PInstructionRefST.Get).Return (0))
										.Select (n => DataReturnStatement.Void)
									)
							select drs.ToTrivialArray ()
						)
						.Or
						(
							from _1 in SpracheUtils.SqlToken ("if")
							from _2 in PExpressionRefST.Get
							from _3 in SpracheUtils.SqlToken ("then")
							from ThenIns in PInstructionRefST.Get.Many () //AtLeastOnce ()
							from ElsifC in
							(
								from _1 in SpracheUtils.AnyTokenST ("elsif", "elseif")
								from _2 in PExpressionRefST.Get
								from _3 in SpracheUtils.SqlToken ("then")
								from ins in PInstructionRefST.Get.AtLeastOnce ()
								select ins
							).Many ()
							from ElseC in
							(
								from _1 in SpracheUtils.SqlToken ("else")
								from ins in PInstructionRefST.Get.AtLeastOnce ()
								select ins
							).Optional ()
							from _4 in SpracheUtils.AnyTokenST ("end if")
							select ThenIns
								.Concat (ElsifC.SelectMany (e => e))
								.ConcatIfNotNull (ElseC.GetOrDefault ())
								.SelectMany (e => e)
								.ToArray ()
						)
						.Or (PBeginEndST)
						.Or (PLoopExST)
					from _ in SpracheUtils.SqlToken (";")
					select body
				;

			PInstructionRefST.Parser = PInstructionST;

			// procedure
			var PProcedureST =
					from declare in
					(
						from _1 in SpracheUtils.SqlToken ("declare")
						from vars in
						(
							from name in PColumnNameLST
							from type in PTypeST
							from init in
							(
								from _1 in SpracheUtils.AnyTokenST (":=", "=")
								from _2 in PExpressionRefST.Get
								select 0
							).Optional ()
							from _2 in SpracheUtils.SqlToken (";")
							select new NamedTyped (name, PSqlType.GetForSqlTypeName (type))
						).AtLeastOnce ()
						select vars.ToArray ()
					).Optional ()
					from body in PBeginEndST
					select new { vars = declare.GetOrElse (new NamedTyped[0]), body }
				;

			//	DEBUG
			/*
			(
				from _1 in PProcedureST
				from _2 in SpracheUtils.SqlToken ("~")
				select 0
			).Parse (@"
BEGIN
END
~
");
*/

			//
			Dictionary<string, DbTable> TablesDict = new Dictionary<string, DbTable> ();
			Dictionary<string, Procedure> ProceduresDict = new Dictionary<string, Procedure> ();
			Dictionary<string, PSqlType> FunctionsDict = new Dictionary<string, PSqlType> ();
			List<string> SchemaOrder = new List<string> ();

			ReadDatabase (ConnectionString, TablesDict, ProceduresDict, FunctionsDict, SchemaOrder);

			// parse all procedures
			Module ModuleReport = new Module { Procedures = new List<Datasets.Procedure> () };

			foreach (var proc in ProceduresDict.Values)
			{
				if (proc.Arguments.Any (a => a.Type == null))
				{
					// here: handle table types
					Console.WriteLine ($"{proc.Name} failed: unknown argument type");
					continue;
				}

				var ProcedureReport = new Datasets.Procedure
				{
					Schema = proc.Schema,
					Name = proc.Name,
					Arguments = proc.Arguments.Select (a =>
						new Datasets.Argument
						{
							Name = a.Name,
							SqlType = new SqlType (a.Type),
							IsOut = a.Direction == Argument.DirectionType.InOut
						}).ToList (),
					ResultSets = new List<ResultSet> ()
				};

				try
				{
					var Parse = PProcedureST.Parse (proc.SourceCode);

					ModuleContext mcProc = new ModuleContext (
						proc.Name,
						SchemaOrder,
						TablesDict,
						FunctionsDict,
						Parse.vars
							.Concat (proc.Arguments)
							.ToDictionary (v => v.Name)
					);

					RequestContext rcProc = new RequestContext (mcProc);

					foreach (var drs in Parse.body
						         .Where (s => s != null)
					        )
					{
						NamedDataReturn Set = drs.GetResult (rcProc);

						ResultSet ResultSetReport = new ResultSet
						{
							Name = Set.Name,
							Comments = Set.Comments.ToList (),
							Columns = Set.Table.Columns.Select (c => new Column
							{
								Name = c.Name,
								SqlType = new SqlType (c.Type)
							}).ToList ()
						};

						ProcedureReport.ResultSets.Add (ResultSetReport);
					}

					ModuleReport.Procedures.Add (ProcedureReport);
					Console.WriteLine ($"{proc.Name} ok");
				}
				catch (Exception ex)
				{
					Console.WriteLine ($"{proc.Name} failed: {ex.Message}");
				}
			}

			ModuleReport.Procedures = ModuleReport.Procedures
					.OrderBy (p => p.Schema)
					.ThenBy (p => p.Name)
					.ToList ()
				;

			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));
		}
	}
}
