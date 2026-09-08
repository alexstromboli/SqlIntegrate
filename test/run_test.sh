#!/bin/bash

set -e -u

cd "$(dirname "$(realpath "${BASH_SOURCE[0]}")")"

export DBNAME=dummy01
export BAD_DBNAME=dummy01_unanalysable
export CALLEE_DBNAME=dummy01_callee
export USER="$(whoami)"

if [ "${1:-}" != '-c' ]; then
    psql -d postgres -c "DROP DATABASE $DBNAME;" || true
    psql -d postgres -c "CREATE DATABASE $DBNAME;"

    psql -q -d "$DBNAME" -v dbname="$DBNAME" -v owner="$USER" -f dummy01.sql
    cat dummy01_2.sql | sed "s/SCHEMA/$USER/g" | sed "s/DBNAME/$DBNAME/g" | psql -q -d "$DBNAME"

    psql -d postgres -c "DROP DATABASE $BAD_DBNAME;" || true
    psql -d postgres -c "CREATE DATABASE $BAD_DBNAME;"

    psql -q -d "$BAD_DBNAME" -v owner="$USER" -v dbname="$BAD_DBNAME" -f unanalysable.sql

    psql -d postgres -c "DROP DATABASE $CALLEE_DBNAME;" || true
    psql -d postgres -c "CREATE DATABASE $CALLEE_DBNAME;"

    psql -q -d "$CALLEE_DBNAME" -v owner="$USER" -v dbname="$CALLEE_DBNAME" -f callee_signature.sql
fi

PARSEPROCS_EXE="../ParseProcs/bin/Debug/net10.0/ParseProcs"
TESTWRAPPER_EXE="../TestWrapper/bin/Debug/net10.0/TestWrapper"

# Rebuild if either executable is missing or older than any source .cs file. Both are
# run below, so both have to be current: running a stale one reports on a generator
# that is not the one being tested.
if [ ! -f "$PARSEPROCS_EXE" ] || [ ! -f "$TESTWRAPPER_EXE" ] || \
        [ -n "$(find ../DbAnalysis ../ParseProcs ../Wrapper ../TestWrapper -name '*.cs' \
        -newer "$PARSEPROCS_EXE" 2>/dev/null | head -1)" ]; then
    dotnet clean ../ParseProcs/ParseProcs.csproj
    dotnet build -c Debug ../ParseProcs/ParseProcs.csproj -v q
    dotnet clean ../TestWrapper/TestWrapper.csproj
    dotnet build -c Debug ../TestWrapper/TestWrapper.csproj -v q
fi

CONN="host=/var/run/postgresql;database=$DBNAME;Integrated Security=true"
BAD_CONN="host=/var/run/postgresql;database=$BAD_DBNAME;Integrated Security=true"
CALLEE_CONN="host=/var/run/postgresql;database=$CALLEE_DBNAME;Integrated Security=true"

FAILED=0

report_ok ()
{
    echo -e "\e[92m""$1""\e[0m"
}

report_failed ()
{
    echo -e "\e[91m""$1""\e[0m"
    FAILED=1
}

### the corpus: the report must match the recorded one

export OUTPUT_JSON_FILE="$(realpath temp_"$(tr -dc a-f0-9 </dev/urandom | dd bs=32 count=1 2>/dev/null)".json)"
# path /var/run/postgresql is taken from section unix_socket_directories
# of /etc/postgresql/12/main/postgresql.conf
"$PARSEPROCS_EXE" --no-cache "$CONN" "$OUTPUT_JSON_FILE"

if [ -f "$OUTPUT_JSON_FILE" ]; then
    sed -i "s/\"Name\": \"indirectly_used_enum\",/\"Name\": \"indirectly_used_enum\", \"GenerateEnum\": true,/g" "$OUTPUT_JSON_FILE"
    sed -i "s/\"Name\": \"monetary\",/\"Name\": \"monetary\", \"Tag\": \"financial\",/g" "$OUTPUT_JSON_FILE"
    sed -i "s/\"Name\": \"city_locale\",/\"Name\": \"city_locale\", \"MapTo\": \"TryWrapper.Town\",/g" "$OUTPUT_JSON_FILE"
    sed -i "s/\"Name\": \"mapped\",/\"Name\": \"mapped\", \"MapTo\": \"TryWrapper.CardType\", \"GenerateEnum\": true,/g" "$OUTPUT_JSON_FILE"

    pushd ../TestWrapper/bin/Debug/net10.0 >/dev/null
    ./TestWrapper --legacy-npgsql "$OUTPUT_JSON_FILE"
    cp dbproc_sch_noda.cs ../../../../TryWrapper
    popd >/dev/null
fi

sed -i "s/\"$USER/\"USER/g" "$OUTPUT_JSON_FILE"

