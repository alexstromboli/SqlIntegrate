using System.Linq;
using System.Collections.Generic;

using Npgsql;

namespace ParseProcs
{
	partial class Program
	{
		private static void ReadDatabase (string ConnectionString,
			Dictionary<string, Table> TablesDict,
			Dictionary<string, Procedure> ProceduresDict,
			Dictionary<string, PSqlType> FunctionsDict,
			List<string> SchemaOrder
			)
		{
			using (var conn = new NpgsqlConnection (ConnectionString))
			{
				conn.Open ();

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

							Table t = new Table (Schema, Name);
							TablesDict[t.Display] = t;
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
        data_type
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

							if (!TablesDict.TryGetValue (SchemaEntity.GetDisplay (Schema, TableName), out Table t))
							{
								continue;
							}

							NamedTyped c = new NamedTyped (ColumnName, PSqlType.Map[Type]);
							t.AddColumn (c);
						}
					}
				}

				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  n.nspname as schema,
        p.proname as name,
        p.prosrc
FROM pg_catalog.pg_namespace n
        INNER JOIN pg_catalog.pg_proc p ON pronamespace = n.oid
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
    ;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							string Schema = (string) rdr["schema"];
							string Name = (string) rdr["name"];
							string SourceCode = (string) rdr["prosrc"];

							Procedure p = new Procedure (Schema, Name, SourceCode);
							ProceduresDict[p.Display] = p;
						}
					}
				}

				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT proc.specific_schema AS procedure_schema,
       proc.routine_name AS procedure_name,
       args.parameter_mode,
       args.parameter_name,
       args.data_type
FROM information_schema.routines proc
	INNER JOIN information_schema.parameters args
          ON proc.specific_schema = args.specific_schema
          	AND proc.specific_name = args.specific_name
WHERE proc.routine_type = 'PROCEDURE'
ORDER BY procedure_schema,
         proc.specific_name,
         procedure_name,
         args.ordinal_position
         ;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							string Schema = (string) rdr["procedure_schema"];
							string ProcedureName = (string) rdr["procedure_name"];
							string ArgumentDirection = (string) rdr["parameter_mode"];
							string ArgumentName = (string) rdr["parameter_name"];
							string Type = (string) rdr["data_type"];

							if (!ProceduresDict.TryGetValue (SchemaEntity.GetDisplay (Schema, ProcedureName),
								out Procedure p))
							{
								continue;
							}

							Argument.DirectionType Direction = ArgumentDirection == "INOUT"
									? Argument.DirectionType.InOut
									: Argument.DirectionType.In
								;

							Argument c = new Argument (ArgumentName, PSqlType.Map[Type], Direction);
							p.AddArgument (c);
						}
					}
				}

				//
				string SchemaPath = (string)conn.ExecuteScalar ("SHOW search_path;");

				SchemaOrder.AddRange (
					SchemaPath.Split (',')
						.Select (s => s.Trim (' ', '"'))
						.Where (s => !string.IsNullOrWhiteSpace (s))
				);
				SchemaOrder.Add ("pg_catalog");

				for (int i = 0; i < SchemaOrder.Count; ++i)
				{
					string s = SchemaOrder[i];
					if (s.StartsWith ('$'))
					{
						s = (string)conn.ExecuteScalar ("SELECT " + s.Substring (1) + ";");
						SchemaOrder[i] = s;
					}
				}

				//
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT routines.routine_schema, routines.routine_name, data_type
FROM information_schema.routines
WHERE routines.routine_type='FUNCTION'
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

							if (!PSqlType.Map.TryGetValue (TypeName, out PSqlType Type))
							{
								continue;
							}

							string QualName = PSqlUtils.PSqlQualifiedName (Schema, RoutineName);
							FunctionsDict[QualName] = Type;
						}
					}
				}
			}
		}
	}
}
