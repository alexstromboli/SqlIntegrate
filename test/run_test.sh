#!/bin/bash

set -e -u

cd "$(dirname "$(realpath "${BASH_SOURCE[0]}")")"

export DBNAME=dummy01
export BAD_DBNAME=dummy01_unanalysable
export USER="$(whoami)"

if [ "${1:-}" != '-c' ]; then
    psql -d postgres -c "DROP DATABASE $DBNAME;" || true
    psql -d postgres -c "CREATE DATABASE $DBNAME;"

    psql -q -d "$DBNAME" -v dbname="$DBNAME" -v owner="$USER" -f dummy01.sql
    cat dummy01_2.sql | sed "s/SCHEMA/$USER/g" | sed "s/DBNAME/$DBNAME/g" | psql -q -d "$DBNAME"

    psql -d postgres -c "DROP DATABASE $BAD_DBNAME;" || true
    psql -d postgres -c "CREATE DATABASE $BAD_DBNAME;"

    psql -q -d "$BAD_DBNAME" -v owner="$USER" -f unanalysable.sql
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
elif ! grep -q 'proc_unparsable' <<<"$NEG_OUTPUT" || ! grep -q 'proc_overloaded' <<<"$NEG_OUTPUT"; then
    report_failed "failed: the summary does not name every dropped procedure"
    echo "$NEG_OUTPUT"
else
    report_ok "success: unanalysable procedures exit $NEG_STATUS and are named"
fi

"$PARSEPROCS_EXE" --no-cache --tolerate-failures "$BAD_CONN" "$NEG_JSON_FILE" >/dev/null 2>&1 \
    && report_ok "success: --tolerate-failures exits 0" \
    || report_failed "failed: --tolerate-failures did not exit 0"

rm -f "$NEG_JSON_FILE"

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

exit "$FAILED"
