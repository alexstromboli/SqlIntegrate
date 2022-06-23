using System;
using System.Linq;
using System.Collections.Generic;

using Utils;
using DbAnalysis.Datasets;

namespace DbAnalysis
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

		public class Property
		{
			public string Name;
			public PSqlType Type;

			public override string ToString ()
			{
				return $"{Name ?? "???"} {Type?.ToString () ?? "???"}";
			}
		}

		public string Schema { get; set; }
		public string OwnName { get; set; }
		public bool IsArray { get; set; } = false;
		// doesn't work for types
		// name, int2vector, oidvector, point, lseg, box, line
		// which are both arrays and items
		public PSqlType BaseType { get; set; }		// can be self
		public PSqlType ArrayType { get; set; }			// can be self

		public string ShortName { get; set; }
		public string Display { get; set; }
		public Type ClrType { get; set; }
		public NumericOrderLevel NumericLevel = NumericOrderLevel.None;

		public string[] EnumValues;
		public Property[] Properties;
		public Dictionary<string, Property> PropertiesDict;

		public bool IsCustom => EnumValues != null && EnumValues.Length > 0
		                        || Properties != null && Properties.Length > 0;

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
			return ShortName ?? Display;
		}
	}

	public class SqlTypeMap
	{
		protected Dictionary<string, PSqlType> _Map;
		public IReadOnlyDictionary<string, PSqlType> Map => _Map;
		public IReadOnlyDictionary<uint, PSqlType> MapByOid { get; }	// null if not initialized from DB

		// version for lookup during procedure body parsing
		protected PSqlType GetTypeForName (IEnumerable<string> SchemaOrder, string TypeName)
		{
			return SchemaOrder.Select (s =>
						Map.TryGetValue (s + "." + TypeName, out var f) ? f : null)
					.FirstOrDefault (f => f != null)
				;
		}

		// version for lookup during code generation
		public PSqlType GetTypeForName (params string[] TypeName)
		{
			return Map.TryGetValue (TypeName.JoinDot (), out var f) ? f : null;
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
			var Result = AddType (ClrType, "pg_catalog", Keys);
			Result.ShortName ??= Keys[0];
			if (Result.ArrayType != null)
			{
				Result.ArrayType.ShortName ??= Keys[0] + "[]";
			}

			// for standard types add short names to map
			foreach (var p in new[]
			         {
				         new { type = Result, keys = Keys },
				         new { type = Result.ArrayType, keys = Keys.Select (k => k + "[]").ToArray () }
			         })
			{
				if (p.type != null)
				{
					foreach (string Key in p.keys)
					{
						_Map.TryAdd (Key, p.type);
					}
				}
			}

			return Result;
		}

		public PSqlType Null { get; protected set; }
		public PSqlType Record { get; protected set; }
		public PSqlType RefCursor { get; protected set; }
		public PSqlType Bool { get; protected set; }
		public PSqlType Binary { get; protected set; }
		public PSqlType Guid { get; protected set; }

		public PSqlType Int { get; protected set; }
		public PSqlType SmallInt { get; protected set; }
		public PSqlType BigInt { get; protected set; }
		public PSqlType Money { get; protected set; }
		public PSqlType Decimal { get; protected set; }
		public PSqlType Real { get; protected set; }
		public PSqlType Float { get; protected set; }

		public PSqlType Json { get; protected set; }
		public PSqlType Jsonb { get; protected set; }

		public PSqlType Date { get; protected set; }
		public PSqlType Timestamp { get; protected set; }
		public PSqlType TimestampTz { get; protected set; }
		public PSqlType Interval { get; protected set; }
		public PSqlType Time { get; protected set; }
		public PSqlType TimeTz { get; protected set; }

		public PSqlType Text { get; protected set; }
		public PSqlType Char { get; protected set; }
		public PSqlType VarChar { get; protected set; }
		public PSqlType BpChar { get; protected set; }
		public PSqlType Name { get; protected set; }
		public PSqlType CString { get; protected set; }
		public PSqlType RegType { get; protected set; }

		public static SqlTypeMap FromTypes<TColumn> (IEnumerable<GSqlType<TColumn>> Types)
			where TColumn : Column, new()
		{
			SqlTypeMap This = new SqlTypeMap (null);

			foreach (var t in Types)
			{
				PSqlType BaseType = new PSqlType { Schema = t.Schema, OwnName = t.Name, Display = t.Schema + "." + t.Name };
				PSqlType ArrayType = new PSqlType { Schema = t.Schema, OwnName = t.Name + "[]", Display = t.Schema + "." + t.Name + "[]", IsArray = true};
				BaseType.BaseType = BaseType;
				ArrayType.BaseType = BaseType;
				BaseType.ArrayType = ArrayType;
				ArrayType.ArrayType = ArrayType;
				This._Map[BaseType.Display] = BaseType;
				This._Map[ArrayType.Display] = ArrayType;

				if (t.Enum != null && t.Enum.Length > 0)
				{
					BaseType.EnumValues = t.Enum;
					BaseType.ClrType = typeof(string);
					ArrayType.ClrType = typeof(string[]);
				}

				if (t.Properties != null && t.Properties.Length > 0)
				{
					BaseType.ClrType = typeof(object);
				}
			}

			return This;
		}

		public SqlTypeMap (Dictionary<uint, PgTypeEntry> PgTypeEntriesDict)		// can be null
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

					PSqlType SqlType = new PSqlType { Schema = Schema, OwnName = t.Name, Display = QualTypeName, IsArray = IsArray };
					SqlType.BaseType = SqlType;
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

					if (t.ArrayId > 0)
					{
						PSqlType ArrayType = OidToSqlTypeDict[t.ArrayId];
						ArrayType.BaseType = BaseType;
						BaseType.ArrayType = ArrayType;
						ArrayType.ArrayType = ArrayType;
					}
				}

				// populate enum values
				// and properties
				foreach (var t in PgTypeEntriesDict.Values)
				{
					PSqlType Type = OidToSqlTypeDict[t.Oid];

					if (t.EnumValues != null && t.EnumValues.Count > 0)
					{
						Type.EnumValues = t.EnumValues.ToArray ();
					}

					if (t.Attributes != null)
					{
						Type.Properties = t.Attributes
								.Select (a => new PSqlType.Property { Name = a.Name, Type = MapByOid[a.Type.Oid] })
								.ToArray ()
							;
						Type.PropertiesDict = Type.Properties.ToDictionary (p => p.Name);
					}
				}
			}

			InitStandardProperties ();
		}

		protected void InitStandardProperties ()
		{
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
			this.Char = AddPgCatalogType (typeof (string), "char", "character").SetIsText ();
			this.VarChar = AddPgCatalogType (typeof (string), "varchar", "character varying").SetIsText ();
			this.BpChar = AddPgCatalogType (typeof (string), "bpchar").SetIsText ();
			this.Name = AddPgCatalogType (typeof (string), "name").SetIsText ();
			this.CString = AddPgCatalogType (typeof (string), "cstring").SetIsText ();
			this.RegType = AddPgCatalogType (typeof (uint), "regtype");
		}

		public void AdoptSchemaOrder (List<string> SchemaOrder)
		{
			foreach (var t in Map.Values.ToArray ())
			{
				if (t.OwnName != null
				    && !Map.ContainsKey (t.OwnName)
				    && ReferenceEquals (GetTypeForName (SchemaOrder, t.OwnName), t)
				    )
				{
					_Map[t.OwnName] = t;

					if (t.ArrayType != null)
					{
						_Map[t.OwnName + "[]"] = t.ArrayType;
					}
				}
			}
		}
	}
}
