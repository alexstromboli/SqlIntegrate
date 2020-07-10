using System;
using System.Collections.Generic;

namespace ParseProcs
{
	public class PSqlType
	{
		public string Display { get; protected set; }
		public Type ClrType { get; protected set; }

		protected PSqlType ()
		{
		}

		public override string ToString ()
		{
			return Display;
		}

		protected static Dictionary<string, PSqlType> _Map;
		public static IReadOnlyDictionary<string, PSqlType> Map => _Map;

		static PSqlType ()
		{
		}

		protected static PSqlType Add (Type ClrType, params string[] Keys)
		{
			_Map ??= new Dictionary<string, PSqlType> ();

			PSqlType Type = new PSqlType { Display = Keys[0], ClrType = ClrType };
			foreach (string Key in Keys)
			{
				_Map[Key] = Type;
			}

			return Type;
		}

		public static readonly PSqlType RefCursor = Add (typeof (object), "refcursor");
		public static readonly PSqlType Bool = Add (typeof (bool), "bool", "boolean");
		public static readonly PSqlType Binary = Add (typeof (byte[]), "bytea");
		public static readonly PSqlType Guid = Add (typeof (Guid), "uuid");

		public static readonly PSqlType Int = Add (typeof (int), "int", "integer", "serial");
		public static readonly PSqlType SmallInt = Add (typeof (int), "smallint", "smallserial");
		public static readonly PSqlType BigInt = Add (typeof (long), "bigint", "bigserial");
		public static readonly PSqlType Money = Add (typeof (decimal), "money");
		public static readonly PSqlType Decimal = Add (typeof (decimal), "decimal", "numeric");
		public static readonly PSqlType Real = Add (typeof (float), "real");
		public static readonly PSqlType Float = Add (typeof (double), "float", "double precision");
		
		public static readonly PSqlType Json = Add (typeof (string), "json");
		public static readonly PSqlType Jsonb = Add (typeof (string), "jsonb");

		public static readonly PSqlType Date = Add (typeof (DateTime), "date");
		public static readonly PSqlType Interval = Add (typeof (TimeSpan), "interval");
		public static readonly PSqlType Time = Add (typeof (DateTime), "time", "time without time zone");
		public static readonly PSqlType TimeTz = Add (typeof (DateTime), "time with time zone");
		public static readonly PSqlType Timestamp = Add (typeof (DateTime), "timestamp", "timestamp without time zone");
		public static readonly PSqlType TimestampTz = Add (typeof (DateTime), "timestamp with time zone");

		public static readonly PSqlType Text = Add (typeof (string), "text");
		public static readonly PSqlType Char = Add (typeof (string), "char", "character");
		public static readonly PSqlType VarChar = Add (typeof (string), "varchar", "character varying");
	}
}
