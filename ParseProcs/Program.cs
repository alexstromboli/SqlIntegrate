using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
	public interface IRequestContext
	{
		NamedTyped GetFunction (string[] FunctionName);
		NamedTyped GetScalar (string[] ScalarName);
		IReadOnlyList<NamedTyped> GetAsterisk (string[] Qualifier);
	}

	public class ModuleContext
	{
		public string ModuleName { get; protected set; }
		public string DefaultSchemaName { get; protected set; }

		protected List<Table> _Tables;
		public IReadOnlyList<Table> Tables => _Tables;

		protected List<NamedTyped> _Variables;
		public IReadOnlyList<NamedTyped> Variables => _Variables;

		public ModuleContext (string ModuleName, string DefaultSchemaName,
			IEnumerable<Table> Tables,
			IEnumerable<NamedTyped> Variables
			)
		{
			this.ModuleName = ModuleName.ToLower ();
			this.DefaultSchemaName = DefaultSchemaName.ToLower ();
			_Tables = new List<Table> (Tables);
			_Variables = new List<NamedTyped> (Variables);
		}
	}

	public class RequestContext : IRequestContext
	{
		protected ModuleContext ModuleContext;

		public RequestContext (ModuleContext ModuleContext)
		{
			this.ModuleContext = ModuleContext;
		}

		public NamedTyped GetFunction (string[] FunctionName)
		{
			throw new NotImplementedException ();
		}

		public NamedTyped GetScalar (string[] ScalarName)
		{
			throw new NotImplementedException ();
		}

		public IReadOnlyList<NamedTyped> GetAsterisk (string[] Qualifier)
		{
			throw new NotImplementedException ();
		}
	}

	/*
	public abstract class TableExpression
	{
		public abstract IReadOnlyList<string> NameFragments { get; }
		public abstract IReadOnlyDictionary<string, NamedTyped> ColumnsDict { get; }
	}

	public class DbTableExpression : TableExpression
	{
		protected Table Table;
		protected string[] NameFragmentsImpl;

		public override IReadOnlyList<string> NameFragments => NameFragmentsImpl;
		public override IReadOnlyDictionary<string, NamedTyped> ColumnsDict => Table.ColumnsDict;

		public DbTableExpression (Table Table, string[] NameFragmentsImpl)
		{
			this.Table = Table;
			this.NameFragmentsImpl = NameFragmentsImpl;
		}
	}

	public interface IOutputNamedTypedColumns
	{
		IReadOnlyList<NamedTyped> Get (RequestContext Context);
	}
	*/

	public class SProcedure
	{
		public List<SVarDeclaration> Variables;
		public List<SInstruction> Instructions;
	}

	public class SInstruction
	{
	}

	public class SVarDeclaration
	{
	}

	partial class Program
	{
		static void Main (string[] args)
		{
			string ConnectionString = "server=127.0.0.1;port=5432;database=dummy01;uid=postgres;pwd=Yakunichev";

			Dictionary<string, Table> TablesDict = new Dictionary<string, Table> ();
			Dictionary<string, Procedure> ProceduresDict = new Dictionary<string, Procedure> ();

			ReadDatabase (ConnectionString, TablesDict, ProceduresDict);

			//

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
			var PBasicIdentifier =
					from n in Parse.Char (c => char.IsLetterOrDigit (c) || c == '_', "").AtLeastOnce ()
					where !char.IsDigit (n.First ())
					select new string (n.ToArray ())
				;

			// any id readable without quotes, except keywords
			var PBasicValidIdentifier =
					PBasicIdentifier
						.Where (n => Keywords.CanBeIdentifier (n))
				;

			// any id readable without quotes, except keywords,
			// or in quotes
			var PValidIdentifierEx = PBasicValidIdentifier
					.Or (PDoubleQuotedString)
				;

			// any id readable without quotes, including keywords
			// (which is valid when identifier is expected, like after AS),
			// or in quotes
			var PExpectedIdentifierEx = PBasicIdentifier
					.Or (PDoubleQuotedString)
				;

			// here: allow keywords after dot, like R.order
			var PQualifiedIdentifierST = PValidIdentifierEx
					.SqlToken ()
					.DelimitedBy (Parse.String (".").SqlToken (), 1, null)
					.Select (seq => seq.ToArray ())
				;

			var PSignPrefix =
					from c in Parse.Chars ('+', '-')
					from sp in Parse.Chars ('+', '-').Or (Parse.WhiteSpace).Many ().Text ()
					let res = c + sp
					where !res.Contains ("--")
					select res
				;

			Ref<SPolynom> PExpressionRefST = new Ref<SPolynom> ();

			var PParentsST = PExpressionRefST.Get.InParentsST ();
			var PBracketsST = PExpressionRefST.Get.Contained (Parse.Char ('[').SqlToken (), Parse.Char (']').SqlToken ());

			var PBinaryAdditionOperatorsST = SpracheUtils.AnyTokenST ("+", "-");
			var PBinaryMultiplicationOperatorsST = SpracheUtils.AnyTokenST ("/", "*", "%");
			var PBinaryExponentialOperatorsST = SpracheUtils.AnyTokenST ("^");

			var PBinaryComparisonOperatorsST = SpracheUtils.AnyTokenST (
				">=", ">", "<=", "<>", "<", "=", "!="
				);

			var PBinaryRangeOperatorsST = SpracheUtils.AnyTokenST (
				"like"
			);

			var PBinaryGeneralTextOperatorsST = SpracheUtils.AnyTokenST (
				"||"
			);

			var PBinaryMatchingOperatorsST = SpracheUtils.AnyTokenST (
				"is"
			);
			var PNullMatchingOperatorsST = SpracheUtils.AnyTokenST ("isnull", "not null");

			var PNegationST = SpracheUtils.AnyTokenST ("not");
			var PBinaryConjunctionST = SpracheUtils.AnyTokenST ("and");
			var PBinaryDisjunctionST = SpracheUtils.AnyTokenST ("or");

			var PTypeST =
					from t in SpracheUtils.AnyTokenST (PSqlType.Map.Keys.OrderByDescending (k => k.Length).ToArray ())
					from p in Parse.Number.SqlToken ()
						.CommaDelimitedST ()
						.InParentsST ()
						.Optional ()
					select t
				;

			var PSimpleTypeCastST =
					from op in Parse.String ("::").SqlToken ()
					from t in PTypeST
					select t
				;

			var PFunctionCallST =
					from n in PQualifiedIdentifierST.SqlToken ()
					from arg in PExpressionRefST.Get
						.CommaDelimitedST ()
						.InParentsST ()
						.SqlToken ()
					select n
				;

			//
			var PAtomicST =
					PNull.SqlToken ().ProduceType (PSqlType.Null)
						.Or (PDecimal.SqlToken ().ProduceType (PSqlType.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.SqlToken ().ProduceType (PSqlType.Int))
						.Or (PBooleanLiteral.SqlToken ().ProduceType (PSqlType.Bool))
						.Or (PSingleQuotedString.SqlToken ().ProduceType (PSqlType.Text))
						.Or (PParentsST.Select<SPolynom, Func<IRequestContext, NamedTyped>> (p =>
							rc => p.GetResultType (rc)))
						.Or (PFunctionCallST.Select<string[], Func<IRequestContext, NamedTyped>> (p => rc =>
							rc.GetFunction (p)
							))
						// PQualifiedIdentifier must be or-ed after PFunctionCall
						.Or (PQualifiedIdentifierST.Select<string[], Func<IRequestContext, NamedTyped>> (p => rc =>
							rc.GetScalar (p)
							))
				;

			var PAtomicPrefixGroupOptionalST =
					PSignPrefix.SqlToken ()
						.Or (PNegationST)
						.Many ()
						.Optional ()
				;

			var PAtomicPostfixOptionalST =
					PBracketsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.None, false,
							(l, r) => rc => new NamedTyped (l (rc).Name, null // type inference not implemented yet
							)))
						.Or (PSimpleTypeCastST.Select (tc => new OperatorProcessor (PSqlOperatorPriority.Typecast, false,
							(l, r) => rc => new NamedTyped (l (rc).Name, PSqlType.Map[tc]))))
						.Or (PNullMatchingOperatorsST.Select (m => new OperatorProcessor (PSqlOperatorPriority.Is, false,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Many ()
						.Optional ()
				;

			var PBinaryOperatorsST =
					PBinaryMultiplicationOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.MulDiv,
							true,
							OperatorProcessor.GetForBinaryOperator (b)))
						.Or (PBinaryAdditionOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.AddSub,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryExponentialOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Exp,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryComparisonOperatorsST.Select (b => new OperatorProcessor (
							PSqlOperatorPriority.Comparison, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryRangeOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Like, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryMatchingOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Is, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryConjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.And, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryDisjunctionST.Select (b => new OperatorProcessor (PSqlOperatorPriority.Or, true,
							(l, r) => rc => new NamedTyped (PSqlType.Bool))))
						.Or (PBinaryGeneralTextOperatorsST.Select (b => new OperatorProcessor (PSqlOperatorPriority.General, true,
							(l, r) => rc => new NamedTyped (PSqlType.Text))))
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
						Operands = new[] { new SPolynom.Operand { Atomic = at1, Postfixes = post1 } }
							.Concat (rest.Select (e => new SPolynom.Operand { Atomic = e.atN, Postfixes = e.postN }))
							.ToList ()
					}
				;

			PExpressionRefST.Parser = PPolynomST;

			//
			var PAsteriskSelectEntryST =
					from qual in
					(
						from qual in PQualifiedIdentifierST
						from dot in Parse.Char ('.').SqlToken ()
						select qual
					).Optional ()
					from ast in Parse.Char ('*').SqlToken ()
					select (Func<IRequestContext, IReadOnlyList<NamedTyped>>)(rc => rc.GetAsterisk (qual.GetOrElse (new string[0])))
				;
			var PSingleSelectEntryST =
					from exp in PExpressionRefST.Get
					from alias_cl in
						(
							from kw_as in SpracheUtils.AnyTokenST ("as")
							from id in PExpectedIdentifierEx.SqlToken ()
							select id
						)
						.Or
						(
							PValidIdentifierEx.SqlToken ()
						).Optional ()
					select (Func<IRequestContext, IReadOnlyList<NamedTyped>>)(rc =>
							{
								var nt = exp.GetResultType (rc);
								var res = alias_cl.IsDefined
									? new NamedTyped (alias_cl.Get (), nt.Type)
									: nt;

								return new[] {res};
							}
						)
				;

			var PSelectListST =
					PAsteriskSelectEntryST
						.Or (PSingleSelectEntryST)
						.CommaDelimitedST ()
						.Select<IEnumerable<Func<IRequestContext, IReadOnlyList<NamedTyped>>>, Func<IRequestContext, IReadOnlyList<NamedTyped>>> (
							list => rc => list
								.SelectMany (e => e (rc))
								.ToArray ()
							)
				;

			var PFromTableExpressionST =
				from table in PQualifiedIdentifierST
				from alias_cl in
					(
						from kw_as in SpracheUtils.AnyTokenST ("as")
						from id in PExpectedIdentifierEx.SqlToken ()
						select id
					)
					.Or
					(
						PValidIdentifierEx.SqlToken ()
					).Optional ()
				select new {table, alias = alias_cl.GetOrDefault ()};

			var PFromClauseOptionalST =
					from kw_from in SpracheUtils.SqlToken ("from")
					from t1 in PFromTableExpressionST
					from tail in (
						from kw_joinN in SpracheUtils.AnyTokenST ("join", "inner join", "left join", "right join")
						from tN in PFromTableExpressionST
						from condN in (
							from kw_onN in SpracheUtils.SqlToken ("on")
							from condexpN in PExpressionRefST.Get
							select 0
						).Optional ()
						select tN
					).Many ()
					select new[] {t1}.Concat (tail).ToArray ()
				;

			var PWhereClauseOptionalST =
				(
					from kw_where in SpracheUtils.SqlToken ("where")
					from cond in PExpressionRefST.Get
					select 0
				).Optional ()
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

			var POrdinarySelectST =
					from kw_select in SpracheUtils.SqlToken ("select")
					from list in PSelectListST
					from from_cl in PFromClauseOptionalST
					from _w in PWhereClauseOptionalST
					from _g in PGroupByClauseOptionalST
					select new {list, from_cl}
				;

			var PSelectST =
					from seq in POrdinarySelectST
						.DelimitedBy (SpracheUtils.AnyTokenST ("union all", "union", "except", "subtract"))
						.Select (ss => ss.ToArray ())
					from ord in POrderByClauseOptionalST
					select new {list = seq[0].list, from_cl = seq[0].from_cl, is_many = seq.Length > 1}
				;

			var PCteLevelST =
					from name in PValidIdentifierEx
					from kw_as in SpracheUtils.SqlToken ("as")
					from select_exp in PSelectST.InParentsST ()
					select new {name, table = select_exp}
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
					select new { cte, select_body }
				;

			var POpenDatasetST =
					from kw_open in SpracheUtils.SqlToken ("open")
					from name in PValidIdentifierEx
					from _cm1 in SpracheUtils.AllCommentsST ()
					from kw_for in Parse.IgnoreCase ("for")
					from _cm2 in SpracheUtils.AllCommentsST ()
					select new {name, last_comment = _cm2.LastOrDefault ()}
				;

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
			RequestContext rc = null;
			Action<string, PSqlType> TestExpr = (s, t) => System.Diagnostics.Debug.Assert (PExpressionRefST.Get.End ().Parse (s).GetResultType (rc).Type == t);
			TestExpr ("5", PSqlType.Int);
			TestExpr ("null::real", PSqlType.Real);
			TestExpr (" null :: real ", PSqlType.Real);
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
