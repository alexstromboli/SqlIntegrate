using System.Linq;
using System.Text;
using System.Collections.Generic;

using Utils;

namespace DbAnalysis
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

		public static PSqlType GetBinaryOperationResultType (SqlTypeMap Typemap, PSqlType Left, PSqlType Right, string Operator)
		{
			if (Left.IsNumber && Right.IsNumber)
			{
				if (Left.BaseType == Typemap.Money || Right.BaseType == Typemap.Money)
				{
					return Typemap.Money;
				}

				if (Left.BaseType == Typemap.Real || Right.BaseType == Typemap.Real)
				{
					if (Left.BaseType == Typemap.Real && Right.BaseType == Typemap.Real)
					{
						return Typemap.Real;
					}

					return Typemap.Float;
				}

				return Left.NumericLevel > Right.NumericLevel
					? Left
					: Right;
			}

			if (Operator == "->>" || Operator == "#>>")
			{
				return Typemap.VarChar;
			}

			if (Operator == "->" || Operator == "#>")
			{
				return Left;
			}

			if (Operator == "||")
			{
				if (Left.IsArray)
				{
					return Left;
				}

				if (Right.IsArray)
				{
					return Right;
				}

				if (Left.IsText)
				{
					return Left;
				}

				if (Right.IsText)
				{
					return Right;
				}
			}

			if (Left == Typemap.TimestampTz && Right.IsTimeSpan
			    || Left.IsTimeSpan && Right == Typemap.TimestampTz)
			{
				return Typemap.TimestampTz;
			}

			if (Left.IsDate && Right.IsTimeSpan
			    || Left.IsTimeSpan && Right.IsDate)
			{
				return Typemap.Timestamp;
			}

			if (Left.IsTimeSpan && Right.IsTimeSpan)
			{
				return Typemap.Interval;
			}

			if (Left.IsDate && Right.IsDate && Operator == "-")
			{
				return Typemap.Interval;
			}

			return Typemap.Null;
		}

		// 'x AT TIME ZONE zone' takes its result type from its LEFT operand alone;
		// the zone names a zone and contributes nothing. The three cases are the ones
		// PostgreSQL documents, and they are NOT a symmetric flip:
		//
		//   timestamptz -> timestamp   (rendered in the zone, zone dropped)
		//   timestamp   -> timestamptz (read as local to the zone)
		//   timetz      -> timetz      (re-rendered; it KEEPS its zone)
		//
		// The timetz row is the one worth stating, because 'it flips zone-awareness'
		// predicts 'time' there and is wrong -- verified against PostgreSQL with
		// pg_typeof. 'time' is not documented but takes the implicit cast to timetz.
		//
		// Anything else is passed through rather than becoming Null. The operator is
		// only defined for these types, so a caller that reaches it with anything else
		// has a SQL error, and inventing a type for it would hide that.
		public static PSqlType GetAtTimeZoneResultType (SqlTypeMap Typemap, PSqlType Left)
		{
			if (Left == Typemap.TimestampTz)
			{
				return Typemap.Timestamp;
			}

			if (Left == Typemap.Timestamp)
			{
				return Typemap.TimestampTz;
			}

			if (Left == Typemap.Time)
			{
				return Typemap.TimeTz;
			}

			return Left;
		}
	}
}
