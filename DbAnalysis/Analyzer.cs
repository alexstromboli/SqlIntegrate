using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Sprache;

using Utils;
using DbAnalysis.Sources;
using DbAnalysis.Datasets;

namespace DbAnalysis
{
	public delegate TResult RcFunc<TResult>(RequestContext rc);
	public delegate Sourced<TResult> RcsFunc<TResult>(RequestContext rc);
	public record FunctionCall(QualifiedName name, NamedTyped[] arg);
	public record SProcedure(NamedTyped[] vars, DataReturnStatement[] body);

	public static class BagUtils
	{
		public static IEnumerable<Sourced<SPolynom>> TestExpressionsInContext (this IEnumerable<Sourced<SPolynom>> Expressions, RequestContext rc)
		{
			foreach (var e in Expressions)
			{
				e.Value.GetResult (rc);
			}

			return Expressions;
		}

		public static Func<RequestContext, IEnumerable<Sourced<SPolynom>>> TestExpressions (this IEnumerable<Sourced<SPolynom>> Expressions)
		{
			return rc => Expressions.TestExpressionsInContext (rc);
		}

		public static Func<RequestContext, T> TestExpressions<T> (this T Input, Func<RequestContext, IEnumerable<Sourced<SPolynom>>> Expressions)
		{
			return rc =>
			{
				Expressions?.Invoke (rc);
				return Input;
			};
		}

		public static Parser<RcFunc<T>> Optional<T> (this Parser<RcFunc<T>> Parser)
		{
			return Parse.Optional (Parser).Select<IOption<RcFunc<T>>, RcFunc<T>> (opt =>
				rc => opt.IsDefined ? opt.Get () (rc) : default);
		}

		/*
		public static Parser<RcsFunc<T>> Optional<T> (this Parser<RcsFunc<T>> Parser)
		{
			return Parse.Optional (Parser).Select<IOption<RcsFunc<T>>, RcsFunc<T>> (opt =>
				rc => opt.IsDefined ? opt.Get () (rc) : default);
		}
		*/

		public static RcFunc<NamedTyped[]> Pack (this IEnumerable<RcFunc<NamedTyped>> Input)
		{
			return rc => Input.Select (arr => arr (rc)).ToArray ();
		}

		public static NamedTyped[] Pack (this IEnumerable<RcFunc<NamedTyped>> Input, RequestContext rc)
		{
			return Input.Pack () (rc);
		}

		public static Parser<RcFunc<NamedTyped[]>> Pack (this Parser<IEnumerable<RcFunc<NamedTyped>>> Parser)
		{
			return Parser.Select (arr => arr.Pack ());
		}
	}

	public class Analyzer
	{
		protected DatabaseContext DatabaseContext;
		protected Dictionary<int, Sourced<string>> WordsCache;
		protected Parser<Sourced<string>> PDoubleQuotedString;
		protected Ref<Sourced<SPolynom>> PExpressionRefST;

		protected Parser<RcFunc<NamedTyped>> PExpressionST =>
			PExpressionRefST.Get.Select<Sourced<SPolynom>, RcFunc<NamedTyped>> (p => rc => p.Value.GetResult (rc));
		public Parser<SProcedure> PProcedureST { get; }

		public static RcFunc<T> FromContext<T> (RcFunc<T> Proc)
		{
			return Proc;
		}

		protected Parser<Sourced<string>> PAlphaNumericL
		{
			get
			{
				return i =>
				{
					if (WordsCache.TryGetValue (i.Position, out Sourced<string> Word))
					{
						return Result.Success (Word,
							new CustomInput (i.Source, i.Position + Word.Value.Length, i.Line,
								i.Column + Word.Value.Length));
					}

					return Result.Failure<Sourced<string>> (i, "No word found", Array.Empty<string> ());
				};
			}
		}

