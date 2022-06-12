﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Sprache;

using Utils;
using DbAnalysis.Datasets;

namespace DbAnalysis
{
	public record SProcedure(NamedTyped[] vars, DataReturnStatement[] body);

	public class Analyzer
	{
		protected DatabaseContext DatabaseContext;
		protected Dictionary<int, string> WordsCache;
		protected Parser<string> PDoubleQuotedString;
		protected Ref<SPolynom> PExpressionRefST;
		protected Parser<SPolynom> PExpressionST => PExpressionRefST.Get;
		public Parser<SProcedure> PProcedureST { get; }

		protected Parser<string> PAlphaNumericL
		{
			get
			{
				return i =>
				{
					if (WordsCache.TryGetValue (i.Position, out string Word))
					{
						int Len = Word.Length;
						return Result.Success (Word, new CustomInput (i.Source, i.Position + Len, i.Line, i.Column + Len));
					}

					return Result.Failure<string> (i, "No word found", Array.Empty<string> ());
				};
			}
		}

		public Parser<string> SqlToken (string LineL)
		{
			if (LineL.All (c => char.IsLetterOrDigit (c) || c == '_'))
			{
				// to prevent cases like taking 'order' for 'or'
				// take all the letters, and then check
				return PAlphaNumericL
						.Where (s => s == LineL)
						.SqlToken ()
					;
			}

			return Parse
					.IgnoreCase (LineL)
					.Text ()
					.ToLower ()
					.SqlToken ()
				;
		}

		// postfix ST means that the result is 'SQL token',
		// i.e. duly processes comments and whitespaces
		public Parser<string> AnyTokenST (params string[] Options)
		{
			Parser<string> Result = null;
			foreach (string[] Tokens in Options.Select (s => s.Split (' ', StringSplitOptions.RemoveEmptyEntries)))
			{
				Parser<string> Line = null;
				foreach (string Token in Tokens)
				{
					var PT = SqlToken (Token);
					Line = Line == null
							? PT
							: (from f in Line
								from n in PT
								select f + " " + n)
						;
				}

				Result = Result == null
						? Line
						: Result.Or (Line)
					;
			}

			return Result;
		}

		protected Parser<CaseBase<T>> GetCase<T> (Parser<T> Then)
		{
			return
				from case_h in SqlToken ("case")
				from _2 in PExpressionST.Optional ()
				from branches in
				(
					from _1 in SqlToken ("when")
					from _2 in PExpressionST.CommaDelimitedST ().AtLeastOnce ()
					from _3 in SqlToken ("then")
					from value in Then
					select value
				).AtLeastOnce ()
				from else_c in
				(
					from _1 in SqlToken ("else")
					from value in PExpressionST
					select value
				).Optional ()
				from _3 in AnyTokenST ("end case", "end")
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

			public override string ToString ()
			{
				return given_as;
			}
		}

		public class WordKeyedType
		{
			public string[] Words;
			public KeyedType Entry;

			public override string ToString ()
			{
				return Entry?.ToString () ?? "???";
			}
		}

		protected Parser<KeyedType> GroupByWord (
			IEnumerable<WordKeyedType> Types,
			int Skip = 0
			)
		{
			string NotFoundMessage = "No valid type name found";
			Parser<KeyedType> End = Types
				                        .Where (t => t.Words.Length == Skip)
				                        .Select (t => Parse.Return (t.Entry))
				                        .FirstOrDefault ()
			                        ?? SpracheUtils.Failure<KeyedType> (NotFoundMessage)
				;

			var Loo = Types
					.Where (t => t.Words.Length > Skip)
					.ToLookup (t => t.Words[Skip])
				;

			if (Loo.Count == 0)
			{
				return End;
			}

			Dictionary<string, Parser<KeyedType>> Map = Loo
				.ToDictionary (
					p => p.Key,
					p => GroupByWord (p, Skip + 1).Named (p.Key)
				);

			var FindWordST = PAlphaNumericL
					.Or (PDoubleQuotedString)
					.Or (Parse.Chars ('[', ']', '.').Select (c => c.ToString ()))
					.SqlToken ()
				;

			return i =>
			{
				var WordResult = FindWordST (i);
				if (WordResult.WasSuccessful
				    && !i.Equals (WordResult.Remainder)
				    && Map.TryGetValue (WordResult.Value, out var Next)
				   )
				{
					return Next (WordResult.Remainder);
				}

				return End (i);
			};
		}

