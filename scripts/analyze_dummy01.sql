\c dummy01;

WITH RECURSIVE C AS
(
    SELECT 5 AS done
)
, R AS
(
    SELECT  id,
            id_parent,
            name,
            1 AS "order"
    FROM Depts
    WHERE id = 9

    UNION ALL

    SELECT  D.id,
            D.id_parent,
            D.name,
            R."order" + 1
    FROM R
        INNER JOIN Depts AS D ON D.id = R.id_parent
)
SELECT  id,
        name
FROM R
ORDER BY R.order;

SELECT  table_schema,
        table_name
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema');

SELECT * FROM ext.Persons;

call RoomsForPerson('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A');

SELECT  table_schema,
        table_name,
        column_name,
        ordinal_position,
        data_type,
        udt_name::regtype
FROM information_schema.columns
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, ordinal_position;

SELECT  /*proname,
        pronargs,
        prorettype,
        --proargtypes,
        --proallargtypes,
        --proargmodes,
        --proargnames,
        prosrc
        */
        n.nspname as schema,
        p.proname as name,
        p.prosrc
FROM pg_catalog.pg_namespace n
        INNER JOIN pg_catalog.pg_proc p ON pronamespace = n.oid
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
    ;

select proc.specific_schema as procedure_schema,
       --proc.specific_name,
       proc.routine_name as procedure_name,
       --proc.external_language,
       args.parameter_mode,
       args.parameter_name,
       args.data_type
from information_schema.routines proc
left join information_schema.parameters args
          on proc.specific_schema = args.specific_schema
          and proc.specific_name = args.specific_name
where proc.routine_schema not in ('pg_catalog', 'information_schema')
      and proc.routine_type = 'PROCEDURE'
order by procedure_schema,
         proc.specific_name,
         procedure_name,
         args.ordinal_position;

SELECT routines.routine_schema, routines.routine_name, data_type
FROM information_schema.routines
WHERE routines.routine_type='FUNCTION'
ORDER BY routines.routine_schema, routines.routine_name;

SHOW search_path;
