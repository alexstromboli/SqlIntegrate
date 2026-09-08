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

-- The overloads of one name are separate functions, and which of them a call site means
-- is decided from the argument types written beside it. So an overload appearing or
-- disappearing moves this procedure's inferred column type while its own source, the
-- tables and the custom types all stay put -- a second input the cache key cannot carry,
-- alongside a callee's changed return type.
--
-- The call names an int, which reaches a bigint argument through a cast PostgreSQL makes
-- without being asked. An int overload added beside it names the argument outright and
-- takes the call.

CREATE FUNCTION overloaded (arg bigint) RETURNS bigint
AS $$ BEGIN RETURN arg; END $$ LANGUAGE plpgsql;

CREATE PROCEDURE overload_caller (INOUT res refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN res FOR
    -- # 1
    SELECT overloaded (1) AS v;
END
$$;

-- The other half of the same dependency: a cache entry may only be served if the
-- argument types it recorded still resolve to what it recorded. date_trunc is the callee
-- because its overloads are the database's own -- four of them, three return types, and a
-- catalogue order PostgreSQL fixes rather than this fixture -- so the answer the arguments
-- give (timestamptz) is not the answer the name alone gives (timestamp, the overload that
-- orders last). An entry replayed without its argument types therefore disagrees with
-- itself and is refused on every run, which is a cache that never hits.

CREATE PROCEDURE builtin_overload_caller (INOUT res refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN res FOR
    -- # 1
    SELECT date_trunc ('day', now ()) AS v;
END
$$;
