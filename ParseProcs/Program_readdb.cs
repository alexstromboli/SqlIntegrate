using System.Collections.Generic;

using Npgsql;

namespace ParseProcs
{
	partial class Program
	{
		private static void ReadDatabase (string ConnectionString,
			Dictionary<string, Table> TablesDict,
			Dictionary<string, Procedure> ProceduresDict)
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

							if (!TablesDict.TryGetValue (Table.GetDisplay (Schema, TableName), out Table t))
							{
								continue;
							}

							Column c = new Column (ColumnName, PSqlType.Map[Type]);
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
							string TableName = (string) rdr["procedure_name"];
							string ArgumentDirection = (string) rdr["parameter_mode"];
							string ArgumentName = (string) rdr["parameter_name"];
							string Type = (string) rdr["data_type"];

							if (!ProceduresDict.TryGetValue (SchemaEntity.GetDisplay (Schema, TableName),
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
			}
		}
	}
}