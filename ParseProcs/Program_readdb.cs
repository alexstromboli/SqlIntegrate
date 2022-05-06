using System.Linq;
using System.Collections.Generic;

using Npgsql;

using Utils;

namespace ParseProcs
{
	public class DatabaseContext
	{
		public string DatabaseName;
		public SqlTypeMap TypeMap;
		public Dictionary<string, DbTable> TablesDict;
		public Dictionary<string, Procedure> ProceduresDict;
		public Dictionary<string, PSqlType> FunctionsDict;
		public List<string> SchemaOrder;
	}

	partial class Program
	{
		private static DatabaseContext ReadDatabase (string ConnectionString)
		{
			var TypeMap = new SqlTypeMap ();
			DatabaseContext Result = new DatabaseContext
			{
				TypeMap = TypeMap,
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
				Dictionary<uint, string> TypesDict = new Dictionary<uint, string> ();
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  oid,
        typname
FROM pg_catalog.pg_type
;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							uint Oid = (uint) rdr["oid"];
							string Name = (string) rdr["typname"];

							TypesDict[Oid] = Name;
						}
					}
				}

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

				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  table_schema,
        table_name,
        column_name,
        ordinal_position,
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
							string Type = (string) rdr["data_type"];

							if (!Result.TablesDict.TryGetValue (SchemaEntity.GetDisplay (Schema, TableName), out DbTable t))
							{
								continue;
							}

							NamedTyped c = new NamedTyped (ColumnName, Result.TypeMap.GetForSqlTypeName(Type));
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

							char[] ArgModes = rdr["proargmodes"] as char[];
							if (ArgModes != null)
							{
								string[] ArgNames = (string[])rdr["proargnames"];
								uint[] ArgTypeCodes = (uint[])rdr["proallargtypes"];

								Argument.DirectionType[] ArgDirections = ArgModes.Select (c =>
									c == 'b' ? Argument.DirectionType.InOut : Argument.DirectionType.In).ToArray ();
								string[] ArgTypes = ArgTypeCodes.Select (n => TypesDict[n]).ToArray ();

								foreach (var arg in ArgNames.Indexed ())
								{
									string Type = ArgTypes[arg.Index];
									PSqlType SqlType;

									if (Type.StartsWith ("_"))		// here: user pg_catalog.pg_type.typelem for proper item type reference
									{
										SqlType = Result.TypeMap.GetForSqlTypeName (Type[1..])?.ArrayType;
									}
									else
									{
										SqlType = Result.TypeMap.GetForSqlTypeName (Type);
									}

									Argument c = new Argument (arg.Value, SqlType, ArgDirections[arg.Index]);
									p.AddArgument (c);
								}
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
SELECT routines.routine_schema, routines.routine_name, type_udt_name::regtype::varchar AS data_type
FROM information_schema.routines
WHERE routines.routine_type='FUNCTION'
    AND type_udt_name NOT IN ('any')
ORDER BY routines.routine_schema, routines.routine_name;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							// presumed to be lowercase
							string Schema = (string)rdr["routine_schema"];
							string RoutineName = (string)rdr["routine_name"];
							string TypeName = (string)rdr["data_type"];

							PSqlType Type = Result.TypeMap.GetForSqlTypeName (TypeName);
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

			return Result;
		}
	}
}