if [ "$(sha1sum correct_output.json | cut -c 1-40)" == "$(sha1sum "$OUTPUT_JSON_FILE" | cut -c 1-40)" ]; then
    report_ok "success"
    rm "$OUTPUT_JSON_FILE"
else
    # Kept under a fixed name so it can be diffed against correct_output.json, and so
    # repeated failures overwrite one file instead of accumulating.
    mv "$OUTPUT_JSON_FILE" temp_actual_output.json
    report_failed "failed: report differs from correct_output.json (see test/temp_actual_output.json)"
fi

### a procedure the analyzer drops must fail the run

# A dropped procedure is absent from the report, and absent is indistinguishable there
# from a procedure the database never had. The exit code is the only thing that can say
# the difference, so it is what this checks.
NEG_JSON_FILE="$(realpath temp_unanalysable.json)"

NEG_OUTPUT="$("$PARSEPROCS_EXE" --no-cache "$BAD_CONN" "$NEG_JSON_FILE" 2>&1)" && NEG_STATUS=0 || NEG_STATUS=$?

if [ "$NEG_STATUS" -eq 0 ]; then
    report_failed "failed: unanalysable procedures exited 0"
elif ! grep -q 'proc_unparsable' <<<"$NEG_OUTPUT" || ! grep -q 'proc_overloaded' <<<"$NEG_OUTPUT" \
        || ! grep -q 'proc_unresolved_function' <<<"$NEG_OUTPUT" \
        || ! grep -q 'proc_pseudo_return_type' <<<"$NEG_OUTPUT"; then
    report_failed "failed: the summary does not name every dropped procedure"
    echo "$NEG_OUTPUT"
else
    report_ok "success: unanalysable procedures exit $NEG_STATUS and are named"
fi

# A drop the reader cannot act on is barely better than a silent one. The likeliest way
# to meet an unresolved function is a search_path that does not reach its schema, so the
# summary has to say that resolution is what failed and which name failed it, rather than
# bucketing the procedure under the catch-all kind.
if grep -q 'proc_unresolved_function: unknown issue' <<<"$NEG_OUTPUT"; then
    report_failed "failed: an unresolved function lands in the unknown-issue bucket"
    echo "$NEG_OUTPUT"
elif ! grep -qE 'proc_unresolved_function: unresolved function .*no_such_function' <<<"$NEG_OUTPUT"; then
    report_failed "failed: the summary does not name the function that did not resolve"
    echo "$NEG_OUTPUT"
else
    report_ok "success: an unresolved function is named as such in the summary"
fi

# The other way a call goes untyped: the function exists, and it is its RETURN TYPE the
# analyzer cannot map. Reported as an unresolved name it sends the reader to check a
# search_path that is already correct, so it needs a kind of its own -- and the type has
# to be named, because the type is what a maintainer adds support for.
if grep -qE 'proc_unmapped_return_type: (unknown issue|unresolved function)' <<<"$NEG_OUTPUT"; then
    report_failed "failed: an unmappable return type is reported as a missing function"
    echo "$NEG_OUTPUT"
elif ! grep -qE 'proc_unmapped_return_type: unmapped return type .*fn_unmapped_return.*bit varying' <<<"$NEG_OUTPUT"; then
    report_failed "failed: the summary does not name the function and the type that has no mapping"
    echo "$NEG_OUTPUT"
else
    report_ok "success: an unmappable return type is told apart from a missing function"
fi

# A pseudo-type return type is the case no mapping could ever cover: anyelement stands
# for whatever type the call site resolved it to, so there is no C# type to map it to and
# nothing to teach the type map. It still reports as an unusable return type rather than
# as a missing function -- the reader's next step is the expression, not the search_path.
if grep -qE 'proc_pseudo_return_type: (unknown issue|unresolved function)' <<<"$NEG_OUTPUT"; then
    report_failed "failed: a pseudo-type return type is reported as a missing function"
    echo "$NEG_OUTPUT"
elif ! grep -qE 'proc_pseudo_return_type: unmapped return type .*fn_pseudo_return.*anyelement' <<<"$NEG_OUTPUT"; then
    report_failed "failed: the summary does not name the function returning a pseudo-type"
    echo "$NEG_OUTPUT"
else
    report_ok "success: a pseudo-type return type is named rather than carried into a column"
fi

"$PARSEPROCS_EXE" --no-cache --tolerate-failures "$BAD_CONN" "$NEG_JSON_FILE" >/dev/null 2>&1 \
    && report_ok "success: --tolerate-failures exits 0" \
    || report_failed "failed: --tolerate-failures did not exit 0"

rm -f "$NEG_JSON_FILE"

### the generator meeting a type it has no C# mapping for

