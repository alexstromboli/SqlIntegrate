using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Sprache;
using Newtonsoft.Json;

using Utils;
using ParseProcs.Datasets;

namespace ParseProcs
{
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
			// name (simple or qualified) to NamedTyped
			List<Tuple<string, NamedTyped>> AllColumns = new List<Tuple<string, NamedTyped>> ();

			Dictionary<string, IReadOnlyList<NamedTyped>> Asterisks =
				new Dictionary<string, IReadOnlyList<NamedTyped>> ();
			List<NamedTyped> AllAsteriskedEntries = new List<NamedTyped> ();

			if (Froms != null && Froms.Length > 0)
			{
				foreach (var f in Froms)
				{
					ITable Table = f.TableRetriever.GetTable (Context);
					var Refs = Table.GetAllColumnReferences (Context.ModuleContext, f.Alias);
					AllColumns.AddRange (Refs.Columns.Select (p => new Tuple<string, NamedTyped> (p.Key, p.Value)));

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
				.Concat (Context.ModuleContext.VariablesDict.Select (p => new Tuple<string, NamedTyped> (p.Key, p.Value)))
				.ToLookup (c => c.Item1)
				.Where (g => g.Count () == 1)
				.ToDictionary (g => g.Key, g => g.First ().Item2)
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

		public class KeyedType
		{
			public string given_as;
			public PSqlType key;

			public KeyedType (string given_as, PSqlType key)
			{
				this.given_as = given_as;
				this.key = key;
			}
		}

		public class WordKeyedType
		{
			public string[] Words;
			public KeyedType Entry;
		}


		protected static Parser<KeyedType> GroupByWord (
			IEnumerable<WordKeyedType> Types,
			Parser<string> PAlphaNumericL,
			Parser<string> PDoubleQuotedString,
			int Skip = 0
			)
		{
			Parser<KeyedType> End = Types
					.Where (t => t.Words.Length == Skip)
					.Select (t => Parse.Return (t.Entry))
					.FirstOrDefault ()
				;

			var Loo = Types
					.Where (t => t.Words.Length > Skip)
					.ToLookup (t => t.Words[Skip])
				;

			Parser<KeyedType> Next = null;

			if (Loo.Count > 0)
			{
				Dictionary<string, Parser<KeyedType>> Map = Loo
					.ToDictionary (
						p => p.Key,
						p => GroupByWord (p, PAlphaNumericL, PDoubleQuotedString, Skip + 1)
					);

				/*
				Next = (
					from f in PAlphaNumericL
						.Or (PDoubleQuotedString)
						.Or (Parse.Chars ('[', ']', '.').Select (c => c.ToString ()))
						.SqlToken ()
					let th = Map.TryGetValue (f, out var p) ? p : null
					where th != null
					);

						.Select (s => Map.TryGetValue (s, out var p) ? p : null)
						.Where (p => p != null)
						.Select (p => p.Parse (""))
					;
					*/
			}

			if (Next != null && End != null)
			{
				return Next.Or (End);
			}

			if (Next != null)
			{
				return Next;
			}

			return End;
		}

		static void Main (string[] args)
		{
			string ConnectionString = args[0];
			string OutputFileName = args[1];

			//
			var DatabaseContext = ReadDatabase (ConnectionString);

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

			var PAlphaNumericOrQuotedLST = PAlphaNumericL
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;

			var PAsColumnAliasLST = PAlphaNumericOrQuotedLST;

			// direct or 'as'
			var PTableAliasLST = PColumnNameLST;

			// lowercase
			var PQualifiedIdentifierLST =
					from k1 in PColumnNameLST
					from kn in
					(
						from d in SpracheUtils.SqlToken (".")
						from n in PAlphaNumericOrQuotedLST
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
				"like", "ilike"
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

			// types

			// here: provide for
			// select '1979-12-07'::character varying;
			// vs
			// select '1979-12-07'::character "varying";

			// here: skip quoting for built-in types like 'timestamp with time zone'

			Parser<KeyedType> PTypeTitleST = GroupByWord (
				DatabaseContext.TypeMap.Map
					.Select (p => new WordKeyedType
					{
						Words = Regex.Matches (p.Key, @"\[|\]|\.|[^\s\[\]\.]+").Select (m => m.Value).ToArray (),
						Entry = new KeyedType (p.Key, p.Value)
					}),
				PAlphaNumericL,
				PDoubleQuotedString
				);

			var PTypeST =
					from t in PTypeTitleST
					from p in Parse.Number.SqlToken ()
						.CommaDelimitedST ()
						.InParentsST ()
						.Optional ()
					from _ps in SpracheUtils.AnyTokenST ("% rowtype").Optional ()
					from array in
						(
							from _1 in Parse.String ("[").SqlToken ()
							from _2 in Parse.String ("]").SqlToken ()
							select 1
						)
						.AtLeastOnce ()
						.Optional ()
					select t
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
									.FirstOrDefault (nt => nt.Type != DatabaseContext.TypeMap.Null) ??
								new NamedTyped (DatabaseContext.TypeMap.Null))
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
					PNull.SqlToken ().ProduceType (DatabaseContext.TypeMap.Null)
						.Or (PDecimal.SqlToken ().ProduceType (DatabaseContext.TypeMap.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.SqlToken ().ProduceType (DatabaseContext.TypeMap.Int))
						.Or (PBooleanLiteral.SqlToken ().ProduceType (DatabaseContext.TypeMap.Bool))
						.Or (PSingleQuotedString.SqlToken ().ProduceType (DatabaseContext.TypeMap.VarChar))
						.Or (PParentsST.Select<SPolynom, Func<RequestContext, NamedTyped>> (p =>
							rc => p.GetResult (rc)))
						.Or (PFunctionCallST.Select<string[], Func<RequestContext, NamedTyped>> (p => rc =>
							rc.ModuleContext.GetFunction (p)
							))
						// PQualifiedIdentifier must be or-ed after PFunctionCall
						.Or (PQualifiedIdentifierLST
							.Select<string[], Func<RequestContext, NamedTyped>> (p => rc =>
							{
								string Key = p.JoinDot ();
								return rc.NamedDict.TryGetValue (Key, out var V) ? V : throw new KeyNotFoundException ("Not found " + Key);
							}))
						.Or (PSelectFirstColumnST)
				;

			var PAtomicST =
					(
						from rn in SpracheUtils.AnyTokenST ("row_number", "rank")
						from _1 in SpracheUtils.AnyTokenST ("( ) over (")
						from _2 in
						(
							from _3 in SpracheUtils.AnyTokenST ("partition by")
							from _4 in PExpressionRefST.Get.CommaDelimitedST ()
							select 0
						).Optional ()
						from _5 in POrderByClauseOptionalST
						from _6 in SpracheUtils.SqlToken (")")
						select (Func<RequestContext, NamedTyped>)
							(rc => new NamedTyped (rn, DatabaseContext.TypeMap.Int))
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
						select (Func<RequestContext, NamedTyped>)(rc =>
							new NamedTyped (f, DatabaseContext.TypeMap.BigInt))
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
							return (ExpRes.Type == DatabaseContext.TypeMap.Null ? subst.GetResult (rc) : ExpRes)
								.WithName (f);
						})
					)
					.Or (
						from kw in PAlphaNumericL.SqlToken ()
						let type = kw.GetExpressionType ()
						where type != null
						select (Func<RequestContext, NamedTyped>)(rc =>
							new NamedTyped (kw, DatabaseContext.GetTypeForName ("pg_catalog", type)))
					)
					.Or (
						(
							from kw in SpracheUtils.AnyTokenST ("all", "any", "some")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (DatabaseContext.TypeMap.Null)
					)
					.Or (
						(
							from kw in SpracheUtils.SqlToken ("exists")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (DatabaseContext.TypeMap.Bool)
					)
					.Or (
						from case_c in GetCase (PExpressionRefST.Get, PExpressionRefST.Get)
						select (Func<RequestContext, NamedTyped>)(rc => new NamedTyped (
							case_c.ElseC.GetOrDefault ()?.GetResult (rc).Name ?? case_c.CaseH,
							case_c.Branches.First ().GetResult (rc).Type
						))
					)
					.Or (PArrayST)
					.Or (PExpressionRefST.Get.CommaDelimitedST ().InParentsST ()		// ('one', 'two', 'three')
						.Where (r => r.Count () > 1)
						.ProduceType (DatabaseContext.TypeMap.Record))
					.Or (	// interval '90 days'
						(
							from t in PTypeST
							from v in PSingleQuotedString.SqlToken ()
							select t.key
						).ProduceType () // here: get default column name
					)
					.Or (PBaseAtomicST)
				;

			var PAtomicPrefixGroupOptionalST =
					PSignPrefix.SqlToken ()
						.Or (PNegationST)
						.Many ()
						.Optional ()
				;

			var PTakePropertyST =
					from _1 in SpracheUtils.SqlToken (".")
					from prop in PColumnNameLST
					select prop
				;

			var PAtomicPostfixOptionalST =
					PBracketsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.None, false,
							(l, r) => rc =>
							{
								var NamedTyped = l (rc);
								return NamedTyped.WithType (NamedTyped.Type.BaseType);
							}))
						.Or (PSimpleTypeCastST.Select (tc => new OperatorProcessor (PSqlOperatorPriority.Typecast,
							false,
							(l, r) => rc => l (rc).WithType (tc.key))))
						.Or (PNullMatchingOperatorsST.Select (m => new OperatorProcessor (PSqlOperatorPriority.Is,
							false,
							(l, r) => rc => new NamedTyped (DatabaseContext.TypeMap.Bool))))
						.Or (PTakePropertyST.Select (prop => new OperatorProcessor (PSqlOperatorPriority.None, false,
							(l, r) => rc => new NamedTyped (prop,
								l (rc).Type.PropertiesDict[prop].Type)
						)))
						.Many ()
						.Optional ()
				;

			var PBinaryOperatorsST =
					PBinaryJsonOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.General,
							true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b)))
						.Or (PBinaryMultiplicationOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.MulDiv,
							true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b))))
						.Or (PBinaryAdditionOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.AddSub,
							true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b))))
						.Or (PBinaryExponentialOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Exp,
							true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b))))
						.Or (PBinaryComparisonOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.Comparison, true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryIncludeOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.In, true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryRangeOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Like,
							true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryMatchingOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Is,
							true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryConjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.And, true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool), IsAnd: true)))
						.Or (PBinaryDisjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Or, true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryGeneralTextOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.General, true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b))))
						.Or (PBetweenOperatorST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Between, true,
							OperatorProcessor.ProduceType (DatabaseContext.TypeMap.Bool), true)))
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
						.Or (SpracheUtils.SqlToken ("default").Return (PExpressionRefST.Get.Parse ("0")))
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
							from kw_joinN in SpracheUtils.AnyTokenST ("cross join", ",")
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
						from trg in PExpressionRefST.Get.InParentsST ().Optional ()
						from _1 in SpracheUtils.SqlToken ("do")
						from act in SpracheUtils.SqlToken ("nothing").Return (0)
							.Or (
								from act in SpracheUtils.AnyTokenST ("update set")
								from _set in
								(
									from col in PQualifiedIdentifierLST
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
												from _1 in PQualifiedIdentifierLST
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
							select type.key != null
								? new NamedTyped (name, type.key)
								: throw new InvalidOperationException ("Type of variable " + name + " (" + type.given_as + ") is not supported")
						).AtLeastOnce ()
						select vars.ToArray ()
					).Optional ()
					from body in PBeginEndST
					select new { vars = declare.GetOrElse (new NamedTyped[0]), body }
				;

			//	DEBUG
			foreach (var t in new[]
				         { "char", "character varying", "app_status", "timestamp", "timestamp with time zone" })
			{
				var rest = (
					from _1 in PTypeST
					from _2 in SpracheUtils.SqlToken ("~")
					select _1
				).Parse (t + " ~");
			}

			// parse all procedures
			Module ModuleReport = new Module { Procedures = new List<Datasets.Procedure> () };

			foreach (var proc in DatabaseContext.ProceduresDict.Values)
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
							Type = a.Type.ToString (),
							PSqlType = a.Type,
							IsOut = a.Direction == Argument.DirectionType.InOut
						}).ToList (),
					ResultSets = new List<ResultSet> ()
				};

				try
				{
					var Parse = PProcedureST.Parse (proc.SourceCode);

					ModuleContext mcProc = new ModuleContext (
						proc.Name,
						DatabaseContext,
						Parse.vars
							.Concat (proc.Arguments)
							.ToDictionary (v => v.Name)
					);

					RequestContext rcProc = new RequestContext (mcProc);

					// to make sure each name is only taken once
					Dictionary<string, ResultSet> ResultSetsDict = new Dictionary<string, ResultSet> ();

					foreach (var drs in Parse.body
						         .Where (s => s != null)
					        )
					{
						NamedDataReturn Set = drs.GetResult (rcProc);

						ResultSet ResultSetReport;
						if (ResultSetsDict.TryGetValue (Set.Name, out ResultSetReport))
						{
							if (ResultSetReport.Comments.Count == 0)
							{
								ResultSetReport.Comments = Set.Comments.ToList ();
							}
						}
						else
						{
							ResultSetReport = new ResultSet
							{
								Name = Set.Name,
								Comments = Set.Comments.ToList (),
								Columns = Set.Table.Columns.Select (c => new Column
								{
									Name = c.Name,
									Type = c.Type.ToString (),
									PSqlType = c.Type
								}).ToList ()
							};

							ResultSetsDict[Set.Name] = ResultSetReport;

							ProcedureReport.ResultSets.Add (ResultSetReport);
						}
					}

					ModuleReport.Procedures.Add (ProcedureReport);
				}
				catch (Exception ex)
				{
					Console.WriteLine ($"{proc.Name} failed: {ex.Message}");
				}
			}

			PSqlType[] DirectlyUsedTypes = ModuleReport.Procedures
					.Select (p => p.Arguments.Select (a => a.PSqlType)
						.Concat (p.ResultSets.Select (rs => rs.Columns.Select (c => c.PSqlType)).SelectMany (t => t))
					)
					.SelectMany (t => t)
					.Distinct (t => t.Display)
					.ToArray ()
				;

			PSqlType[] UsedCustomTypes = DirectlyUsedTypes
					.Select (t => t.BaseType)
					.Where (t => t.IsCustom)
					.Distinct (t => t.Display)
					.ToArray ()
				;

			// include types used indirectly, from properties, by recursion
			while (true)
			{
				var UsedTypesDict = UsedCustomTypes.ToDictionary (t => t.ToString ());
				var ToAdd = UsedCustomTypes
						.Where (t => t.Properties != null)
						.SelectMany (t => t.Properties)
						.Select (p => p.Type.BaseType)
						.Where (t => t.IsCustom)
						.Distinct (t => t.Display)
						.Where (t => !UsedTypesDict.ContainsKey (t.ToString ()))
						.ToArray ()
					;

				if (ToAdd.Length == 0)
				{
					break;
				}

				UsedCustomTypes = UsedCustomTypes
						.Concat (ToAdd)
						.ToArray ()
					;
			}

			// sort
			UsedCustomTypes = UsedCustomTypes
					.OrderBy (t => t.Schema)
					.ThenBy (t => t.OwnName)
					.ToArray ()
				;

			ModuleReport.Procedures = ModuleReport.Procedures
					.OrderBy (p => p.Schema)
					.ThenBy (p => p.Name)
					.ToList ()
				;

			ModuleReport.Types = UsedCustomTypes
					.Select (t => new SqlType (t))
					.ToList ()
				;

			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));
		}
	}
}
