using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace ParseProcs
{
	public static class PSqlUtils
	{
		public static string PSqlEscape (this string LiteralL)
		{
			if (!LiteralL.IsKeyword ()
				&& LiteralL.All (c => char.IsLetterOrDigit (c) || c == '_' ))
			{
				return LiteralL;
			}

			return new StringBuilder ("\"")
					.Append (LiteralL.Replace ("\"", "\"\""))
					.Append ('"')
					.ToString ()
				;
		}

		public static string PSqlQualifiedName (this IEnumerable<string> Segments)
		{
			return Segments.Select (s => s.ToLower ().PSqlEscape ()).JoinDot ();
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
			if (Left.IsNumber && Right.IsNumber)
			{
				if (Left.BaseType == PSqlType.Money || Right.BaseType == PSqlType.Money)
				{
					return PSqlType.Money;
				}

				if (Left.BaseType == PSqlType.Real || Right.BaseType == PSqlType.Real)
				{
					if (Left.BaseType == PSqlType.Real && Right.BaseType == PSqlType.Real)
					{
						return PSqlType.Real;
					}

					return PSqlType.Float;
				}

				return Left.NumericLevel > Right.NumericLevel
					? Left
					: Right;
			}

			if (Operator == "->>" || Operator == "#>>")
			{
				return PSqlType.Text;
			}

			if (Operator == "->" || Operator == "#>")
			{
				return Left;
			}

			if (Left.IsNumber && Right.IsText
			    || Left.IsText && Right.IsNumber)
			{
				return Operator == "||"
					? PSqlType.Text
					: PSqlType.Int;
			}

			if (Left.IsDate && Right.IsTimeSpan
			    || Left.IsTimeSpan && Right.IsDate)
			{
				return PSqlType.Timestamp;
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