# The analyzer refuses such a type on the way in, so the only way to put one in front of
# the generator is to doctor a report. It is worth doing: the generator looks a type name
# up per column, per argument and per composite property, and a bare dictionary miss
# names the type and nothing else -- not the procedure, not the column -- which is the
# half that says where to go. The type is spelled pg_catalog.anyelement because a
# pseudo-type is the one an overloaded name can put in front of the generator.
DOCTORED_JSON_FILE="$(realpath temp_doctored.json)"
"$PARSEPROCS_EXE" --no-cache "$CONN" "$DOCTORED_JSON_FILE" >/dev/null

sed -i '/"Name": "overloaded_name_resolves_to_a_real_type",/{n;s/"Type": "text"/"Type": "pg_catalog.anyelement"/}' \
    "$DOCTORED_JSON_FILE"

if ! grep -q 'pg_catalog.anyelement' "$DOCTORED_JSON_FILE"; then
    report_failed "failed: the doctored report carries no unmappable type, so the generator is not being tested"
else
    pushd ../TestWrapper/bin/Debug/net10.0 >/dev/null
    GEN_OUTPUT="$(./TestWrapper --legacy-npgsql "$DOCTORED_JSON_FILE" 2>&1)" && GEN_STATUS=0 || GEN_STATUS=$?
    popd >/dev/null

    if [ "$GEN_STATUS" -eq 0 ]; then
        report_failed "failed: the generator emitted code for a type it has no mapping for"
    elif grep -q 'KeyNotFoundException' <<<"$GEN_OUTPUT"; then
        report_failed "failed: an unmappable type reaches the generator as a bare dictionary miss"
        echo "$GEN_OUTPUT"
    elif ! grep -qE 'get_operators.*overloaded_name_resolves_to_a_real_type.*pg_catalog.anyelement' <<<"$GEN_OUTPUT"; then
        report_failed "failed: the generator does not name the procedure, the column and the type"
        echo "$GEN_OUTPUT"
    else
        report_ok "success: an unmappable type is named with the column that carries it"
    fi
fi

rm -f "$DOCTORED_JSON_FILE"

### the analysis cache

# Every other run here passes --no-cache, so without this the cache is never exercised
# at all. HOME is redirected because the cache lives under it and the real one must not
# be disturbed by a test run.
CACHE_HOME="$(realpath temp_cache_home)"
rm -rf "$CACHE_HOME"
mkdir -p "$CACHE_HOME"

HOME="$CACHE_HOME" "$PARSEPROCS_EXE" "$CONN" temp_cache_first.json >/dev/null
HOME="$CACHE_HOME" "$PARSEPROCS_EXE" "$CONN" temp_cache_second.json >/dev/null

if cmp -s temp_cache_first.json temp_cache_second.json; then
    report_ok "success: a cached run reproduces the fresh one"
else
    report_failed "failed: a cached run differs from the fresh one"
fi

# The analyzer's identity is the first segment. Pinning the shape here keeps a later
# refactor from dropping it and quietly reinstating cross-version staleness.
if [ -z "$(ls "$CACHE_HOME"/.sqlintegrate/cache)" ]; then
    report_failed "failed: the cached run wrote no cache entries"
elif ls "$CACHE_HOME"/.sqlintegrate/cache | grep -qvE '^[0-9a-f]{32}_[0-9a-f]{40}_[0-9a-f]{40}\.json$'; then
    report_failed "failed: a cache key does not carry the analyzer, the data layout and the procedure"
else
    report_ok "success: cache keys carry the analyzer's identity"
fi

rm -rf "$CACHE_HOME" temp_cache_first.json temp_cache_second.json

### the cache and a callee whose signature changed

# A procedure's inferred types depend on the return types of the functions it calls, and
# none of that is visible in the procedure's own source, the tables or the custom types --
# so a cache keyed on those alone hands back the old types and the run still exits 0. The
# callee set is only known after the parse, so it is recorded in the entry and replayed;
# this is what checks the replay actually happens.

CALLEE_HOME="$(realpath temp_callee_home)"
rm -rf "$CALLEE_HOME"
mkdir -p "$CALLEE_HOME"

# Set explicitly rather than taken from the fixture, so the section is idempotent and a
# -c re-run does not start from the return type the previous run left behind.
callee_returns ()
{
    psql -q -d "$CALLEE_DBNAME" -v ON_ERROR_STOP=1 -v owner="$USER" \
        -c "SET search_path TO $USER; DROP FUNCTION callee ();
            CREATE FUNCTION callee () RETURNS $1 AS \$\$ BEGIN RETURN 1; END \$\$ LANGUAGE plpgsql;"
}

callee_returns int
HOME="$CALLEE_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_callee_before.json >/dev/null