		// immediate, i.e. no comments or whitespace
		protected Parser<string> ReadKeywordL (params string[] ValuesL)
		{
			return PAlphaNumericL
					.Where (r => ValuesL.Any (v => v == r))
				;
		}

		public Analyzer (DatabaseContext DatabaseContext)
		{
			this.DatabaseContext = DatabaseContext;
			PExpressionRefST = new Ref<SPolynom> ();
			
			// https://www.postgresql.org/docs/12/sql-syntax-lexical.html
			PDoubleQuotedString =
					from _1 in Parse.Char ('"')
					from s in Parse.CharExcept ('"')
						//.Or (Parse.Char ('\\').Then (c => Parse.AnyChar))
						.Or (Parse.String ("\"\"").Return ('"'))
						.Many ()
						.Text ()
					from _2 in Parse.Char ('"')
					select s
				;

			// postfix ST means that the result is 'SQL token',
			// i.e. duly processes comments and whitespaces

			var PSingleQuotedString =
					from _1 in Parse.Char ('\'')
					from s in Parse.CharExcept ('\'')
						//.Or (Parse.Char ('\\').Then (c => Parse.AnyChar))
						.Or (Parse.String ("''").Return ('\''))
						.Many ()
						.Text ()
					from _2 in Parse.Char ('\'')
					select s
				;

			var PNull = ReadKeywordL ( "null");
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

			var PBooleanLiteralST = AnyTokenST ("true", "false");

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
						from d in SqlToken (".")
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

			Ref<FullSelectStatement> PFullSelectStatementRefST = new Ref<FullSelectStatement> ();

			var PParentsST = PExpressionRefST.Get.InParentsST ();
			var PBracketsST = PExpressionRefST.Get.Contained (Parse.Char ('[').SqlToken (), Parse.Char (']').SqlToken ());

			var PBinaryAdditionOperatorsST = AnyTokenST ("+", "-");
			var PBinaryMultiplicationOperatorsST = AnyTokenST ("/", "*", "%");
			var PBinaryExponentialOperatorsST = AnyTokenST ("^");

			var PBinaryComparisonOperatorsST = AnyTokenST (
				">=", ">", "<=", "<>", "<", "=", "!="
				);

			var PBinaryJsonOperatorsST = AnyTokenST (
				"->>", "->", "#>>", "#>"
			);

			var PBinaryRangeOperatorsST = AnyTokenST (
				"like", "ilike"
			);

			var PBetweenOperatorST = SqlToken ("between");

			var PBinaryGeneralTextOperatorsST = AnyTokenST (
				"||"
			);

			var PBinaryIncludeOperatorsST = AnyTokenST (
				"in", "not in"
			);
			var PBinaryMatchingOperatorsST = AnyTokenST (
				"is"
			);
			var PNullMatchingOperatorsST = AnyTokenST ("isnull", "notnull");

			var PNegationST = AnyTokenST ("not");
			var PBinaryConjunctionST = AnyTokenST ("and");
			var PBinaryDisjunctionST = AnyTokenST ("or");

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
					})
			);

			var PTypeST =
					from t in PTypeTitleST
					from p in Parse.Number.SqlToken ()
						.CommaDelimitedST ()
						.InParentsST ()
						.Optional ()
					from _ps in AnyTokenST ("% rowtype").Optional ()
					from array in
						(
							from _1 in Parse.Char ('[').SqlToken ()
							from _2 in Parse.Char (']').SqlToken ()
							select 1
						)
						.AtLeastOnce ()
						.Optional ()
					select array.IsDefined
						? new KeyedType (t.given_as + "[]", t.key.ArrayType)
						: t
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
					from array_kw in SqlToken ("array")
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
					from kw_groupby in AnyTokenST ("group by")
					from grp in PExpressionRefST.Get.CommaDelimitedST ()
					select 0
				).Optional ()
				;

			var POrderByClauseOptionalST =
				(
					from f in AnyTokenST ("order by")
					from grp in
						(
							from _1 in PExpressionRefST.Get
							from _2 in AnyTokenST ("asc", "desc").Optional ()
							select 0
						)
						.CommaDelimitedST ()
					select 0
				).Optional ()
				;

			var PUnnestST =
					from f in AnyTokenST ("unnest")
					from _1 in AnyTokenST ("(")
					from exp in PExpressionRefST.Get
					from _3 in AnyTokenST (")")
					select (Func<RequestContext, NamedTyped>)(rc =>
						new NamedTyped (f, exp.GetResult (rc).Type.BaseType))
				;

			var PBaseAtomicST =
					PNull.SqlToken ().ProduceType (DatabaseContext.TypeMap.Null)
						.Or (PDecimal.SqlToken ().ProduceType (DatabaseContext.TypeMap.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.SqlToken ().ProduceType (DatabaseContext.TypeMap.Int))
						.Or (PBooleanLiteralST.ProduceType (DatabaseContext.TypeMap.Bool))
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
						from rn in AnyTokenST ("row_number", "rank")
						from _1 in AnyTokenST ("( ) over (")
						from _2 in
						(
							from _3 in AnyTokenST ("partition by")
							from _4 in PExpressionRefST.Get.CommaDelimitedST ()
							select 0
						).Optional ()
						from _5 in POrderByClauseOptionalST
						from _6 in SqlToken (")")
						select (Func<RequestContext, NamedTyped>)
							(rc => new NamedTyped (rn, DatabaseContext.TypeMap.Int))
					)
					.Or (
						from f in AnyTokenST ("sum", "min", "max")
						from _1 in SqlToken ("(")
						from _2 in SqlToken ("distinct").Optional ()
						from exp in PExpressionRefST.Get
						from _3 in SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => exp.GetResult (rc).WithName (f))
					)
					.Or (
						from f in SqlToken ("count")
						from _1 in SqlToken ("(")
						from _2 in SqlToken ("distinct").Optional ()
						from exp in PAsteriskSelectEntryST.Return (0).Or (PExpressionRefST.Get.Return (0))
						from _3 in SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc =>
							new NamedTyped (f, DatabaseContext.TypeMap.BigInt))
					)
					.Or (
						from f in SqlToken ("array_agg")
						from _1 in SqlToken ("(")
						from _2 in SqlToken ("distinct").Optional ()
						from exp in PExpressionRefST.Get
						from _3 in SqlToken (")")
						select (Func<RequestContext, NamedTyped>)(rc => exp.GetResult (rc).ToArray ().WithName (f))
					)
					.Or (PUnnestST)
					.Or (
						from f in SqlToken ("coalesce")
						from _1 in SqlToken ("(")
						from exp in PExpressionRefST.Get
						from _2 in SqlToken (",")
						from subst in PExpressionRefST.Get
						from _3 in SqlToken (")")
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
							from kw in AnyTokenST ("all", "any", "some")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (DatabaseContext.TypeMap.Null)
					)
					.Or (
						(
							from kw in SqlToken ("exists")
							from exp in PExpressionRefST.Get.Return (0)
								.Or (PFullSelectStatementRefST.Get.Return (0))
								.InParentsST ()
							select 0
						).ProduceType (DatabaseContext.TypeMap.Bool)
					)
					.Or (
						from case_c in GetCase (PExpressionRefST.Get)
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
					from _1 in SqlToken (".")
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
							from kw_as in AnyTokenST ("as")
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
					from kw_as in SqlToken ("as").Optional ()
					from id in PTableAliasLST
					where kw_as.IsDefined || excl == null || excl.All (s => s != id)
					select id
				).Optional ()
				;

			var PValuesClauseST =
					from _1 in SqlToken ("values")
					from v in PExpressionRefST.Get
						.Or (SqlToken ("default").Return (new SPolynom ()))
						.CommaDelimitedST ()
						.InParentsST ()
						.CommaDelimitedST ()
					select v.First ()
				;

			var PValuesSourceST =
					from v in PValuesClauseST.InParentsST ()
					from kw_as in SqlToken ("as").Optional ()
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
					from kw_from in SqlToken ("from")
					from t1 in PFromTableExpressionST
					from tail in
						(
							from kw_joinN in AnyTokenST ("join", "inner join", "left join", "right join")
							from tN in PFromTableExpressionST
							from kw_onN in SqlToken ("on")
							from condexpN in PExpressionRefST.Get
							select tN
						)
						.Or (
							from kw_joinN in AnyTokenST ("cross join", ",")
							from tN in PFromTableExpressionST
							select tN
						)
						.Many ()
					select t1.ToTrivialArray ().Concat (tail).ToArray ()
				).Optional ()
				;

			var PWhereClauseOptionalST =
				(
					from kw_where in SqlToken ("where")
					from cond in PExpressionRefST.Get
					select 0
				).Optional ()
				;

			var POrdinarySelectST =
					from kw_select in SqlToken ("select")
					from distinct in
					(
						from _1 in SqlToken ("distinct")
						from _2 in
						(
							from _3 in SqlToken ("on")
							from _4 in PExpressionRefST.Get.InParentsST ()
							select 0
						).Optional ()
						select 0
					).Optional ()
					from list in PSelectListST
					from into_t1 in
					(
						from _1 in SqlToken ("into")
						from _2 in PQualifiedIdentifierLST.CommaDelimitedST ()
						select 0
					).Optional ()
					from from_cl in PFromClauseOptionalST
					from into_t2 in
					(
						from _1 in SqlToken ("into")
						from _2 in PQualifiedIdentifierLST.CommaDelimitedST ()
						select 0
					).Optional ()
					from _w in PWhereClauseOptionalST
					from _g in PGroupByClauseOptionalST
					select new OrdinarySelect (list, from_cl)
				;

			var PSelectST =
					from seq in POrdinarySelectST
						.DelimitedBy (AnyTokenST ("union all", "union", "except", "subtract"))
						.Select (ss => ss.ToArray ())
					from ord in POrderByClauseOptionalST
					from limit in SqlToken ("limit").Then (p => Parse.Number.SqlToken ()).Optional ()
					select new SelectStatement (seq[0].List, seq[0].FromClause.GetOrDefault ())
				;

			var PCteLevelST =
					from name in PColumnNameLST
					from kw_as in SqlToken ("as")
					from select_exp in PSelectST.InParentsST ()
					select new SelectStatement (select_exp, name)
				;

			var PCteTopOptionalST =
				(
					from kw_with in SqlToken ("with")
					from kw_recursive in SqlToken ("recursive").Optional ()
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
					from _1 in AnyTokenST ("insert into")
					from table_name in PQualifiedIdentifierLST
					from _3 in PColumnNameLST.CommaDelimitedST ().AtLeastOnce ()
						.InParentsST ()
						.Optional ()
					from _4 in PValuesClauseST.Return (0)
						.Or (PSelectST.Return (0))
					from conflict in
					(
						from _on in AnyTokenST ("on conflict")
						from trg in PExpressionRefST.Get.InParentsST ().Optional ()
						from _1 in SqlToken ("do")
						from act in SqlToken ("nothing").Return (0)
							.Or (
								from act in AnyTokenST ("update set")
								from _set in
								(
									from col in PQualifiedIdentifierLST
									from eq in SqlToken ("=")
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
						from _1 in SqlToken ("returning")
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
					from _1 in AnyTokenST ("delete from")
					from table_name in PQualifiedIdentifierLST
					from al in PTableAliasClauseOptionalST (null)
					from _3 in PFromClauseOptionalST
					from _4 in PWhereClauseOptionalST
					from returning in
					(
						from _1 in SqlToken ("returning")
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
					from kw_open in SqlToken ("open")
					from name in PColumnNameLST
					from _cm1 in SpracheUtils.AllCommentsST ()
					from kw_for in SqlToken ("for")
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
					from _1 in SqlToken ("loop")
					from ins in PInstructionRefST.Get.AtLeastOnce ()
					from _2 in AnyTokenST ("end loop")
					select ins.SelectMany (e => e).ToArray ()
				;

			var PLoopExST =
					from header in
					(
						(
							from _1 in SqlToken ("while")
							from _2 in PExpressionRefST.Get
							select 0
						)
						.Or
						(
							from _1 in SqlToken ("for")
							from _2 in PColumnNameLST
							from _3 in SqlToken ("in")
							from _4 in SqlToken ("reverse").Optional ()
							from in_c in
								(
									from _5 in PExpressionRefST.Get
									from _6 in SqlToken ("..")
									from _7 in PExpressionRefST.Get
									from _8 in
									(
										from _1 in SqlToken ("by")
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
							from _1 in SqlToken ("foreach")
							from _2 in PColumnNameLST
							from _3 in
							(
								from _4 in SqlToken ("slice")
								from _5 in PExpressionRefST.Get
								select 0
							).Optional ()
							from _6 in AnyTokenST ("in array")
							from _7 in PExpressionRefST.Get
							select 0
						)
					).Optional ()
					from body in PLoopST
					select body
				;

			var PExceptionBlockST =
					from _exc in SqlToken ("exception")
					from _blocks in
					(
						from _when in SqlToken ("when")
						from _cond in PExpressionRefST.Get
						from _then in SqlToken ("then")
						from _inst in PInstructionRefST.Get.AtLeastOnce ()
						select _inst
					).AtLeastOnce ()
					select _blocks
						.SelectMany (b => b)
						.SelectMany (b => b)
						.ToArray ()
				;

			var PBeginEndST =
					from _1 in SqlToken ("begin")
					from inst in PInstructionRefST.Get.Many ()
					from exc in PExceptionBlockST.Optional ()
					from _2 in SqlToken ("end")
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
											from _2 in AnyTokenST (":=", "=")
											from _3 in PExpressionRefST.Get
											select 0
										)
										.Or (PSelectFullST.Return (0))
										.Or (SqlToken ("null").Return (0))
										.Or (PInsertFullST.Return (0))
										.Or (PDeleteFullST.Return (0))
										.Or
										(
											// update
											from cte in PCteTopOptionalST
											from _1 in SqlToken ("update")
											from _2 in PQualifiedIdentifierLST
											from al in PTableAliasClauseOptionalST ("set".ToTrivialArray ())
											from _3 in SqlToken ("set")
											from _4 in
											(
												from _1 in PQualifiedIdentifierLST
												from _2 in SqlToken ("=")
												from _3 in PExpressionRefST.Get
												select 0
											).CommaDelimitedST ().AtLeastOnce ()
											from _5 in PFromClauseOptionalST
											from _6 in PWhereClauseOptionalST
											select 0
										)
										.Or
										(
											from _1 in SqlToken ("return")
											from _2 in PExpressionRefST.Get.Optional ()
											select 0
										)
										.Or
										(
											from _1 in SqlToken ("call")
											from _2 in PQualifiedIdentifierLST
											from _3 in PExpressionRefST.Get.CommaDelimitedST (true).InParentsST ()
											select 0
										)
										.Or
										(
											from _1 in AnyTokenST ("raise exception")
											from _2 in PExpressionRefST.Get
											select 0
										)
										.Or (GetCase (PInstructionRefST.Get).Return (0))
										.Select (n => DataReturnStatement.Void)
									)
							select drs.ToTrivialArray ()
						)
						.Or
						(
							from _1 in SqlToken ("if")
							from _2 in PExpressionRefST.Get
							from _3 in SqlToken ("then")
							from ThenIns in PInstructionRefST.Get.Many ()
							from ElsifC in
							(
								from _1 in AnyTokenST ("elsif", "elseif")
								from _2 in PExpressionRefST.Get
								from _3 in SqlToken ("then")
								from ins in PInstructionRefST.Get.AtLeastOnce ()
								select ins
							).Many ()
							from ElseC in
							(
								from _1 in SqlToken ("else")
								from ins in PInstructionRefST.Get.AtLeastOnce ()
								select ins
							).Optional ()
							from _4 in AnyTokenST ("end if")
							select ThenIns
								.Concat (ElsifC.SelectMany (e => e))
								.ConcatIfNotNull (ElseC.GetOrDefault ())
								.SelectMany (e => e)
								.ToArray ()
						)
						.Or (PBeginEndST)
						.Or (PLoopExST)
					from _ in SqlToken (";")
					select body
				;

			PInstructionRefST.Parser = PInstructionST;

			// procedure
			PProcedureST =
					from declare in
					(
						from _1 in SqlToken ("declare")
						from vars in
						(
							from name in PColumnNameLST
							from type in PTypeST
							from init in
							(
								from _1 in AnyTokenST (":=", "=")
								from _2 in PExpressionRefST.Get
								select 0
							).Optional ()
							from _2 in SqlToken (";")
							select type.key != null
								? new NamedTyped (name, type.key)
								: throw new InvalidOperationException ("Type of variable " + name + " (" + type.given_as + ") is not supported")
						).AtLeastOnce ()
						select vars.ToArray ()
					).Optional ()
					from body in PBeginEndST
					select new SProcedure (declare.GetOrElse (new NamedTyped[0]), body)
				;
		}

		public Module Run ()
		{
			/*
			// DEBUG
			(
				from _1 in PProcedureST
				from _2 in SqlToken ("~")
				select _1
			).Parse (@"
~");
*/

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
					// build word cache
					WordsCache = new Dictionary<int, string> ();
					{
						int Pos = -1;
						int WordStartPos = -1;
						bool IsWord = false;
						bool IsStartedWithDigit = false;
						bool HasUppercase = false;

						foreach (var c in proc.SourceCode.Concat (' '.ToTrivialArray ()))
						{
							++Pos;
							bool IsCapital = c >= 'A' && c <= 'Z';
							bool IsLowercaseOrSymbol = c >= 'a' && c <= 'z' || c == '_';
							bool IsDigit = c >= '0' && c <= '9';

							if (IsCapital || IsLowercaseOrSymbol || IsDigit)
							{
								if (!IsWord)
								{
									IsWord = true;
									WordStartPos = Pos;
									IsStartedWithDigit = IsDigit;
									HasUppercase = false;
								}

								HasUppercase |= IsCapital;
							}
							else
							{
								if (IsWord)
								{
									if (!IsStartedWithDigit)
									{
										string Word = proc.SourceCode[WordStartPos..Pos];
										if (HasUppercase)
										{
											Word = Word.ToLower ();
										}

										WordsCache[WordStartPos] = Word;
									}

									WordStartPos = -1;
									IsWord = false;
									IsStartedWithDigit = false;
									HasUppercase = false;
								}
							}
						}
					}

					//
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

			return ModuleReport;
		}
	}
}
