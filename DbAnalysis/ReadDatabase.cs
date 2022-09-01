using System;
using System.Linq;
using System.Collections.Generic;

using Npgsql;

using Utils;
using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class ReadDatabase
	{
		public static DatabaseContext LoadContext (string ConnectionString)
		{
			DatabaseContext Result = new DatabaseContext
			{
				TypeMap = null,
				TablesDict = new Dictionary<string, DbTable> (),
				ProceduresDict = new Dictionary<string, Procedure> (),
				FunctionsDict = new Dictionary<string, PSqlType> (),
				SchemaOrder = new List<string> ()
			};

			using (var conn = new NpgsqlConnection (ConnectionString))
			{
				conn.Open ();

				Result.DatabaseName = (string)conn.ExecuteScalar ("SELECT current_database();");

				// types
				List<PgTypeEntry> PgTypeEntries = new List<PgTypeEntry> ();
				Dictionary<uint, PgTypeEntry> PgTypeEntriesDict;
				Dictionary<uint, PgTypeEntry> PgTypeEntriesRelidDict;
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  T.oid,
        T.typname,
        NS.nspname,
        T.typtype,
        T.typrelid,
        T.typelem,
        T.typarray
FROM pg_catalog.pg_type AS T
    INNER JOIN pg_catalog.pg_namespace AS NS ON NS.oid = T.typnamespace
;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							PgTypeEntries.Add (new PgTypeEntry
							{
								Oid = (uint) rdr["oid"],
								Name = (string) rdr["typname"],
								Schema = (string) rdr["nspname"],
								Category = (char) rdr["typtype"],
								RelId = (uint) rdr["typrelid"],
								ElemId = (uint) rdr["typelem"],
								ArrayId = (uint) rdr["typarray"]
							});
						}
					}
				}

				PgTypeEntriesDict = PgTypeEntries.ToDictionary (e => e.Oid);
				PgTypeEntriesRelidDict = PgTypeEntries
					.Where (t => t.RelId != 0)
					.ToDictionary (e => e.RelId);

				// enum values
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  enumtypid,
		enumlabel
FROM pg_catalog.pg_enum
ORDER BY enumtypid, enumsortorder
;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							uint TypeId = (uint)rdr["enumtypid"];
							string Value = (string)rdr["enumlabel"];

							if (PgTypeEntriesDict.TryGetValue (TypeId, out PgTypeEntry Parent))
							{
								Parent.EnumValues ??= new List<string> ();
								Parent.EnumValues.Add (Value);
							}
						}
					}
				}

				// attributes, properties
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  attrelid,
        attname,
        atttypid
FROM pg_catalog.pg_attribute
ORDER BY attrelid, attnum
;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							uint RelId = (uint)rdr["attrelid"];
							string Name = (string)rdr["attname"];
							uint TypeId = (uint)rdr["atttypid"];

							if (PgTypeEntriesRelidDict.TryGetValue (RelId, out PgTypeEntry Parent)
							    && PgTypeEntriesDict.TryGetValue (TypeId, out PgTypeEntry AttType)
							    )
							{
								Parent.Attributes ??= new List<PgTypeEntry.Attribute> ();
								Parent.Attributes.Add (new PgTypeEntry.Attribute { Name = Name, Type = AttType });
							}
						}
					}
				}

				Result.TypeMap = new SqlTypeMap (PgTypeEntriesDict);

				// tables
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  table_schema,
        table_name
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema');
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							string Schema = (string) rdr["table_schema"];
							string Name = (string) rdr["table_name"];

							DbTable t = new DbTable (Schema, Name);
							Result.TablesDict[t.Display] = t;
						}
					}
				}

				// table columns
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  table_schema,
        table_name,
        column_name,
        ordinal_position,
		udt_schema AS data_type_schema,
        udt_name::regtype::varchar AS data_type
