using System;
using System.Collections.Generic;

using Npgsql;

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

	public abstract class NamedTyped
	{
		public string Name { get; protected set; }
		public PSqlType Type { get; protected set; }
		public override string ToString () => $"{Name} {Type.Display}";

		public NamedTyped (string Name, PSqlType Type)
		{
			this.Name = Name.ToLower ();
			this.Type = Type;
		}
	}

	public class Column : NamedTyped
	{
		public Column (string Name, PSqlType Type)
			: base (Name, Type)
		{
		}
	}

	public class Argument : NamedTyped
	{
		public enum DirectionType
		{
			In,
			InOut
		}
		
		public DirectionType Direction { get; protected set; }
		
		public Argument (string Name, PSqlType Type, DirectionType Direction)
			: base (Name, Type)
		{
			this.Direction = Direction;
		}

		public override string ToString ()
		{
			return base.ToString () + (Direction == DirectionType.InOut ? " INOUT" : "");
		}
	}

	public abstract class SchemaEntity
	{
		public string Schema { get; protected set; }
		public string Name { get; protected set; }

		public static string GetDisplay (string Schema, string Name)
		{
			return $"{Schema}.{Name}";
		}
		
		public string Display { get; protected set; }
		public override string ToString () => Display;

		public SchemaEntity (string Schema, string Name)
		{
			this.Schema = Schema.ToLower ();
			this.Name = Name.ToLower ();
			Display = GetDisplay (Schema, Name);
		}
	}

	public class Table : SchemaEntity
	{
		protected List<Column> _Columns;
		public IReadOnlyList<Column> Columns => _Columns;

		protected Dictionary<string, Column> _ColumnsDict;
		public IReadOnlyDictionary<string, Column> ColumnsDict => _ColumnsDict;

		public Table (string Schema, string Name)
		: base (Schema, Name)
		{
			_Columns = new List<Column> ();
			_ColumnsDict = new Dictionary<string, Column> ();
		}

		public Column AddColumn (Column Column)
		{
			if (_ColumnsDict.TryGetValue (Column.Name, out Column Existing))
			{
				return Existing;
			}
			
			_Columns.Add (Column);
			_ColumnsDict[Column.Name] = Column;

			return Column;
		}
	}

	public class Procedure : SchemaEntity
	{
		public string SourceCode { get; protected set; }

		protected List<Argument> _Arguments;
		public IReadOnlyList<Argument> Arguments => _Arguments;

		protected Dictionary<string, Argument> _ArgumentsDict;
		public IReadOnlyDictionary<string, Argument> ArgumentsDict => _ArgumentsDict;

		public Procedure (string Schema, string Name, string SourceCode)
			: base (Schema, Name)
		{
			this.SourceCode = SourceCode;
			_Arguments = new List<Argument> ();
			_ArgumentsDict = new Dictionary<string, Argument> ();
		}

		public Argument AddArgument (Argument Argument)
		{
			if (_ArgumentsDict.TryGetValue (Argument.Name, out Argument Existing))
			{
				return Existing;
			}
			
			_Arguments.Add (Argument);
			_ArgumentsDict[Argument.Name] = Argument;

			return Argument;
		}
	}
	
	class Program
	{
		static void Main (string[] args)
		{
			Dictionary<string, Table> TablesDict = new Dictionary<string, Table> ();
			Dictionary<string, Procedure> ProceduresDict = new Dictionary<string, Procedure> ();
			
			using (var conn = new NpgsqlConnection ("server=127.0.0.1;port=5432;database=dummy01;uid=postgres;pwd=Yakunichev"))
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

							if (!ProceduresDict.TryGetValue (SchemaEntity.GetDisplay (Schema, TableName), out Procedure p))
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
