#!/bin/bash

set -e -u

while read KW;
do
    sudo -u postgres psql -q -t -c "SELECT '[\"$KW\"]' || ' = \"' || pg_typeof($KW) || '\",';" 2>/dev/null
done <<<"$(cat expression_keywords.txt)"