FROM information_schema.columns
ORDER BY table_schema, table_name, ordinal_position;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							string Schema = (string) rdr["table_schema"];
							string TableName = (string) rdr["table_name"];
							string ColumnName = (string) rdr["column_name"];
							string TypeSchema = (string) rdr["data_type_schema"];
							string Type = (string) rdr["data_type"];

							if (!Result.TablesDict.TryGetValue (SchemaEntity.GetDisplay (Schema, TableName), out DbTable t))
							{
								continue;
							}

							NamedTyped c = new NamedTyped (ColumnName.SourcedTable (Schema, TableName, ColumnName),
								Result.GetTypeForName (TypeSchema, Type).SourcedTable (Schema, TableName, ColumnName));
							t.AddColumn (c);
						}
					}
				}

				// procedures
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  n.nspname as schema,
        p.proname as name,
        p.oid,
        p.proargmodes,
        p.proargnames,
        p.proallargtypes,
        p.proargtypes,
        p.prosrc
FROM pg_catalog.pg_namespace n
        INNER JOIN pg_catalog.pg_proc p ON pronamespace = n.oid
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
		AND p.prokind = 'p'
    ;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							string Schema = (string)rdr["schema"];
							string Name = (string)rdr["name"];
							uint Oid = (uint)rdr["oid"];
							string SourceCode = (string) rdr["prosrc"];

							Procedure p = new Procedure (Schema, Name, Oid, SourceCode);

							// no arguments if null
							string[] ArgNames = (rdr["proargnames"] as string[]) ?? Array.Empty<string> ();
							// all INs if null
							char[] ArgModes = rdr["proargmodes"] as char[];
							// proallargtypes is null for all INs, take proargtypes then
							uint[] ArgTypeCodes = (rdr["proallargtypes"] as uint[]) ?? (rdr["proargtypes"] as uint[]);

							Argument.DirectionType[] ArgDirections =
								ArgModes == null
									? ArgNames.Select (a => Argument.DirectionType.In).ToArray ()
									: ArgModes.Select (c =>
											c == 'b' ? Argument.DirectionType.InOut : Argument.DirectionType.In)
										.ToArray ();
							PSqlType[] ArgTypes = ArgTypeCodes.Select (n => Result.TypeMap.MapByOid[n]).ToArray ();

							foreach (var arg in ArgNames.Indexed ())
							{
								Argument c = new Argument (arg.Value.SourcedDefinition (),
									ArgTypes[arg.Index].SourcedDefinition (),
									ArgDirections[arg.Index]);
								p.AddArgument (c);
							}

							//
							Result.ProceduresDict[Oid.ToString ()] = p;
						}
					}
				}

				//
				string SchemaPath = (string)conn.ExecuteScalar ("SHOW search_path;");

				Result.SchemaOrder.AddRange (
					SchemaPath.Split (',')
						.Select (s => s.Trim (' ', '"'))
						.Where (s => !string.IsNullOrWhiteSpace (s))
				);
				Result.SchemaOrder.Add ("pg_catalog");

				for (int i = 0; i < Result.SchemaOrder.Count; ++i)
				{
					string s = Result.SchemaOrder[i];
					if (s.StartsWith ('$'))
					{
						s = (string)conn.ExecuteScalar ("SELECT " + s.Substring (1) + ";");
						Result.SchemaOrder[i] = s;
					}
				}

				//
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT
	routines.routine_schema,
    routines.routine_name,
    type_udt_schema AS result_schema,
    type_udt_name::regtype::varchar AS result_type
FROM information_schema.routines
WHERE routines.routine_type='FUNCTION'
    AND type_udt_name NOT IN ('any', 'citext')
ORDER BY routines.routine_schema, routines.routine_name;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							// presumed to be lowercase
							string Schema = (string)rdr["routine_schema"];
							string RoutineName = (string)rdr["routine_name"];
							string ResultSchema = (string)rdr["result_schema"];
							string ResultType = (string)rdr["result_type"];

							PSqlType Type = Result.GetTypeForName (ResultSchema, ResultType);
							if (Type == null)
							{
								continue;
							}

							string QualName = PSqlUtils.PSqlQualifiedName (Schema, RoutineName);
							Result.FunctionsDict[QualName] = Type;
						}
					}
				}
			}

			//
			Result.TypeMap.AdoptSchemaOrder (Result.SchemaOrder);

			return Result;
		}
	}
}
