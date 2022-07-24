using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

using Utils;
using DbAnalysis.Sources;

namespace DbAnalysis
{
	public static class SpracheUtils
	{
		public static Parser<T> Failure<T> (string Message = null, string Expected = null)
		{
			return i => Result.Failure<T> (i, Message ?? "Parser failed",
				Expected == null ? Array.Empty<string> () : Expected.ToTrivialArray ());
		}

		public static T[] GetOrEmpty<T> (this IOption<IEnumerable<T>> Input)
		{
			return Input.GetOrElse (Array.Empty<T> ()).ToArray ();
		}

		public static Parser<string> ToLower (this Parser<string> Inner)
		{
			return Inner.Select (s => s.ToLower ());
		}

		public static Parser<Sourced<T>> SpanSourced<T> (this Parser<T> Inner)
		{
			return Inner
					.Span ()
					.Select (t => t.Value.SourcedTextSpan (t.ToTextSpan ()))
				;
		}

		public static Parser<Sourced<string>> ToLower (this Parser<Sourced<string>> Inner)
		{
			return Inner.Select (s => s.ToLower ());
		}

		public static string JoinDot (this IEnumerable<Sourced<string>> Fragments)
		{
			return Fragments.Values ().JoinDot ();
		}

		public static Parser<Sourced<T>> SqlToken<T> (this Parser<Sourced<T>> Inner)
		{
			return Inner
					.Commented (SqlCommentParser.Instance)
					.Select (p => p.Value)
				;
		}

		public static Parser<Sourced<T>> SqlToken<T> (this Parser<T> Inner)
		{
			return Inner
					.SpanSourced ()
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

		public static Parser<T> InBracketsST<T> (this Parser<T> Inner)
		{
			return Inner.Contained (
				Parse.Char ('[').SqlToken (),
				Parse.Char (']').SqlToken ()
			);
		}

		public static Parser<IEnumerable<T>> CommaDelimitedST<T> (this Parser<T> Inner, bool CanBeEmpty = false)
		{
			var Result = Inner.DelimitedBy (Parse.Char (',').SqlToken ());

			if (CanBeEmpty)
			{
				Result = Result.Optional ().Select (seq => seq.GetOrEmpty ());
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

		public static Parser<RcFunc<NamedTyped>> ProduceType<T> (this Parser<Sourced<T>> Parser, PSqlType Type)
		{
			return Parser.Select<Sourced<T>, RcFunc<NamedTyped>> (t =>
				rc => new NamedTyped (new Sourced<PSqlType> (Type, t.Source)));
		}

		public static Parser<RcFunc<NamedTyped>> ProduceType<T> (this Parser<T> Parser, PSqlType Type)
		{
			return Parser.SpanSourced ().ProduceType (Type);
		}

		public static Parser<RcFunc<NamedTyped>> ProduceType (this Parser<Sourced<PSqlType>> Parser)
		{
			return Parser.Select<Sourced<PSqlType>, RcFunc<NamedTyped>> (t => rc => new NamedTyped (t));
		}
	}
}
