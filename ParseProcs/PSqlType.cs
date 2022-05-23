using System;
using System.Linq;
using System.Collections.Generic;

namespace ParseProcs
{
	public class PgTypeEntry
	{
		public uint Oid;
		public string Name;
		public string Schema;
		public char Category;
		public uint RelId;
		public uint ElemId;
		public uint ArrayId;

		public class Attribute
		{
			public string Name;
			public PgTypeEntry Type;

			public override string ToString ()
			{
				return $"{Name ?? "???"}.{Type?.ToString () ?? "???"}";
			}
		}

		public List<Attribute> Attributes;
		public List<string> EnumValues;

		public override string ToString ()
		{
			return $"{Schema ?? "???"}.{Name ?? "???"}";
		}
	}

	public class PSqlType
	{
		public enum NumericOrderLevel
		{
			None,
			SmallInt,
			Int,
			BigInt,
			Real,
			Decimal,
			Float,
			Money
		}

		public string Schema { get; set; }
		public bool IsArray { get; set; } = false;
		// doesn't work for types
		// name, int2vector, oidvector, point, lseg, box, line
		// which are both arrays and items
		public PSqlType BaseType { get; set; }		// can be self
		public PSqlType ArrayType { get; set; }			// can be self

		public string Display { get; set; }
		public Type ClrType { get; set; }
		public NumericOrderLevel NumericLevel = NumericOrderLevel.None;

		public bool IsNumber { get; set; } = false;
		public PSqlType SetNumericLevel (NumericOrderLevel Level)
		{
			NumericLevel = Level;
			IsNumber = true;
			return this;
		}

		public bool IsDate { get; protected set; } = false;
		public PSqlType SetIsDate (bool Value = true) { IsDate = Value; return this; }
		public bool IsTimeSpan { get; protected set; } = false;
		public PSqlType SetIsTimeSpan (bool Value = true) { IsTimeSpan = Value; return this; }
		public bool IsText { get; protected set; } = false;
		public PSqlType SetIsText (bool Value = true) { IsText = Value; return this; }

		public PSqlType ()
		{
		}

		public override string ToString ()
		{
			return Display;
		}
	}

	public class SqlTypeMap
	{
		protected Dictionary<string, PSqlType> _Map;
		public IReadOnlyDictionary<string, PSqlType> Map => _Map;
		public IReadOnlyDictionary<uint, PSqlType> MapByOid { get; }	// null if not initialized from DB

		public string[] GetAllKeys ()
		{
			return _Map.Keys.OrderByDescending (k => k.Length).ToArray ();
		}

		// https://dba.stackexchange.com/questions/90230/postgresql-determine-column-type-when-data-type-is-set-to-array
		protected PSqlType AddType (Type ClrType, string Schema, params string[] Keys)
		{
			string[] BaseKeys = Keys.Select (k => Schema + "." + k).ToArray ();
			string[] ArrayKeys = Keys.Select (k => Schema + "." + k + "[]").ToArray ();

			PSqlType BaseType = BaseKeys
					.Select (n => _Map.TryGetValue (n, out var t) ? t : null)
					.FirstOrDefault (t => t != null)
				;
			PSqlType ArrayType = ArrayKeys
					.Select (n => _Map.TryGetValue (n, out var t) ? t : null)
					.FirstOrDefault (t => t != null)
				;

			if (BaseType == null && ArrayType == null)
			{
				BaseType = new PSqlType { Schema = Schema, Display = BaseKeys[0], ClrType = ClrType };
				ArrayType = new PSqlType { Schema = Schema, Display = ArrayKeys[0], ClrType = ClrType, IsArray = true };

				BaseType.BaseType = BaseType;
				BaseType.ArrayType = ArrayType;
				ArrayType.BaseType = BaseType;
				ArrayType.ArrayType = ArrayType;
			}

			if (ArrayType == null && BaseType != null)
			{
				ArrayType = BaseType.ArrayType;
			}

			if (BaseType == null && ArrayType != null)
			{
				BaseType = ArrayType.BaseType;
			}

			foreach (var p in new[] { new { type = BaseType, keys = BaseKeys }, new { type = ArrayType, keys = ArrayKeys } })
			{
				if (p.type != null)
				{
					p.type.ClrType ??= ClrType;

					foreach (string Key in p.keys)
					{
						_Map.TryAdd (Key, p.type);
					}
				}
			}

			return BaseType;
		}

		protected PSqlType AddPgCatalogType (Type ClrType, params string[] Keys)
		{
			return AddType (ClrType, "pg_catalog", Keys);
		}

		public readonly PSqlType Null;
		public readonly PSqlType Record;
		public readonly PSqlType RefCursor;
		public readonly PSqlType Bool;
		public readonly PSqlType Binary;
		public readonly PSqlType Guid;

		public readonly PSqlType Int;
		public readonly PSqlType SmallInt;
		public readonly PSqlType BigInt;
		public readonly PSqlType Money;
		public readonly PSqlType Decimal;
		public readonly PSqlType Real;
		public readonly PSqlType Float;

		public readonly PSqlType Json;
		public readonly PSqlType Jsonb;

