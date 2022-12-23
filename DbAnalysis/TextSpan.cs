using System.Linq;
using System.Collections.Generic;

using Sprache;		// can be dismissed through redefinition of the types used

namespace DbAnalysis
{
	public class TextSpan
	{
		public Position Start { get; set; }
		public Position End { get; set; }
		public int Length { get; set; }

		public TextSpan ()
		{
		}

		public TextSpan (Position Start, Position End, int Length)
		{
			// here: verify correctness
			this.Start = Start;
			this.End = End;
			this.Length = Length;
		}

		public static TextSpan Range (TextSpan Left, TextSpan Right)
		{
			if (Left == null || Right == null)
			{
				return null;
			}

			return new TextSpan { Start = Left.Start, End = Right.End, Length = Right.End.Pos - Left.Start.Pos };
		}

		public override string ToString ()
		{
			return $"{Start} + {Length}";
		}
	}

	public class TextSpan<T> : TextSpan, ITextSpan<T>
	{
		public T Value { get; set; }
	}

	public static class TextSpanUtils
	{
		public static TextSpan ToTextSpan<T> (this ITextSpan<T> Span)
		{
			return Span as TextSpan
			       ?? new TextSpan { Start = Span.Start, End = Span.End, Length = Span.Length };
		}

		// proper order presumed
		public static TextSpan Range (this IEnumerable<TextSpan> Spans)
		{
			return TextSpan.Range (Spans.First (), Spans.Last ());
		}
	}
}
