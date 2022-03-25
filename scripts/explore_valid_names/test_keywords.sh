#!/bin/bash

# get all Postgres keywords that do not work as table column name

set -e -u

# SELECT *
# FROM pg_get_keywords()

while read KW;
do
    #sudo -u postgres psql -q -d dummy01 -c 'DROP TABLE test_kw;' 2>/dev/null >/dev/null || true

    # column name
    # WHERE catcode IN ('R', 'T')
    #(sudo -u postgres psql -q -d dummy01 -c "CREATE TABLE test_kw ( $KW int );" 2>/dev/null >/dev/null) || echo "$KW"

    # schema
    # WHERE catcode IN ('R', 'T')
    #(sudo -u postgres psql -q -d dummy01 -c "CREATE SCHEMA $KW; DROP SCHEMA $KW;" 2>/dev/null >/dev/null) || echo "$KW"

    # simple column alias
    # none are good
    #(sudo -u postgres psql -q -d dummy01 -c "SELECT id_person $KW FROM own;" 2>/dev/null >/dev/null) || echo "$KW"

    # 'as' column alias
    # all are good
    #(sudo -u postgres psql -q -d dummy01 -c "SELECT id_person AS $KW FROM own;" 2>/dev/null >/dev/null) || echo "$KW"

    # simple table alias
    # WHERE catcode IN ('R', 'T')
    #(sudo -u postgres psql -q -d dummy01 -c "SELECT id_person FROM own $KW;" 2>/dev/null >/dev/null) || echo "$KW"

    # 'as' table alias
    # WHERE catcode IN ('R', 'T')
    #(sudo -u postgres psql -q -d dummy01 -c "SELECT id_person FROM own AS $KW;" 2>/dev/null >/dev/null) || echo "$KW"

    # column table prefix
    # WHERE catcode IN ('R', 'T')
    #(sudo -u postgres psql -q -d dummy01 -c "SELECT $KW.id_person FROM own AS \"$KW\";" 2>/dev/null >/dev/null) || echo "$KW"

    # function name
    # WHERE catcode IN ('R', 'C')
    #   or type name
    #   or some other words
    #(sudo -u postgres psql -q -d dummy01 -c "CREATE FUNCTION $KW() RETURNS int AS \$\$ BEGIN RETURN 5; END \$\$ LANGUAGE plpgsql; DROP FUNCTION $KW;" 2>/dev/null >/dev/null) || echo "$KW"

    # simple expression
    (sudo -u postgres psql -q -d dummy01 -c "SELECT $KW;" 2>/dev/null >/dev/null) || echo "$KW"
done <<<"$(cat list.txt)"