		public readonly PSqlType Date;
		public readonly PSqlType Timestamp;
		public readonly PSqlType TimestampTz;
		public readonly PSqlType Interval;
		public readonly PSqlType Time;
		public readonly PSqlType TimeTz;

		public readonly PSqlType Text;
		public readonly PSqlType Char;
		public readonly PSqlType VarChar;
		public readonly PSqlType Name;
		public readonly PSqlType CString;
		public readonly PSqlType RegType;

		public SqlTypeMap (Dictionary<uint, PgTypeEntry> PgTypeEntriesDict = null)
		{
			_Map = new Dictionary<string, PSqlType> ();

			if (PgTypeEntriesDict != null)
			{
				Dictionary<uint, PSqlType> OidToSqlTypeDict = new Dictionary<uint, PSqlType> ();
				MapByOid = OidToSqlTypeDict;

				foreach (var t in PgTypeEntriesDict.Values)
				{
					// avoid types which are both arrays and items
					bool IsArray = t.ElemId > 0 && PgTypeEntriesDict[t.ElemId].ArrayId == t.Oid;

					string Schema = IsArray
						? PgTypeEntriesDict[t.ElemId].Schema
						: t.Schema;
					string QualTypeName = Schema + "."
					                             + (IsArray
						                             ? PgTypeEntriesDict[t.ElemId].Name + "[]"
						                             : t.Name);

					PSqlType SqlType = new PSqlType { Schema = Schema, Display = QualTypeName, IsArray = IsArray };
					_Map[QualTypeName] = SqlType;
					OidToSqlTypeDict[t.Oid] = SqlType;

					if (t.EnumValues != null && t.EnumValues.Count > 0)
					{
						SqlType.ClrType = typeof(string);
					}
				}

				// connect base types to arrays
				foreach (var t in PgTypeEntriesDict.Values)
				{
					PSqlType BaseType = OidToSqlTypeDict[t.Oid];
					BaseType.BaseType = BaseType;

					if (t.ArrayId > 0)
					{
						PSqlType ArrayType = OidToSqlTypeDict[t.ArrayId];
						ArrayType.BaseType = BaseType;
						BaseType.ArrayType = ArrayType;
						ArrayType.ArrayType = ArrayType;
					}
				}
			}

			this.Null = AddPgCatalogType (typeof (object), "unknown");
			this.Record = AddPgCatalogType (typeof (object), "record");
			this.RefCursor = AddPgCatalogType (typeof (object), "refcursor");
			this.Bool = AddPgCatalogType (typeof (bool), "bool", "boolean");
			this.Binary = AddPgCatalogType (typeof (byte[]), "bytea");
			this.Guid = AddPgCatalogType (typeof (Guid), "uuid");

			this.Int = AddPgCatalogType (typeof (int), "int", "integer", "serial", "int4").SetNumericLevel (PSqlType.NumericOrderLevel.Int);
			this.SmallInt = AddPgCatalogType (typeof (short), "smallint", "smallserial", "int2").SetNumericLevel (PSqlType.NumericOrderLevel.SmallInt);
			this.BigInt = AddPgCatalogType (typeof (long), "bigint", "bigserial", "int8").SetNumericLevel (PSqlType.NumericOrderLevel.BigInt);
			this.Money = AddPgCatalogType (typeof (decimal), "money").SetNumericLevel (PSqlType.NumericOrderLevel.Money);
			this.Decimal = AddPgCatalogType (typeof (decimal), "decimal", "numeric").SetNumericLevel (PSqlType.NumericOrderLevel.Decimal);
			this.Real = AddPgCatalogType (typeof (float), "real", "float4").SetNumericLevel (PSqlType.NumericOrderLevel.Real);
			this.Float = AddPgCatalogType (typeof (double), "float", "double precision").SetNumericLevel (PSqlType.NumericOrderLevel.Float);

			this.Json = AddPgCatalogType (typeof (string), "json");
			this.Jsonb = AddPgCatalogType (typeof (string), "jsonb");

			this.Date = AddPgCatalogType (typeof (DateTime), "date").SetIsDate ();
			this.Timestamp = AddPgCatalogType (typeof (DateTime), "timestamp", "timestamp without time zone").SetIsDate ();
			this.TimestampTz = AddPgCatalogType (typeof (DateTime), "timestamptz", "timestamp with time zone").SetIsDate ();
			this.Interval = AddPgCatalogType (typeof (TimeSpan), "interval").SetIsTimeSpan ();
			this.Time = AddPgCatalogType (typeof (TimeSpan), "time", "time without time zone").SetIsTimeSpan ();
			this.TimeTz = AddPgCatalogType (typeof (TimeSpan), "timetz", "time with time zone").SetIsTimeSpan ();

			this.Text = AddPgCatalogType (typeof (string), "text").SetIsText ();
			this.Char = AddPgCatalogType (typeof (string), "char", "character", "bpchar").SetIsText ();
			this.VarChar = AddPgCatalogType (typeof (string), "varchar", "character varying").SetIsText ();
			this.Name = AddPgCatalogType (typeof (string), "name").SetIsText ();
			this.CString = AddPgCatalogType (typeof (string), "cstring").SetIsText ();
			this.RegType = AddPgCatalogType (typeof (uint), "regtype");
		}
	}
}
