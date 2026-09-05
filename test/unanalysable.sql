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
-- Pinned in the database rather than left to the session, because the analyzer reads
-- SHOW search_path over its own connection. Without this the fixture inherits whatever
-- the developer's role happens to carry, and every case here that needs a bare name to
-- resolve fails for a reason the fixture never intended.
ALTER ROLE :owner IN DATABASE :dbname SET search_path TO :owner;
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

-- A call to a function that resolves to nothing, in a position where its type becomes a
-- result column. No such function exists here; a function whose schema the search_path
-- does not reach resolves the same way, and is the likelier way to meet this. The column
-- type is unknown, so the procedure is dropped rather than reported with a guess.
CREATE PROCEDURE proc_unresolved_function (INOUT r refcursor)
LANGUAGE 'plpgsql' AS $$
BEGIN
    OPEN r FOR
    -- # 1
    SELECT no_such_function () AS v;
END;
$$;

-- A call to a function the database really HAS, whose return type the analyzer has no
-- mapping for. The column type is unknown for a different reason than above, and the two
-- must not report the same way: this one is fixed by teaching the type map, and pointing
-- its reader at the search_path points away from the fix. `bit varying` is used because
-- it is in every PostgreSQL without an extension and the type map does not cover it; if
-- it gains a mapping, swap in another unmapped type rather than deleting the case.
CREATE FUNCTION fn_unmapped_return () RETURNS bit varying
LANGUAGE 'sql' AS $$ SELECT B'101'::bit varying; $$;

CREATE PROCEDURE proc_unmapped_return_type (INOUT r refcursor)
LANGUAGE 'plpgsql' AS $$
BEGIN
    OPEN r FOR
    -- # 1
    SELECT fn_unmapped_return () AS v;
END;
$$;
