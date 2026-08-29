-- Procedures the analyzer cannot turn into report entries.
--
-- Kept in a database of its own, separate from dummy01: ParseProcs analyses every
-- procedure in the database it is pointed at, so putting these beside the corpus would
-- fold them into the report that is compared against correct_output.json.
--
-- The point of the fixture is that a procedure the analyzer drops must make the run
-- fail. It needs one case per reachable kind of drop; if a construct here gains support,
-- replace it with another the grammar still rejects rather than deleting the case.

CREATE SCHEMA :owner;
SET search_path TO :owner;

CREATE TABLE t (id int, val int);

-- Unsupported syntax: dropped by the parse-failure path.
CREATE PROCEDURE proc_unparsable ()
LANGUAGE 'plpgsql' AS $$
BEGIN
    MERGE INTO t USING t AS s ON t.id = s.id
    WHEN MATCHED THEN UPDATE SET val = s.val;
END;
$$;

-- Overloading is not supported, and the whole name group is dropped -- not just the
-- extra overload -- so neither of these two reaches the report.
CREATE PROCEDURE proc_overloaded (a int)
LANGUAGE 'plpgsql' AS $$ BEGIN UPDATE t SET val = a; END; $$;

CREATE PROCEDURE proc_overloaded (a text)
LANGUAGE 'plpgsql' AS $$ BEGIN UPDATE t SET val = 1; END; $$;

-- A procedure that analyses cleanly, so the run is not failing for want of anything
-- analysable at all.
CREATE PROCEDURE proc_analysable (INOUT r refcursor)
LANGUAGE 'plpgsql' AS $$ BEGIN OPEN r FOR SELECT t.id FROM t; END; $$;
