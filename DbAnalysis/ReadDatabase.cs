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
				FunctionsDict = new Dictionary<string, List<DbFunction>> (),
				ImplicitCasts = new HashSet<(string Source, string Target)> (),
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

				// Types PostgreSQL coerces between without being asked. Overload resolution
				// needs them because a call site rarely names an argument's declared type
				// exactly: a string literal reaches date_trunc as varchar where the
				// argument is declared text, and an overload rejected over that would
				// leave the arguments deciding nothing. Keyed on the two types' display
				// names, which is what both sides of a resolution have to hand.
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = @"
SELECT  casts.castsource,
        casts.casttarget
FROM pg_catalog.pg_cast AS casts
WHERE casts.castcontext = 'i'
;
";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							uint SourceOid = (uint)rdr["castsource"];
							uint TargetOid = (uint)rdr["casttarget"];

							if (Result.TypeMap.MapByOid.TryGetValue (SourceOid, out PSqlType Source)
							    && Result.TypeMap.MapByOid.TryGetValue (TargetOid, out PSqlType Target)
							    )
							{
								Result.ImplicitCasts.Add ((Source.Display, Target.Display));
							}
						}
					}
				}

				//
				using (var cmd = conn.CreateCommand ())
				{
					// Every function is read, including the ones whose return type will not
					// map. A function the database has must not be missing from the analyzer's
					// view for the same reason a name that matches nothing is absent -- the two
					// are then indistinguishable downstream, and the advice for them differs.
					// 'any' and 'citext' are spelled out because ::regtype is not a total
					// function over type_udt_name: those two would raise rather than return a
					// name the type map can be asked about and decline.
					//
					// typtype tells a pseudo-type from a real one, and it is read here because
					// a pseudo-type is not a type a value can be carried in: a call that
					// resolves to a function returning anyelement, anyarray, record or void
					// yields a column no wrapper can declare. Joined by name and schema
					// rather than by the routine's oid, which information_schema does not
					// expose in a form worth splitting out of specific_name.
					//
					// The declared argument types come from pg_proc, which specific_name is
					// what reaches: it is the routine's name and its oid joined by an
					// underscore, so the trailing digits are the oid whatever the name
					// contains. proargtypes is the input arguments alone, which is what a
					// call site names; pronargdefaults and provariadic are how many of
					// them the call may leave out and whether the last one absorbs the rest.
					//
					// specific_name orders the overloads of one name, which the two key
					// columns leave tied. It is the last-resort tie-break in resolution, so a
					// call whose arguments genuinely decide nothing still answers the same way
					// on every run rather than by the planner's choice -- reproducible output
					// being the whole reason the analysis is comparable at all.
					cmd.CommandText = @"
SELECT
	routines.routine_schema,
    routines.routine_name,
    routines.type_udt_schema AS result_schema,
    CASE WHEN routines.type_udt_name IN ('any', 'citext')
        THEN routines.type_udt_name
        ELSE routines.type_udt_name::regtype::varchar
    END AS result_type,
    COALESCE (rettype.typtype = 'p', false) AS result_is_pseudo,
    proc.proargtypes AS arg_type_oids,
    COALESCE (proc.pronargdefaults, 0) AS arg_default_count,
    COALESCE (proc.provariadic <> 0, false) AS arg_is_variadic
FROM information_schema.routines
LEFT JOIN pg_catalog.pg_namespace retschema
    ON retschema.nspname = routines.type_udt_schema
LEFT JOIN pg_catalog.pg_type rettype
    ON rettype.typname = routines.type_udt_name
    AND rettype.typnamespace = retschema.oid
LEFT JOIN pg_catalog.pg_proc proc
    ON proc.oid = SUBSTRING (routines.specific_name FROM '_([0-9]+)$')::oid
WHERE routines.routine_type='FUNCTION'
ORDER BY routines.routine_schema, routines.routine_name, routines.specific_name;
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

							bool ResultIsPseudo = (bool)rdr["result_is_pseudo"];

							string QualName = PSqlUtils.PSqlQualifiedName (Schema, RoutineName);

							PSqlType Type = ResultIsPseudo
								? null
								: Result.GetTypeForName (ResultSchema, ResultType);

							uint[] ArgTypeOids = (rdr["arg_type_oids"] as uint[]) ?? Array.Empty<uint> ();

							DbFunction Function = new DbFunction
							{
								QualifiedName = QualName,
								ReturnType = Type,
								// The function exists; it is its return type nothing can carry
								// a value of -- either the type map has no mapping for it, or
								// it is a pseudo-type, which no mapping could describe. Naming
								// the type is what lets a caller that found no type tell "no
								// such function" from "no usable type here", and act on it.
								UnmappedReturnTypeName = Type == null ? ResultType : null,
								Arguments = ArgTypeOids
									.Select (Oid => new DbFunctionArgument
									{
										Type = Result.TypeMap.MapByOid.TryGetValue (Oid, out PSqlType ArgType)
											? ArgType
											: null,
										IsPseudo = PgTypeEntriesDict.TryGetValue (Oid, out PgTypeEntry ArgEntry)
										           && ArgEntry.Category == 'p'
									})
									.ToArray (),
								DefaultCount = Convert.ToInt32 (rdr["arg_default_count"]),
								IsVariadic = (bool)rdr["arg_is_variadic"]
							};

							if (!Result.FunctionsDict.TryGetValue (QualName, out List<DbFunction> Overloads))
							{
								Overloads = new List<DbFunction> ();
								Result.FunctionsDict[QualName] = Overloads;
							}

							Overloads.Add (Function);
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
