using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
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

		public static Parser<T> InParentsST<T> (this Parser<T> Inner)
		{
			return Inner.Contained (
				Parse.Char ('(').SqlToken (),
				Parse.Char (')').SqlToken ()
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

		public static Parser<Func<IRequestContext, NamedTyped>> ProduceType<T> (this Parser<T> Parser, PSqlType Type)
		{
			return Parser.Select<T, Func<IRequestContext, NamedTyped>> (t => rc => new NamedTyped (Type));
		}
	}
}