callee_returns bigint
HOME="$CALLEE_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_callee_after.json >/dev/null
"$PARSEPROCS_EXE" --no-cache "$CALLEE_CONN" temp_callee_fresh.json >/dev/null

if cmp -s temp_callee_before.json temp_callee_after.json; then
    report_failed "failed: a changed callee signature was served from the cache"
elif ! cmp -s temp_callee_after.json temp_callee_fresh.json; then
    report_failed "failed: the re-analysed report differs from an uncached one"
else
    report_ok "success: a changed callee signature invalidates the entry"
fi

# The check above is also satisfied by a cache that never hits, which would be a
# correct-but-useless one. Doctoring the stored result to something the database cannot
# produce separates the two: the sentinel comes back only if the entry was served.
CALLEE_ENTRY="$(ls "$CALLEE_HOME"/.sqlintegrate/cache/*.json 2>/dev/null | head -1)"

if [ -z "$CALLEE_ENTRY" ]; then
    report_failed "failed: the callee run wrote no cache entry"
else
    sed -i 's/"Name":"v","Type":"bigint"/"Name":"v","Type":"numeric"/' "$CALLEE_ENTRY"
    HOME="$CALLEE_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_callee_hit.json >/dev/null

    if grep -q '"Type": "numeric"' temp_callee_hit.json; then
        report_ok "success: an entry whose callees still match is served"
    else
        report_failed "failed: the entry was re-analysed although its callees are unchanged"
    fi
fi

rm -rf "$CALLEE_HOME" temp_callee_before.json temp_callee_after.json \
    temp_callee_fresh.json temp_callee_hit.json

### the cache and an overload added beside a callee

# Which overload of a name a call site means is decided from the argument types beside it,
# so an overload added next to a function a procedure calls moves the inferred column type
# with nothing the cache key describes having moved. The recorded callee carries the
# argument types it was resolved against; replaying that lookup is what catches this, and
# an entry keyed on the name alone would hand back the type the other overload gave.

OVERLOAD_HOME="$(realpath temp_overload_home)"
rm -rf "$OVERLOAD_HOME"
mkdir -p "$OVERLOAD_HOME"

# Dropped rather than assumed absent, so the section is idempotent and a -c re-run does
# not start from the overload the previous run added.
psql -q -d "$CALLEE_DBNAME" -v ON_ERROR_STOP=1 \
    -c "SET search_path TO $USER; DROP FUNCTION IF EXISTS overloaded (int);"

HOME="$OVERLOAD_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_overload_before.json >/dev/null

psql -q -d "$CALLEE_DBNAME" -v ON_ERROR_STOP=1 \
    -c "SET search_path TO $USER;
        CREATE FUNCTION overloaded (arg int) RETURNS int
            AS \$\$ BEGIN RETURN arg; END \$\$ LANGUAGE plpgsql;"

HOME="$OVERLOAD_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_overload_after.json >/dev/null
"$PARSEPROCS_EXE" --no-cache "$CALLEE_CONN" temp_overload_fresh.json >/dev/null

if cmp -s temp_overload_before.json temp_overload_after.json; then
    report_failed "failed: an overload added beside a callee was served from the cache"
elif ! cmp -s temp_overload_after.json temp_overload_fresh.json; then
    report_failed "failed: the re-analysed report differs from an uncached one"
else
    report_ok "success: an overload added beside a callee invalidates the entry"
fi

# The check above is also satisfied by a cache that never hits. Doctoring a stored result
# to something the database cannot produce separates the two -- and the procedure doctored
# is the one calling a built-in whose overloads the arguments and the catalogue order
# disagree about, so the sentinel comes back only if the replay consulted the argument
# types the entry recorded.
OVERLOAD_ENTRY="$(grep -l 'builtin_overload_caller' "$OVERLOAD_HOME"/.sqlintegrate/cache/*.json 2>/dev/null | head -1)"

if [ -z "$OVERLOAD_ENTRY" ]; then
    report_failed "failed: the overload run wrote no cache entry for the built-in caller"
else
    sed -i 's/"Name":"v","Type":"timestamptz"/"Name":"v","Type":"numeric"/' "$OVERLOAD_ENTRY"
    HOME="$OVERLOAD_HOME" "$PARSEPROCS_EXE" "$CALLEE_CONN" temp_overload_hit.json >/dev/null

    if grep -q '"Type": "numeric"' temp_overload_hit.json; then
        report_ok "success: an entry is replayed against the argument types it recorded"
    else
        report_failed "failed: an entry was re-analysed although its overloaded call still resolves the same way"
    fi
fi

rm -rf "$OVERLOAD_HOME" temp_overload_before.json temp_overload_after.json \
    temp_overload_fresh.json temp_overload_hit.json

exit "$FAILED"
