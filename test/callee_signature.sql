-- A procedure whose inferred column type comes from a function it calls.
--
-- Kept in a database of its own, separate from dummy01: the corpus is compared
-- byte-for-byte against correct_output.json, and this fixture's whole point is that its
-- schema is mutated part-way through the run.
--
-- The procedure's own source, the tables and the custom types all stay fixed while the
-- callee's return type changes, so nothing the cache key describes moves. What the
-- report says about column "v" is therefore entirely a statement about whether the
-- analysis tracked the callee.

ALTER ROLE :owner IN DATABASE :dbname SET search_path TO :owner;
CREATE SCHEMA :owner;
SET search_path TO :owner;

CREATE FUNCTION callee () RETURNS int
AS $$ BEGIN RETURN 1; END $$ LANGUAGE plpgsql;

CREATE PROCEDURE caller (INOUT res refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN res FOR
    -- # 1
    SELECT callee () AS v;
END
$$;
