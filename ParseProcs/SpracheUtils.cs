using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

using Utils;

namespace ParseProcs
{
	public static class SpracheUtils
	{
		public static Parser<T> Failure<T> (string Message = null, string Expected = null)
		{
			return i => Result.Failure<T> (i, Message ?? "Parser failed",
				Expected == null ? Array.Empty<string> () : Expected.ToTrivialArray ());
		}
		
		public static Parser<string> ToLower (this Parser<string> Inner)
		{
			return Inner.Select (s => s.ToLower ());
		}

		public static Parser<T> SqlToken<T> (this Parser<T> Inner)
		{
			return Inner
					.Commented (SqlCommentParser.Instance)
					.Select (p => p.Value)
				;
		}

		public static Parser<string> SqlToken (string Line)
		{
			if (Line.All (c => char.IsLetterOrDigit (c) || c == '_'))
			{
				// to prevent cases like taking 'order' for 'or'
				// take all the letters, and then check
				return Parse
						.LetterOrDigit
						.Or (Parse.Char ('_'))
						.Many ()
						.Text ()
						.Where (s => string.Equals (s, Line, StringComparison.InvariantCultureIgnoreCase))
						.Select (l => l.ToLower ())
						.SqlToken ()
					;
			}

			return Parse
					.IgnoreCase (Line)
					.Text ()
					.ToLower ()
					.SqlToken ()
				;
		}

		// generally, use of this is a red flag of bad design
		public static Parser<T> Or<T> (this IEnumerable<Parser<T>> Items)
		{
			Items = Items.Where (i => i != null);

			if (!Items.Any ())
			{
				return null;
			}

			return Items
					.Skip (1)
					.Aggregate (Items.First (), (ch, i) => ch.Or (i))
				;
		}

		public static Parser<T> InParentsST<T> (this Parser<T> Inner)
		{
			return Inner.Contained (
				SqlToken ("("),
				SqlToken (")")
			);
		}

		public static Parser<T> InBracketsST<T> (this Parser<T> Inner)
		{
			return Inner.Contained (
				SqlToken ("["),
				SqlToken ("]")
			);
		}

		public static Parser<IEnumerable<T>> CommaDelimitedST<T> (this Parser<T> Inner, bool CanBeEmpty = false)
		{
			var Result = Inner.DelimitedBy (Parse.Char (',').SqlToken ());

			if (CanBeEmpty)
			{
				Result = Result.Optional ().Select (seq => seq.GetOrElse (new T[0]));
			}

			return Result;
		}

		public static Parser<IEnumerable<string>> AllCommentsST ()
		{
			return SqlCommentParser.Instance.AnyComment
					.Or (Parse.WhiteSpace.Return ((string)null))
					.Many ()
					.Select (seq => seq.Where (l => l != null))
				;
		}

		// postfix ST means that the result is 'SQL token',
		// i.e. duly processes comments and whitespaces
		public static Parser<string> AnyTokenST (params string[] Options)
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

		public static Parser<Func<RequestContext, NamedTyped>> ProduceType<T> (this Parser<T> Parser, PSqlType Type)
		{
			return Parser.Select<T, Func<RequestContext, NamedTyped>> (t => rc => new NamedTyped (Type));
		}

		public static Parser<Func<RequestContext, NamedTyped>> ProduceType (this Parser<PSqlType> Parser)
		{
			return Parser.Select<PSqlType, Func<RequestContext, NamedTyped>> (t => rc => new NamedTyped (t));
		}
	}
}
