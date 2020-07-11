using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
	public static class SpracheUtils
	{
		public static Parser<string> AnyToken (params string[] Options)
		{
			Parser<string> Result = null;
			foreach (string[] Tokens in Options.Select (s => s.Split (' ', StringSplitOptions.RemoveEmptyEntries)))
			{
				Parser<string> Line = null;
				foreach (string Token in Tokens)
				{
					var PT = Parse.IgnoreCase (Token).Text ();
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
			var PFloat =
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
					select new string (n.ToArray ())
				;

			var PIdentifierEx = PSimpleIdentifier.Or (PDoubleQuotedString);

			var PQualifiedIdentifier = Parse.DelimitedBy (PIdentifierEx, Parse.String ("."), 1, null)
					.Select (seq => seq.ToArray ())
				;

			var PSignPrefix =
					from c in Parse.Chars ('+', '-')
					from sp in Parse.Chars ('+', '-').Or (Parse.WhiteSpace).Many ().Text ()
					let res = c + sp
					where !res.Contains ("--")
					select res
				;

			Ref<string> PExpressionRef = new Ref<string> ();

			var PParents = PExpressionRef.Get.Contained (Parse.Char ('('), Parse.Char (')'));
			var PBrackets = PExpressionRef.Get.Contained (Parse.Char ('['), Parse.Char (']'));

			var PBinaryAdditionOperators = SpracheUtils.AnyToken ("+", "-");
			var PBinaryMultiplicationOperators = SpracheUtils.AnyToken ("/", "*", "%");
			var PBinaryExponentialOperators = SpracheUtils.AnyToken ("^");

			var PBinaryComparisonOperators = SpracheUtils.AnyToken (
				">", ">=", "<", "<=", "=", "<>", "!="
				);

			var PBinaryRangeOperators = SpracheUtils.AnyToken (
				"like"
			);

			var PBinaryMatchingOperators = SpracheUtils.AnyToken (
				"is"
			);
			var PNullMatchingOperators = SpracheUtils.AnyToken ("isnull", "not null");

			var PNegation = SpracheUtils.AnyToken ("not");
			var PBinaryConjunction = SpracheUtils.AnyToken ("and");
			var PBinaryDisjunction = SpracheUtils.AnyToken ("or");

			var PType =
					from t in SpracheUtils.AnyToken (PSqlType.Map.Keys.ToArray ())
					from p in Parse.Number
						.DelimitedBy (Parse.Char (','))
						.Contained (Parse.Char ('('), Parse.Char (')'))
						.Optional ()
					select t
				;

			var PSimpleTypeCast =
					from op in Parse.String ("::")
					from t in PType
					select t
				;

			var PFunctionCall =
					from n in PQualifiedIdentifier
					from arg in PExpressionRef.Get
						.DelimitedBy (Parse.Char (','))
						.Contained (Parse.Char ('('), Parse.Char (')'))
					select n
				;

			//
			var PAtomic =
					PInteger.ProduceType (PSqlType.Int)
						.Or (PNull.ProduceType (PSqlType.Null))
						.Or (PFloat.ProduceType (PSqlType.Decimal))
						.Or (PBooleanLiteral.ProduceType (PSqlType.Bool))
						.Or (PSingleQuotedString.ProduceType (PSqlType.VarChar))
						.Or (PQualifiedIdentifier.ProduceTypeThrow ())
						.Or (PParents.ProduceTypeThrow ())
						.Or (PFunctionCall.ProduceTypeThrow ())
				;

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
