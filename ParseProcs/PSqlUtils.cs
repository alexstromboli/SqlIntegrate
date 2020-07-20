using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace ParseProcs
{
	public static class PSqlUtils
	{
		public static string PSqlEscape (this string Literal)
		{
			return new StringBuilder ("\"")
					.Append (Literal.Replace ("\"", "\"\""))
					.Append ('"')
					.ToString ()
				;
		}

		public static string PSqlQualifiedName (this IEnumerable<string> Segments)
		{
			return string.Join ('.', Segments.Select (s => s.ToLower ().PSqlEscape ()));
		}

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
}
