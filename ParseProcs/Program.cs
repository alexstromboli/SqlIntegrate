using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
	public enum PsqlOperatorPriority
	{
		None,
		Or,
		And,
		Not,
		Is,
		Comparison,
		Like,
		General,
		AddSub,
		MulDiv,
		Exp,
		Unary,
		Array,
		Typecast,
		NameSeparator
	}

	public class SqlCommentParser : CommentParser
	{
		protected SqlCommentParser ()
		{
			this.Single = "--";
		}

		public static readonly SqlCommentParser Instance = new SqlCommentParser ();
	}

	public static class PSqlUtils
	{
		public static PSqlType GetBinaryOperationResultType (PSqlType Left, PSqlType Right, string Operator)
		{
			if (Left.IsNumber && Right.IsNumber)
			{
				return Left.NumericLevel > Right.NumericLevel
					? Left
					: Right;
			}

			if (Left.IsNumber && Right.IsText
			    || Left.IsText && Right.IsNumber)
			{
				return Operator == "||"
					? PSqlType.Text
					: PSqlType.Int;
			}

			if (Left.IsDate && Right.IsTimeSpan)
			{
				return Left;
			}

			if (Left.IsTimeSpan && Right.IsDate)
			{
				return Right;
			}

			if (Left.IsTimeSpan && Right.IsTimeSpan)
			{
				return PSqlType.Interval;
			}

			if (Left.IsDate && Right.IsDate && Operator == "-")
			{
				return PSqlType.Interval;
			}

			return PSqlType.Null;
		}
	}

	public static class SpracheUtils
	{
		public static Parser<T> SqlToken<T> (this Parser<T> Inner)
		{
			return Inner
					.Commented (SqlCommentParser.Instance)
					.Select (p => p.Value)
				;
		}

		public static Parser<string> SqlToken (string Line)
		{
			return Parse
					.IgnoreCase (Line)
					.Text ()
					.Select (l => l.ToLower ())
					.SqlToken ()
				;
		}

		public static Parser<string> AnyToken (params string[] Options)
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

		public static Parser<Func<RequestContext, PSqlType>> ProduceType<T> (this Parser<T> Parser, PSqlType Type)
		{
			return Parser.Select<T, Func<RequestContext, PSqlType>> (t => rc => Type);
		}

		public static Parser<Func<RequestContext, PSqlType>> ProduceTypeThrow<T> (this Parser<T> Parser)
		{
			return Parser.Select<T, Func<RequestContext, PSqlType>> (t => rc => throw new NotImplementedException ());
		}
	}

	public class Ref<T>
	{
		public Parser<T> Parser = null;

		public Parser<T> Get { get; }

		public Ref ()
		{
			Get = Parse.Ref (() => Parser);
		}
	}

	public class RequestContext
	{
	}

	public class OperatorProcessor
	{
		public int Precedence = 0;
		public bool IsBinary = false;

		public Func<
			Func<RequestContext, PSqlType>,		// left
			Func<RequestContext, PSqlType>,		// right (null for unary)
			Func<RequestContext, PSqlType>		// result
		> Processor = null;

		public OperatorProcessor (PsqlOperatorPriority Precedence,
			bool IsBinary,
			Func<
				Func<RequestContext, PSqlType>, // left
				Func<RequestContext, PSqlType>, // right (null for unary)
				Func<RequestContext, PSqlType> // result
			> Processor
		)
		{
			this.Precedence = (int)Precedence;
			this.IsBinary = IsBinary;
			this.Processor = Processor;
		}

		public static Func<
			Func<RequestContext, PSqlType>, // left
			Func<RequestContext, PSqlType>, // right (null for unary)
			Func<RequestContext, PSqlType> // result
		> GetForBinaryOperator (string Operator)
		{
			return (l, r) => rc =>
			{
				PSqlType Left = l (rc);
				PSqlType Right = r (rc);
				return PSqlUtils.GetBinaryOperationResultType (Left, Right, Operator);
			};
		}
	}

	public class SPolynom
	{
		public class Operand
		{
			// ignore prefixes as irrelevant
			public Func<RequestContext, PSqlType> Atomic;
			public IOption<IEnumerable<OperatorProcessor>> Postfixes;
		}

		public List<Operand> Operands;
		public List<OperatorProcessor> Operators;

		protected PSqlType ResultType = null;

		public PSqlType GetResultType (RequestContext Context)
		{
			if (ResultType != null)
			{
				return ResultType;
			}

			Stack<Func<RequestContext, PSqlType>> OperandsStack = new Stack<Func<RequestContext, PSqlType>> ();
			Stack<OperatorProcessor> OperatorsStack = new Stack<OperatorProcessor> ();
			Action<int> Perform = n =>
			{
				while (OperatorsStack.Count > 0 && OperatorsStack.Peek ().Precedence >= n)
				{
					OperatorProcessor op = OperatorsStack.Pop ();
					var r = OperandsStack.Pop ();
					var l = OperandsStack.Pop ();
					var res = op.Processor (l, r);
					OperandsStack.Push (res);
				}
			};

			Action<Operand> Process = arg =>
			{
				OperandsStack.Push (arg.Atomic);

				if (arg.Postfixes.IsDefined)
				{
					foreach (var Postfix in arg.Postfixes.Get ())
					{
						Perform (Postfix.Precedence);
						var l = OperandsStack.Pop ();
						var res = Postfix.Processor (l, null);
						OperandsStack.Push (res);
					}
				}
			};

			Process (Operands[0]);
			for (int i = 0; i < Operators.Count; ++i)
			{
				var op = Operators[i];
				Perform (op.Precedence);
				OperatorsStack.Push (op);

				var v = Operands[i + 1];
				Process (v);
			}

			Perform (0);
			var ResultFunc = OperandsStack.Pop ();
			ResultType = ResultFunc (Context);
			return ResultType;
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

	public class RequestContext
	{
		public IReadOnlyList<NamedTyped> Variables;
		public IReadOnlyList<TableExpression> Tables;
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

			var PSimpleIdentifier =
					from n in Parse.Char (c => char.IsLetterOrDigit (c) || c == '_', "").AtLeastOnce ()
					where !char.IsDigit (n.First ())
					let res =new string (n.ToArray ())
					where Keywords.CanBeIdentifier (res)
					select res
				;

			var PIdentifierEx = PSimpleIdentifier.Or (PDoubleQuotedString);

			var PQualifiedIdentifier = PIdentifierEx
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

			Ref<SPolynom> PExpressionRef = new Ref<SPolynom> ();

			var PParents = PExpressionRef.Get.Contained (Parse.Char ('(').SqlToken (), Parse.Char (')').SqlToken ());
			var PBrackets = PExpressionRef.Get.Contained (Parse.Char ('[').SqlToken (), Parse.Char (']').SqlToken ());

			var PBinaryAdditionOperators = SpracheUtils.AnyToken ("+", "-");
			var PBinaryMultiplicationOperators = SpracheUtils.AnyToken ("/", "*", "%");
			var PBinaryExponentialOperators = SpracheUtils.AnyToken ("^");

			var PBinaryComparisonOperators = SpracheUtils.AnyToken (
				">=", ">", "<=", "<>", "<", "=", "!="
				);

			var PBinaryRangeOperators = SpracheUtils.AnyToken (
				"like"
			);

			var PBinaryGeneralTextOperators = SpracheUtils.AnyToken (
				"||"
			);

			var PBinaryMatchingOperators = SpracheUtils.AnyToken (
				"is"
			);
			var PNullMatchingOperators = SpracheUtils.AnyToken ("isnull", "not null");

			var PNegation = SpracheUtils.AnyToken ("not");
			var PBinaryConjunction = SpracheUtils.AnyToken ("and");
			var PBinaryDisjunction = SpracheUtils.AnyToken ("or");

			var PType =
					from t in SpracheUtils.AnyToken (PSqlType.Map.Keys.OrderByDescending (k => k.Length).ToArray ())
					from p in Parse.Number.SqlToken ()
						.DelimitedBy (Parse.Char (',').SqlToken ())
						.Contained (Parse.Char ('(').SqlToken (), Parse.Char (')').SqlToken ())
						.Optional ()
					select t
				;

			var PSimpleTypeCast =
					from op in Parse.String ("::").SqlToken ()
					from t in PType
					select t
				;

			var PFunctionCall =
					from n in PQualifiedIdentifier.SqlToken ()
					from arg in PExpressionRef.Get
						.DelimitedBy (Parse.Char (',').SqlToken ())
						.Contained (Parse.Char ('(').SqlToken (), Parse.Char (')').SqlToken ())
						.SqlToken ()
					select n
				;

			//
			var PAtomic =
					PNull.ProduceType (PSqlType.Null)
						.Or (PDecimal.ProduceType (PSqlType.Decimal))
						// PInteger must be or-ed after PDecimal
						.Or (PInteger.ProduceType (PSqlType.Int))
						.Or (PBooleanLiteral.ProduceType (PSqlType.Bool))
						.Or (PSingleQuotedString.ProduceType (PSqlType.Text))
						.Or (PParents.Select<SPolynom, Func<RequestContext, PSqlType>> (p => rc => p.GetResultType (rc)))
						.Or (PFunctionCall.SqlToken ().ProduceTypeThrow ())
						// PQualifiedIdentifier must be or-ed after PFunctionCall
						.Or (PQualifiedIdentifier.SqlToken ().ProduceTypeThrow ())
						.SqlToken ()
				;

			var PAtomicPrefixGroupOptional =
					PSignPrefix.SqlToken ()
						.Or (PNegation.SqlToken ())
						.Many ()
						.Optional ()
				;

			var PAtomicPostfixOptional =
					PBrackets.Select (b => new OperatorProcessor (PsqlOperatorPriority.None, false,
							(l, r) => throw new NotImplementedException ()))
						.Or (PSimpleTypeCast.Select (tc => new OperatorProcessor (PsqlOperatorPriority.Typecast, false,
							(l, r) => rc => PSqlType.Map[tc])))
						.Or (PNullMatchingOperators.Select (m => new OperatorProcessor (PsqlOperatorPriority.Is, false,
							(l, r) => rc => PSqlType.Bool)))
						.Many ()
						.Optional ()
				;

			var PBinaryOperators =
					PBinaryMultiplicationOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.MulDiv,
							true,
							OperatorProcessor.GetForBinaryOperator (b)))
						.Or (PBinaryAdditionOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.AddSub,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryExponentialOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.Exp,
							true,
							OperatorProcessor.GetForBinaryOperator (b))))
						.Or (PBinaryComparisonOperators.Select (b => new OperatorProcessor (
							PsqlOperatorPriority.Comparison, true,
							(l, r) => rc => PSqlType.Bool)))
						.Or (PBinaryRangeOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.Like, true,
							(l, r) => rc => PSqlType.Bool)))
						.Or (PBinaryMatchingOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.Is, true,
							(l, r) => rc => PSqlType.Bool)))
						.Or (PBinaryConjunction.Select (b => new OperatorProcessor (PsqlOperatorPriority.And, true,
							(l, r) => rc => PSqlType.Bool)))
						.Or (PBinaryDisjunction.Select (b => new OperatorProcessor (PsqlOperatorPriority.Or, true,
							(l, r) => rc => PSqlType.Bool)))
						.Or (PBinaryGeneralTextOperators.Select (b => new OperatorProcessor (PsqlOperatorPriority.General, true,
							(l, r) => rc => PSqlType.Text)))
						.SqlToken ()
				;

			var PPolynom =
					from pref1 in PAtomicPrefixGroupOptional
					from at1 in PAtomic
					from post1 in PAtomicPostfixOptional
					from rest in
					(
						from op in PBinaryOperators

						from prefN in PAtomicPrefixGroupOptional
						from atN in PAtomic
						from postN in PAtomicPostfixOptional
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

			PExpressionRef.Parser = PPolynom;

			//
			RequestContext rc = null;
			Action<string, PSqlType> TestExpr = (s, t) => System.Diagnostics.Debug.Assert (PExpressionRef.Get.End ().Parse (s).GetResultType (rc) == t);
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
