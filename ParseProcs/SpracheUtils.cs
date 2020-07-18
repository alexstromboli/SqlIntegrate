using System;
using System.Linq;

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
}
