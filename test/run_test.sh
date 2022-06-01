#!/bin/bash

set -e -u

cd "$(dirname "$(realpath "${BASH_SOURCE[0]}")")"

export DBNAME=dummy01
export USER="$(whoami)"

psql -d postgres -c "DROP DATABASE $DBNAME;" || true
psql -d postgres -c "CREATE DATABASE $DBNAME;"

psql -q -d "$DBNAME" -v owner="$USER" -f dummy01.sql
cat dummy01_2.sql | sed "s/SCHEMA/$USER/g" | sed "s/DBNAME/$DBNAME/g" | psql -q -d "$DBNAME"

export OUTPUT_JSON_FILE="$(realpath temp_"$(tr -dc a-f0-9 </dev/urandom | dd bs=32 count=1 2>/dev/null)".json)"
# path /var/run/postgresql is taken from section unix_socket_directories
# of /etc/postgresql/12/main/postgresql.conf
../ParseProcs/bin/Debug/net5.0/ParseProcs "host=/var/run/postgresql;database=$DBNAME;Integrated Security=true" "$OUTPUT_JSON_FILE"

if [ -f "$OUTPUT_JSON_FILE" ]; then
    pushd ../MakeWrapper/bin/Debug/net5.0 >/dev/null
    ./MakeWrapper "$OUTPUT_JSON_FILE"
    cp dbproc_sch_noda.cs ../../../../TryWrapper
    popd >/dev/null
fi

sed -i "s/\"$USER/\"USER/g" "$OUTPUT_JSON_FILE"
[ "$(sha1sum correct_output.json | cut -c 1-40)" == "$(sha1sum $OUTPUT_JSON_FILE | cut -c 1-40)" ] && echo -e "\e[92m""success""\e[0m" && rm "$OUTPUT_JSON_FILE" || echo -e "\e[91m""failed""\e[0m"
