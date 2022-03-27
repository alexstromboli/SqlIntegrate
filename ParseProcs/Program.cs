using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Sprache;
using Newtonsoft.Json;

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
		public string LastComment { get; }

		public OpenDataset (string Name, string LastComment)
		{
			this.Name = Name;
			this.LastComment = LastComment;
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
		public IOption<FromTableExpression[]> FromClause { get; }
		public bool IsMany { get; }
		public string Name { get; }

		public SelectStatement (Func<RequestContext, IReadOnlyList<NamedTyped>> List, IOption<FromTableExpression[]> FromClause, bool IsMany,
			string Name = null)
		{
			this.List = List;
			this.FromClause = FromClause;
			this.IsMany = IsMany;
			this.Name = Name;
		}

		public SelectStatement (SelectStatement Core, string Name)
			: this (Core.List, Core.FromClause, Core.IsMany, Name)
		{
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			List<NamedTyped> AllColumns = new List<NamedTyped> ();
			Dictionary<string, IReadOnlyList<NamedTyped>> Asterisks =
				new Dictionary<string, IReadOnlyList<NamedTyped>> ();
			List<NamedTyped> AllAsteriskedEntries = new List<NamedTyped> ();

			if (FromClause.IsDefined)
			{
				FromTableExpression[] Froms = FromClause.Get ();

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

			if (Cte.IsDefined)
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

	public class NamedDataReturn
	{
		public string Name;
		public ITable Table;
		public string ServiceComment;
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
				ServiceComment = Open.LastComment,
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
			// postfix ST means that the result is 'SQL token',
			// i.e. duly processes comments and whitespaces

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
					from p in Parse.Char ('.')
					from f in Parse.Number.Optional ()
					where i.IsDefined || f.IsDefined
					select i.GetOrElse ("") + p + f.GetOrElse ("")
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

			// valid for function name
			/*
			var PFunctionNameLST = PAlphaNumericL
					.Where (n => n.NotROrC () && PSqlType.GetForSqlTypeName (n) == null)
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;
				*/

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
			var PNullMatchingOperatorsST = SpracheUtils.AnyTokenST ("isnull", "not null");

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
					from t in PBaseTypeST
					from array in
						(
							from _1 in Parse.String ("[").SqlToken ()
							from _2 in Parse.String ("]").SqlToken ()
							select 1
						)
						.AtLeastOnce ()
						.Optional ()
					select t + (array.IsDefined ? "[]" : "");
				;

			var PSimpleTypeCastST =
					from op in Parse.String ("::").SqlToken ()
					from t in PTypeST
					select t
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
						.Or (PSingleQuotedString.SqlToken ().ProduceType (PSqlType.Text))
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
						.Or (PFullSelectStatementRefST.Get.InParentsST ().Select<FullSelectStatement, Func<RequestContext, NamedTyped>> (fss => rc =>
							fss.GetTable (rc, false).Columns[0]
						))
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
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (f, exp.GetResult (rc).Type))
					)
					.Or (
						from f in SpracheUtils.SqlToken ("count")
						from _1 in SpracheUtils.SqlToken ("(")
						from _2 in SpracheUtils.SqlToken ("distinct").Optional ()
						from exp in PExpressionRefST.Get.Return (0).Or (PAsteriskSelectEntryST.Return (0))
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (f, PSqlType.Int))
					)
					.Or (PUnnestST)
					.Or (
						from f in SpracheUtils.SqlToken ("coalesce")
						from _1 in SpracheUtils.SqlToken ("(")
						from exp in PExpressionRefST.Get
						from _2 in SpracheUtils.SqlToken (",")
						from subst in PExpressionRefST.Get
						from _3 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (f, exp.GetResult (rc).Type))
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
							from kw in SpracheUtils.AnyTokenST ("all", "any")
							from exp in PExpressionRefST.Get.InParentsST ()
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
							case_c.ElseC.Get ()?.GetResult (rc).Name ?? case_c.CaseH,
							case_c.Branches.First ().GetResult (rc).Type
						))
					)
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
								return new NamedTyped (NamedTyped.Name, NamedTyped.Type.BaseType);
							}))
						.Or (PSimpleTypeCastST.Select (tc => new OperatorProcessor (PSqlOperatorPriority.Typecast, false,
							(l, r) => rc => new NamedTyped (l (rc).Name, PSqlType.GetForSqlTypeName(tc)))))
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
							(l, r) => rc => new NamedTyped (PSqlType.Text))))
						.Or (PBetweenOperatorST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Between, true,
							(l, r) => rc => new NamedTyped (PSqlType.Text), true)))
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
									? new NamedTyped (alias_cl.Get (), nt.Type)
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

			var PFromTableExpressionST =
				from table in
					(
						PUnnestST.Select<Func<RequestContext, NamedTyped>, ITableRetriever> (p => new UnnestTableRetriever (p))
							// or-ed after unnest
							.Or (PFunctionCallST.Select (qi => new NamedTableRetriever (qi)	// stub
									))
							// or-ed after function calls
							.Or (PQualifiedIdentifierLST.Select (qi => new NamedTableRetriever (qi)))
							.Or (PFullSelectStatementRefST.Get.InParentsST ())
					)
				from alias_cl in PTableAliasClauseOptionalST (
					new[] { "loop", "on", "inner", "left", "right", "cross", "join", "where", "group", "order", "limit", "having" }
					)		// stub, for cases like 'for ... in select * from mytable loop ... end loop'
				select new FromTableExpression (table, alias_cl.GetOrDefault ());

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
					from distinct in SpracheUtils.SqlToken ("distinct").Optional ()
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
					select new SelectStatement (seq[0].List, seq[0].FromClause, seq.Length > 1)
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
					from _2 in PQualifiedIdentifierLST
					from _3 in PColumnNameLST.CommaDelimitedST ().AtLeastOnce ()
						.InParentsST ()
						.Optional ()
					from _4 in
					(
						from _1 in SpracheUtils.SqlToken ("values")
						from _2 in PExpressionRefST.Get.CommaDelimitedST ().AtLeastOnce ()
							.InParentsST ()
						select 0
					).Or (PSelectST.Return (0))
					// here: provide data return
					from _ret in
					(
						from _1 in SpracheUtils.SqlToken ("returning")
						from _sel in PSelectListST
						select 0
					).Optional ()
					select 0
				;

			var POpenDatasetST =
					from kw_open in SpracheUtils.SqlToken ("open")
					from name in PColumnNameLST
					from _cm1 in SpracheUtils.AllCommentsST ()
					from kw_for in Parse.IgnoreCase ("for")
					from _cm2 in SpracheUtils.AllCommentsST ()
					select new OpenDataset (name, _cm2.LastOrDefault ())
				;

			var PDataReturnStatementST =
					from open in POpenDatasetST
					from p_select in PSelectFullST
						.Or (PInsertFullST.Return<int, FullSelectStatement> (null))
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

			var PBeginEndST =
					from _1 in SpracheUtils.SqlToken ("begin")
					from inst in PInstructionRefST.Get.Many ()
					from _2 in SpracheUtils.SqlToken ("end")
					select inst.SelectMany (i => i).ToArray ()
				;

			var PInstructionST =
					from _exc in SpracheUtils.AnyTokenST ("exception when others then").Optional ()
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
										.Or (PInsertFullST)
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
											// delete
											from cte in PCteTopOptionalST
											from _1 in SpracheUtils.AnyTokenST ("delete from")
											from _2 in PQualifiedIdentifierLST
											from al in PTableAliasClauseOptionalST (null)
											from _3 in PFromClauseOptionalST
											from _4 in PWhereClauseOptionalST
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
							from ThenIns in PInstructionRefST.Get.AtLeastOnce ()
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
								.Concat (ElseC.GetOrElse (new DataReturnStatement[0][]))
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

			//
			string ConnectionString = "server=127.0.0.1;port=5432;database=dummy01;uid=alexey;pwd=1234";

			Dictionary<string, DbTable> TablesDict = new Dictionary<string, DbTable> ();
			Dictionary<string, Procedure> ProceduresDict = new Dictionary<string, Procedure> ();
			Dictionary<string, PSqlType> FunctionsDict = new Dictionary<string, PSqlType> ();
			List<string> SchemaOrder = new List<string> ();

			ReadDatabase (ConnectionString, TablesDict, ProceduresDict, FunctionsDict, SchemaOrder);

			// parse all procedures
			Module ModuleReport = new Module { Procedures = new List<Datasets.Procedure> () };

			foreach (var proc in ProceduresDict.Values)
			{
				var ProcedureReport = new Datasets.Procedure { Schema = proc.Schema, Name = proc.Name, ResultSets = new List<ResultSet> () };

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
							Name = Set.Name, Comments = Set.ServiceComment.ToTrivialArray ().ToList (),
							Columns = Set.Table.Columns.Select (c => new Column { Name = c.Name, SqlBaseType = c.Type.BaseType.Display, IsArray = c.Type.IsArray }).ToList ()
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

			File.WriteAllText ("out.json", JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));

			//
			var sel01 = PDataReturnStatementST.Parse ("open ref01 for select 654");
			var sel02 = PDataReturnStatementST.Parse ("open ref01 for select DISTINCT t");
			var sel05 = PDataReturnStatementST.Parse (@"
open ref01 for
-- pfizer
WITH F AS
(
	SELECT 'y' AS pink
), Q AS
(
	SELECT 2.3
)
select	a.b,
		b,		-- repeat
		(select 45 as retry),
		'45',
		NOW() + '02:30'::interval drain,
		SUM(done.best),
		SUM(done.best) AS troy,
		SUM(DISTINCT done.best + 9) AS troy,
		U.*
UNION ALL
select DISTINCT
		a.b,
		b,
		45 AS done,
		'45' AS most
FROM F
	LEFT JOIN (WITH r as (select 6 as mark) select mark from r) log on 5=9
	INNER JOIN dbo.Users AS U ON U.id = F.id_author
WHERE a.b > 10
		OR zen
GROUP BY name
ORDER BY name
LIMIT 10
");

			//
			var opr01 = POpenDatasetST.Parse ("open ref01 for select 654");
			var opr02 = POpenDatasetST.Parse ("OPEn ref01 foR /* r */ /* this */ select kjfgh");
			var opr03 = POpenDatasetST.Parse (
@"-- minus
/* ghfghfhdfyjhrt */
OPEn ref01
/* dfghdfghdrhrt */
-- rtyertyrtyrtghe
foR	-- that
/* r */ /* this
is the one
*/
SELECT
done
"
);

			//
			ModuleContext mc = new ModuleContext (
				"test",
				SchemaOrder,
				TablesDict,
				FunctionsDict,
				new Dictionary<string, NamedTyped>
				{
					["a.b.c"] = new NamedTyped ("a.b.c", PSqlType.Float)
				}
			);
			RequestContext rc = new RequestContext (mc);
			Action<string, PSqlType> TestExpr = (s, t) => System.Diagnostics.Debug.Assert (PExpressionRefST.Get.End ().Parse (s).GetResult (rc).Type == t);
			TestExpr ("5", PSqlType.Int);
			TestExpr ("NOW()", PSqlType.TimestampTz);
			TestExpr ("EXT.sum(2,5)", PSqlType.Decimal);
			TestExpr ("suM(2,5)", PSqlType.BigInt);
			TestExpr ("null::real", PSqlType.Real);
			TestExpr (" null :: REAL ", PSqlType.Real);
			TestExpr ("2.5", PSqlType.Decimal);
			TestExpr ("5::bigint", PSqlType.BigInt);
			TestExpr ("5::bigint+7", PSqlType.BigInt);
			TestExpr ("1+-1.2", PSqlType.Decimal);
			TestExpr ("1--1.2", PSqlType.Int);
			TestExpr ("-1--1.2", PSqlType.Int);
			TestExpr (" 5 :: bigint + 7 ", PSqlType.BigInt);
			TestExpr ("5::smallint+f(a,7,y.\"i\".\"ghost 01\",'test')::money+1.2*a.b.c::real", PSqlType.Money);
			TestExpr (" 5 :: smallint + f(a,7,y.\"i\".\"ghost 01\",'test')::money + 1.2 * a . b . c :: real  ", PSqlType.Money);
			TestExpr (" 5 :: smallint + f(a, 7,y.\"i\".\"ghost 01\",'test')::money + 1.2 * a . b . c :: real  ", PSqlType.Money);
			TestExpr (" 5 :: smallint + f(a, 7 , y . \"i\".\"ghost 01\",'test')::money + 1.2 * a . b . c :: real  ", PSqlType.Money);
			TestExpr (" 5 :: smallint + f ( a , 7 , y . \"i\" . \"ghost 01\" , 'test' ) :: money + 1.2 * a . b . c :: real  ", PSqlType.Money);
			TestExpr ("5+4", PSqlType.Int);
			TestExpr ("5+4*8", PSqlType.Int);
			TestExpr ("(5+4)*8", PSqlType.Int);
			TestExpr ("150-(5+4)::smallint*8", PSqlType.Int);
			TestExpr ("150-(5+4)::bigint*8", PSqlType.BigInt);
			TestExpr ("(150-(5+4)::smallint*8)||'tail'||'_more'", PSqlType.Text);
			TestExpr ("''::interval+''::date", PSqlType.Date);
			TestExpr ("'irrelevant'::date+'nonsense'::interval", PSqlType.Date);
			TestExpr ("'irrelevant'::date+'nonsense'::time", PSqlType.Date);
			TestExpr ("'irrelevant'::date-'nonsense'::date", PSqlType.Interval);
			TestExpr ("5>6", PSqlType.Bool);
			TestExpr ("5<=6", PSqlType.Bool);
			TestExpr (" 5 <= 2*3 AnD NOT 4.5 isnull ", PSqlType.Bool);

			TestExpr ("80-(select 5::money+6)", PSqlType.Money);

			TestExpr ("5 betWEEN 1 and 6", PSqlType.Bool);
			TestExpr ("6*(2+3) betWEEN 1 and 6", PSqlType.Bool);
			TestExpr ("6*(2+3) betWEEN 1 and 6", PSqlType.Bool);
			TestExpr ("true and 6*(2+3) betWEEN 1 and 6 and 6>5 or (5=2+9)", PSqlType.Bool);

			TestExpr ("case when 6 * ( 2 + 3 ) betWEEN 1 and 6 then 'test' else null end", PSqlType.Text);

			TestExpr ("a.b.c::bigint", PSqlType.BigInt);		// test irrelevance of the left part of a type cast
			TestExpr ("('{6, 9, 3}'::int[])[1]", PSqlType.Int);		// test taking an array item

			TestExpr (
@" 5 /* t 67 */  /* t 67 */  /* t 67 */ :: /* t 67 */ /* t 67 */ smallint + f ( a || '--' ,
 /* t 67 */  7 , y . ""as"" .  /* t 67 */  /* t 67 */  ""ghost 01"" +  /* t 67 */  /* t 67 */ 8.30 ,		-- none
 'test /*' ) :: money + 1.2  /* t 67 */  * a . b . c :: real  /* t 67 */  ",
				PSqlType.Money);

			// https://www.postgresql.org/docs/12/sql-syntax-lexical.html

			var s01 = PDoubleQuotedString.Parse ("\"\"");
			var s02 = PDoubleQuotedString.Parse ("\"test\"");
			var s03 = PDoubleQuotedString.Parse ("\"te\n\\\"st\"");
			var s04 = PDoubleQuotedString.Parse ("\"ca\\\"nada\\\\\"");
			var sq01 = PSingleQuotedString.Parse ("''");
			var sq02 = PSingleQuotedString.Parse ("'test'");
			var sq03 = PSingleQuotedString.Parse ("'te\n\\'s'");
			var sq04 = PSingleQuotedString.Parse ("'ca\\'nada\\\\'");
		}
	}
}
