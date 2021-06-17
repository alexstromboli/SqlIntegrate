using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace ParseProcs
{
	public static class PSqlUtils
	{
		public static string PSqlEscape (this string Literal)
		{
			if (Literal.CanBeIdentifier ()
				&& Literal.All (c =>
					c >= 'A' && c <= 'Z'
					|| c >= 'a' && c <= 'z'
					|| c >= '0' && c <= '9'
					|| c == '_'
				))
			{
				return Literal;
			}

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

		public static string PSqlQualifiedName (params string[] Segments)
		{
			return Segments.PSqlQualifiedName ();
		}

		public static object ExecuteScalar (this Npgsql.NpgsqlConnection conn, string CommandText)
		{
			using (var cmd = conn.CreateCommand ())
			{
				cmd.CommandText = CommandText;
				return cmd.ExecuteScalar ();
			}
		}

		public static PSqlType GetBinaryOperationResultType (PSqlType Left, PSqlType Right, string Operator)
		{
			// here: text + text ?

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