		public Parser<Sourced<string>> SqlToken (string LineL)
		{
			if (LineL.All (c => char.IsLetterOrDigit (c) || c == '_'))
			{
				// to prevent cases like taking 'order' for 'or'
				// take all the letters, and then check
				return PAlphaNumericL
						.Where (s => s.Value == LineL)
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
		public Parser<Sourced<string>> AnyTokenST (params string[] Options)
		{
			Parser<Sourced<string>> Result = null;
			foreach (string[] Tokens in Options.Select (s => s.Split (' ', StringSplitOptions.RemoveEmptyEntries)))
			{
				Parser<Sourced<string>> Line = null;
				foreach (string Token in Tokens)
				{
					var PT = SqlToken (Token);
					Line = Line == null
							? PT
							: (from f in Line
								from n in PT
								select (f + " " + n).SourcedTextSpan (TextSpan.Range (f.TextSpan, n.TextSpan))
							)
						;
				}

				Result = Result == null
						? Line
						: Result.Or (Line)
					;
			}

			return Result;
		}

		protected Parser<RcFunc<CaseBase<T>>> GetCase<T> (Parser<T> Then)
		{
			return
				from case_h in SqlToken ("case")
				from sample in PExpressionST.Optional ()
				from branches in
				(
					from _1 in SqlToken ("when")
					from cond in PExpressionST.CommaDelimitedST ()
					from _3 in SqlToken ("then")
					from value in Then.SpanSourced ()
					select new { cond, value }
				).AtLeastOnce ()
				from else_c in
				(
					from _1 in SqlToken ("else")
					from value in PExpressionST
					select value
				).Optional ()
				from _3 in AnyTokenST ("end case", "end")
				select FromContext (rc => new CaseBase<T> (
					case_h,
					sample (rc),
					branches.Select (b => b.cond).SelectMany (t => t).Select (t => t (rc)).ToArray (),
					branches.Select (b => b.value).ToArray (),
					else_c (rc)))
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
					.Or (Parse.Chars ('[', ']', '.').Select (c => c.ToString ()).SpanSourced ())
					.SqlToken ()
				;

			return i =>
			{
				var WordResult = FindWordST (i);
				if (WordResult.WasSuccessful
				    && !i.Equals (WordResult.Remainder)
				    && Map.TryGetValue (WordResult.Value.Value, out var Next)
				   )
				{
					return Next (WordResult.Remainder);
				}

				return End (i);
			};
		}

		// immediate, i.e. no comments or whitespace
		protected Parser<Sourced<string>> ReadKeywordL (params string[] ValuesL)
		{
			return PAlphaNumericL
					.Where (r => ValuesL.Any (v => v == r.Value))
				;
		}

		public Analyzer (DatabaseContext DatabaseContext)
		{
			this.DatabaseContext = DatabaseContext;
			PExpressionRefST = new Ref<Sourced<SPolynom>> ();

			// https://www.postgresql.org/docs/12/sql-syntax-lexical.html
			PDoubleQuotedString =
					from _1 in Parse.Char ('"')
					from s in Parse.CharExcept ('"')
						//.Or (Parse.Char ('\\').Then (c => Parse.AnyChar))
						.Or (Parse.String ("\"\"").Return ('"'))
						.Many ()
						.Text ()
						.SpanSourced ()
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
				(
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
				).SpanSourced ()
				;

			var PBooleanLiteralST = AnyTokenST ("true", "false");

			// valid for column name
			var PColumnNameLST = PAlphaNumericL
					.Where (n => n.Value.NotROrT ())
					.Or (PDoubleQuotedString.ToLower ())
					.SqlToken ()
				;

			// valid for schema name
			//var PSchemaNameLST = PColumnNameLST;

			var PDirectColumnAliasLST = PAlphaNumericL
					.Where (n => !n.Value.IsKeyword ())
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
					select new QualifiedName (k1.ToTrivialArray ().Concat (kn).ToArray ())
				;

			var PSignPrefix =
					from c in Parse.Chars ('+', '-').SpanSourced ()
					from sp in Parse.Chars ('+', '-').Or (Parse.WhiteSpace).Many ().Text ().SpanSourced ()
					let res = c.Value + sp.Value
					where !res.Contains ("--")
					select res;

			Ref<FullSelectStatement> PFullSelectStatementRefST = new Ref<FullSelectStatement> ();

			//var PParentsST = PExpressionST.InParentsST ();
			//var PBracketsST = PExpressionST.InBracketsST ();

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
				(
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
				).SpanSourced ()
				;

			var PSimpleTypeCastST =
					from op in Parse.String ("::").SqlToken ()
					from t in PTypeST
					select t
				;

			var PSelectFirstColumnST = PFullSelectStatementRefST.Get.InParentsST ()
				.Select<FullSelectStatement, RcFunc<NamedTyped>> (fss => rc =>
					fss.GetTable (rc, false).Columns[0]);

			var PArrayST =
				from array_kw in SqlToken ("array")
				from body in PExpressionST.CommaDelimitedST ().InBracketsST ().Pack ()
					.Select (c => FromContext (rc => c(rc)[0]))
					.Or (PSelectFirstColumnST)
				select FromContext (rc => body (rc).ToArray ().WithName (array_kw));

			var PFunctionCallST =
					from name in PQualifiedIdentifierLST
					from arg in PExpressionST
						.CommaDelimitedST (true)
						.InParentsST ()
					select FromContext (rc => new FunctionCall (name, arg.Pack (rc)))
				;

			//
			var PAsteriskSelectEntryST =
					from qual in
					(
						from qual in PQualifiedIdentifierLST
						from dot in Parse.Char ('.').SqlToken ()
						select qual
					).Optional ()
					from ast in SqlToken ("*")
					select FromContext (rc =>
					{
						var Qualifier = qual.GetOrDefault ()?.Get (rc, 2) ?? Array.Empty<Sourced<string>> ();

						return rc.GetAsterisk (
							(qual.IsDefined
								? Qualifier.JoinDot () + ".*"
								: "*"
							).SourcedTextSpan (Qualifier.Concat (ast.ToTrivialArray ()).Range ())
						);
					})
				;

			var PGroupByClauseOptionalST =
				(
					from kw_groupby in AnyTokenST ("group by")
					from grp in PExpressionST.CommaDelimitedST ()
					select grp.Pack ()
				).Optional ()
				;

			var PHavingClauseOptionalST =
				(
					from kw_groupby in SqlToken ("having")
					from cond in PExpressionST
					select cond
				).Optional ()
				;

			var POrderByClauseOptionalST =
				(
					from f in AnyTokenST ("order by")
					from grp in
						(
							from exp in PExpressionST/*exp*/
							from _2 in AnyTokenST ("asc", "desc").Optional ()
							select exp
						)
						.CommaDelimitedST ()
					select grp.Pack ()
				).Optional ()
				;

			var PUnnestST =
					from f in SqlToken ("unnest")
					from _1 in SqlToken ("(")
					from exp in PExpressionST
					from _3 in SqlToken (")")
					select FromContext (rc =>
						new NamedTyped (f, exp (rc).Type.Select (t => t.BaseType)))
				;

			var PBaseAtomicST =
					PNull.SqlToken ().ProduceType (DatabaseContext.TypeMap.Null)
						.Or (PDecimal.SqlToken ().ProduceType (DatabaseContext.TypeMap.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.SqlToken ().ProduceType (DatabaseContext.TypeMap.Int))
						.Or (PBooleanLiteralST.ProduceType (DatabaseContext.TypeMap.Bool))
						.Or (PSingleQuotedString.SqlToken ().ProduceType (DatabaseContext.TypeMap.VarChar))
						.Or (PExpressionST.InParentsST ())
						.Or (PFunctionCallST.Select<RcFunc<FunctionCall>, RcFunc<NamedTyped>> (p => rc =>
						{
							var Function = p (rc).name.Get (rc, 2);
							return rc.ModuleContext.GetFunction (Function);
						}))
						// PQualifiedIdentifier must be or-ed after PFunctionCall
						.Or (PQualifiedIdentifierLST
							.Select<QualifiedName, RcFunc<NamedTyped>> (p => rc =>
							{
								string Key = p.Get (rc, 3).JoinDot ();
								return rc.NamedDict.TryGetValue (Key, out var V)
									? V
									: throw new KeyNotFoundException ("Not found " + Key);
							}))
						.Or (PSelectFirstColumnST)
				;

			var PAtomicST =
				(
					from rn in AnyTokenST ("row_number", "rank")
					from _1 in AnyTokenST ("( ) over (")
					from part in
					(
						from _3 in AnyTokenST ("partition by")
						from exp in PExpressionST.CommaDelimitedST ()
						select exp.Pack ()
					).Optional () /*exp*/
					from order_by in POrderByClauseOptionalST /*exp*/
					from _6 in SqlToken (")")
					select FromContext
						(rc => new NamedTyped (rn, DatabaseContext.TypeMap.Int.SourcedCalculated (rn)))
				)
				.Or (
					from f in AnyTokenST ("sum", "min", "max", "avg")
					from _1 in SqlToken ("(")
					from _2 in SqlToken ("distinct").Optional ()
					from exp in PExpressionST
					from _3 in SqlToken (")")
					select FromContext (rc => exp (rc).WithName (f))
				)
				.Or (
					from f in SqlToken ("count")
					from _1 in SqlToken ("(")
					from _2 in SqlToken ("distinct").Optional ()
					from exp in PAsteriskSelectEntryST /*exp*/.Return (0).Or (PExpressionST /*exp*/.Return (0))
					from _3 in SqlToken (")")
					select FromContext (rc =>
						new NamedTyped (f, DatabaseContext.TypeMap.BigInt.SourcedCalculated (f)))
				)
				.Or (
					from f in SqlToken ("array_agg")
					from _1 in SqlToken ("(")
					from _2 in SqlToken ("distinct").Optional ()
					from exp in PExpressionST
					from _3 in SqlToken (")")
					select FromContext (rc => exp (rc).ToArray ().WithName (f))
				)
				.Or (PUnnestST)
				.Or (
					from f in SqlToken ("coalesce")
					from _1 in SqlToken ("(")
					from exp in PExpressionST
					from _2 in SqlToken (",")
					from subst in PExpressionST /*exp*/
					from _3 in SqlToken (")")
					select FromContext (rc =>
					{
						var ExpRes = exp (rc);
						return (ExpRes.Type.Value == DatabaseContext.TypeMap.Null ? subst (rc) : ExpRes)
							.WithName (f);
					})
				)
				.Or (
					// value specified by a keyword
					from kw in PAlphaNumericL.SqlToken ()
					let type = kw.Value.GetExpressionType ()
					where type != null
					select FromContext (rc =>
						new NamedTyped (kw, DatabaseContext.GetTypeForName ("pg_catalog", type).SourcedCalculated (kw)))
				)
				.Or (
					(
						from kw in AnyTokenST ("all", "any", "some")
						from exp in PExpressionST /*exp*/.Return (0)
							.Or (PFullSelectStatementRefST.Get.Return (0))
							.InParentsST ()
						select 0
					).ProduceType (DatabaseContext.TypeMap.Null)
				)
				.Or (
					(
						from kw in SqlToken ("exists")
						from exp in PExpressionST /*exp*/.Return (0)
							.Or (PFullSelectStatementRefST.Get.Return (0))
							.InParentsST ()
						select 0
					).ProduceType (DatabaseContext.TypeMap.Bool)
				)
				.Or (
					from case_c in GetCase (PExpressionST /*exp*/) /*exp*/
					select FromContext (rc =>
					{
						var c = case_c (rc);
						return new NamedTyped (
							c.ElseC?.Name ?? c.CaseH,
							c.Branches.First ().Value (rc).Type
						);
					})
					)
					.Or (
						(
							from kw_ext in SqlToken ("extract")
							from p in (
								from kw_part in AnyTokenST ("century", "day", "decade", "dow", "doy", "epoch", "hour",
									"isodow", "isoyear", "microseconds", "millennium", "milliseconds", "minute",
									"month", "quarter", "second", "timezone", "timezone_hour", "timezone_minute",
									"week", "year")
								from _from in SqlToken ("from")
								from exp in PExpressionST/*exp*/
								select 0
							).InParentsST ()
							select (Func<RequestContext, NamedTyped>)(rc =>
								new NamedTyped (kw_ext, DatabaseContext.TypeMap.Decimal.SourcedCalculated (kw_ext)))
						)
					)
					.Or (PArrayST)
					.Or (PExpressionST/*exp*/.CommaDelimitedST ().InParentsST () // ('one', 'two', 'three')
						.Where (r => r.Count () > 1)
						.ProduceType (DatabaseContext.TypeMap.Record))
					.Or ( // interval '90 days'
						(
							from t in PTypeST
							from v in PSingleQuotedString.SqlToken ()
							select t.Value.key
						).SpanSourced ().ProduceType () // here: get default column name
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
					PExpressionST/*exp*/.InBracketsST ().Select (b => new OperatorProcessor (PSqlOperatorPriority.None, false,
							(l, r) => rc =>
							{
								var NamedTyped = l (rc);
								return NamedTyped.WithType (NamedTyped.Type.Select (t => t.BaseType));
							}))
						.Or (PSimpleTypeCastST.Select (tc => new OperatorProcessor (PSqlOperatorPriority.Typecast,
							false,
							(l, r) => rc => l (rc).WithType (tc.Select (t => t.key)))))
						.Or (PNullMatchingOperatorsST.Select (m => new OperatorProcessor (PSqlOperatorPriority.Is,
							false,
							(l, r) => rc => new NamedTyped (DatabaseContext.TypeMap.Bool.SourcedCalculated (m)))))
						.Or (PTakePropertyST.Select (prop => new OperatorProcessor (PSqlOperatorPriority.Unary, false,
							(l, r) => rc =>
							{
								var Parent = l (rc);
								var CompositeType = Parent.Type.Value;

								if (CompositeType.PropertiesDict == null ||
								    !CompositeType.PropertiesDict.TryGetValue (prop.Value, out var Property))
								{
									rc.ReportError (
										$"Line {prop?.TextSpan?.Start.Line}, column {prop?.TextSpan?.Start.Column}, type {CompositeType} does not have property {prop?.Value ?? "???"}");
									throw new InvalidOperationException ();
								}

								return new NamedTyped (prop,
									Property.Type.SourcedCompositeType (CompositeType.Schema, CompositeType.OwnName,
										prop.Value));
							})))
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
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryIncludeOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.In, true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryRangeOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Like,
							true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryMatchingOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Is,
							true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryConjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.And, true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool), IsAnd: true)))
						.Or (PBinaryDisjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Or, true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool))))
						.Or (PBinaryGeneralTextOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.General, true,
							OperatorProcessor.GetForBinaryOperator (DatabaseContext.TypeMap, b))))
						.Or (PBetweenOperatorST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Between, true,
							OperatorProcessor.ProduceType (b, DatabaseContext.TypeMap.Bool), true)))
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
						Operands = new SPolynom.Operand { Atomic = at1, Postfixes = post1.GetOrEmpty () }
							.ToTrivialArray ()
							.Concat (rest.Select (e => new SPolynom.Operand
								{ Atomic = e.atN, Postfixes = e.postN.GetOrEmpty () }))
							.ToList ()
					}
				;

			PExpressionRefST.Parser = PPolynomST.SpanSourced ();

			//
			var PSingleSelectEntryST =
					from exp in PExpressionST
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
								var nt = exp.Value.GetResult (rc);
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

			Func<IEnumerable<string>, Parser<IOption<Sourced<string>>>> PTableAliasClauseOptionalST = excl =>
				(
					from kw_as in SqlToken ("as").Optional ()
					from id in PTableAliasLST
					where kw_as.IsDefined || excl == null || excl.All (s => s != id.Value)
					select id
				).Optional ()
				;

			var PValuesClauseST =
					from _1 in SqlToken ("values")
					from v in PExpressionST/*exp*/
						.Or (SqlToken ("default").Return (new SPolynom ().SourcedUnknown ()))
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
						column_names.GetOrElse (Array.Empty<Sourced<string>> ()).ToArray ())
				;

			var PFromTableExpressionST =
					from table in
					(
						PUnnestST.Select<Func<RequestContext, NamedTyped>, Func<RequestContext, ITableRetriever>> (p =>
								rc => new UnnestTableRetriever (p))
							// or-ed after unnest
							.Or (PFunctionCallST.Select<FunctionCall, Func<RequestContext, ITableRetriever>> (qi => rc =>
								{
									qi.arg.TestExpressionsInContext (rc);
									return new NamedTableRetriever (qi.name.Get (rc, 2).Values ());
								}
								// stub
							))
							// or-ed after function calls
							.Or (PQualifiedIdentifierLST.Select<QualifiedName, Func<RequestContext, ITableRetriever>> (qi => rc => new NamedTableRetriever (qi.Get (rc, 2).Values ())))
							.Or (PFullSelectStatementRefST.Get.InParentsST ()
								.Select<FullSelectStatement, Func<RequestContext, ITableRetriever>> (t => rc => t)
							)
							.Or (PValuesSourceST.Select<ValuesBlock, Func<RequestContext, ITableRetriever>> (t => rc => t))
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
							from condexpN in PExpressionST/*exp*/
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
					from cond in PExpressionST/*exp*/
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
							from _4 in PExpressionST /*exp*/.InParentsST ()
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
					from grp in PGroupByClauseOptionalST
					from _h in PHavingClauseOptionalST/*exp*/
					select new OrdinarySelect (list, from_cl).TestExpressions (grp.GetOrDefault ())
				;

			var PSelectST =
					from seq in POrdinarySelectST/*exp*/
						.DelimitedBy (AnyTokenST ("union all", "union", "except", "subtract"))
						.Select (ss => ss.ToArray ())
					from ord in POrderByClauseOptionalST
					from limit in
					(
						from kw in SqlToken ("limit")
						from size in PExpressionST/*exp*/.Return (0)
							.Or (SqlToken ("limit").Return (0))
						select 0
					).Optional ()
					from offset in
					(
						from kw in SqlToken ("offset")
						from size in PExpressionST/*exp*/
						select 0
					).Optional ()
					select BagUtils.FromContext (rc =>
					{
						var Select = seq[0] (rc);
						return new SelectStatement (Select.List, Select.FromClause.GetOrDefault ());
					}, null)
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
						from trg in PExpressionST/*exp*/.InParentsST ().Optional ()
						from _1 in SqlToken ("do")
						from act in SqlToken ("nothing").Return (0)
							.Or (
								from act in AnyTokenST ("update set")
								from _set in
								(
									from col in PQualifiedIdentifierLST
									from eq in SqlToken ("=")
									from val in PExpressionST/*exp*/
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
								new FromTableExpression (rc => new NamedTableRetriever (table_name.Get (rc, 2).Values ()), null).ToTrivialArray ()))
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
								new FromTableExpression (rc => new NamedTableRetriever (table_name.Get (rc, 2).Values ()), null).ToTrivialArray ()))
						: null
				;

			var POpenDatasetST =
					from kw_open in SqlToken ("open")
					from name in PColumnNameLST
					from _cm1 in SpracheUtils.AllCommentsST ()
					from kw_for in SqlToken ("for")
					from _cm2 in SpracheUtils.AllCommentsST ()
					select new OpenDataset (name.Value, _cm2.ToArray ())
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
							from _2 in PExpressionST/*exp*/
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
									from _5 in PExpressionST/*exp*/
									from _6 in SqlToken ("..")
									from _7 in PExpressionST/*exp*/
									from _8 in
									(
										from _1 in SqlToken ("by")
										from _2 in PExpressionST/*exp*/
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
								from _5 in PExpressionST/*exp*/
								select 0
							).Optional ()
							from _6 in AnyTokenST ("in array")
							from _7 in PExpressionST/*exp*/
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
						from _stat in SqlToken ("sqlstate").Optional ()
						from _cond in PExpressionST/*exp*/
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
											from _3 in PExpressionST/*exp*/
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
												from _3 in PExpressionST/*exp*/
												select 0
											).CommaDelimitedST ().AtLeastOnce ()
											from _5 in PFromClauseOptionalST
											from _6 in PWhereClauseOptionalST
											select 0
										)
										.Or (
											// https://www.postgresql.org/docs/current/plpgsql-errors-and-messages.html
											from _r in SqlToken ("raise")
											from _l in AnyTokenST ("debug", "log", "info", "notice", "warning",
												"exception").Optional ()
											from _p in PExpressionST/*exp*/.CommaDelimitedST (true)
											from _u in
											(
												from kw in SqlToken ("using")
												from opts in
												(
													from _n in PAlphaNumericOrQuotedLST
													from _eq in SqlToken ("=")
													from _p in PExpressionST/*exp*/
													select 0
												).CommaDelimitedST ()
												select 0
											).Optional ()
											select 0
										)
										.Or
										(
											from _1 in SqlToken ("return")
											from _2 in PExpressionST/*exp*/.Optional ()
											select 0
										)
										.Or
										(
											from _1 in SqlToken ("call")
											from _2 in PQualifiedIdentifierLST
											from _3 in PExpressionST/*exp*/.CommaDelimitedST (true).InParentsST ()
											select 0
										)
										.Or (GetCase (PInstructionRefST.Get)/*exp*/.Return (0))
										.Select (n => DataReturnStatement.Void)
									)
							select drs.ToTrivialArray ()
						)
						.Or
						(
							from _1 in SqlToken ("if")
							from _2 in PExpressionST/*exp*/
							from _3 in SqlToken ("then")
							from ThenIns in PInstructionRefST.Get.Many ()
							from ElsifC in
							(
								from _1 in AnyTokenST ("elsif", "elseif")
								from _2 in PExpressionST/*exp*/
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
								from _2 in PExpressionST/*exp*/
								select 0
							).Optional ()
							from _2 in SqlToken (";")
							select type.Value.key != null
								? new NamedTyped (name, type.Select (t => t.key))
								: throw new InvalidOperationException ("Type of variable " + name + " (" + type.Value.given_as + ") is not supported")
						).Many ()
						select vars.ToArray ()
					).Optional ()
					from body in PBeginEndST
					select new SProcedure (declare.GetOrEmpty (), body)
				;
		}

		protected void BuildWordCache (string SourceCode)
		{
			WordsCache = new Dictionary<int, Sourced<string>> ();

			int Pos = -1;
			int WordStartPos = -1;
			bool IsWord = false;
			bool IsStartedWithDigit = false;
			bool HasUppercase = false;

			foreach (var c in SourceCode.Concat (' '.ToTrivialArray ()))
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
							string Word = SourceCode[WordStartPos..Pos];
							if (HasUppercase)
							{
								Word = Word.ToLower ();
							}

							WordsCache[WordStartPos] = Word.SourcedTextSpan (new TextSpan
							{
								Start = Position.FromInput (i),
								End = new Position (i.Position + Word.Length, i.Line, i.Column + Word.Length),
								Length = Word.Length
							});
						}

						WordStartPos = -1;
						IsWord = false;
						IsStartedWithDigit = false;
						HasUppercase = false;
					}
				}
			}
		}

		public Module Run ()
		{
			bool IsDebugging = false;
			if (IsDebugging)
			{
				// DEBUG
				//string TestProc = @"BEGIN END";
				while (true)
				{
					string TestProc = System.IO.File.ReadAllText ("debug_procedure.sql").Trim ().TrimEnd (';');

					if (string.IsNullOrWhiteSpace (TestProc))
					{
						break;
					}

					try
					{
						BuildWordCache (TestProc);
						(
							from _1 in PProcedureST
							from _2 in SqlToken ("~")
							select _1
						).Parse (TestProc + "~");

						break;
					}
					catch (Exception)
					{
						Debugger.Break ();
					}
				}
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
							Name = a.Name.Value,
							Type = a.Type.Value.ToString (),
							PSqlType = a.Type.Value,
							IsOut = a.Direction == Argument.DirectionType.InOut
						}).ToList (),
					ResultSets = new List<ResultSet> ()
				};

				try
				{
					// build word cache
					BuildWordCache (proc.SourceCode);

					//
					var Parse = PProcedureST.Parse (proc.SourceCode);

					var VarNames = Parse.vars.ToDictionary (v => v.Name.Value);
					ModuleContext mcProc = new ModuleContext (
						proc.Name,
						DatabaseContext,
						Parse.vars
							.Concat (proc.Arguments
								// here: issue warning for duplicate names
								.Where (a => !VarNames.ContainsKey (a.Name.Value))
							)
							.ToDictionary (v => v.Name.Value)
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
									Name = c.Name.Value,
									Type = c.Type.Value.ToString (),
									PSqlType = c.Type.Value
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
