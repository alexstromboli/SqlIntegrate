CREATE SCHEMA ext;
CREATE SCHEMA no_proc;      -- no procedures in this schema, only types
CREATE SCHEMA :owner;     -- for test, needs to match the username

CREATE TYPE app_status AS ENUM
(
    'pending',
    'active',
    'hold',
    'half-reviewed',
    '13 digits'
);

CREATE TYPE no_proc.package AS ENUM
(
    'open',
    'sealed',
    'enclosed'
);

-- not used (directly or indirectly) in any procedure
CREATE TYPE useless_enum AS ENUM
(
    'okay',
    'taken',
    'rigged'
);

CREATE TYPE indirectly_used_enum AS ENUM
(
    'first',
    'second',
    'put-out',
    '2 digit'
);

CREATE TYPE indirectly_used_type AS
(
    sign char(5),
    is_on bool,
    "order" indirectly_used_enum
);

-- not used (directly or indirectly) in any procedure
CREATE TYPE useless_struct AS
(
    status useless_enum,
    name varchar(50)
);

CREATE TABLE ext.Persons
(
    id uuid PRIMARY KEY,
    lastname varchar(50),
    firstname varchar(50),
    dob date,
    tab_num bigint,
    status app_status,
    effect int DEFAULT 0
);

CREATE TABLE Rooms
(
    id int PRIMARY KEY,
    name varchar(30),
    extents int[]
);

CREATE TABLE Own
(
    id_person uuid,
    id_room int
);

CREATE TABLE Depts
(
    id int,
    id_parent int,
    name varchar(20)
);

CREATE TABLE VoidThings
(
    id serial PRIMARY KEY,
    category varchar(30),
    height int,
    stub int
);

CREATE TYPE monetary AS
(
    amount numeric,
    id_currency int
);

CREATE TYPE payment AS
(
    paid monetary,
    date date,
    indi indirectly_used_type[]
);

CREATE TABLE financial_history
(
    id int,
    diff payment
);

INSERT INTO financial_history VALUES
(
    76,
    (
        (562.30, 2),
        '2019-08-21',
        array[('high', false, 'second'), ('mid', true, 'first')]::indirectly_used_type[]
    )
);

INSERT INTO Depts VALUES (1, null, 'Administration');
INSERT INTO Depts VALUES (2, 1, 'Operation');
INSERT INTO Depts VALUES (3, 1, 'Strategy');
INSERT INTO Depts VALUES (4, 1, 'Legal');
INSERT INTO Depts VALUES (5, 2, 'Accounting');
INSERT INTO Depts VALUES (6, 2, 'Storage');
INSERT INTO Depts VALUES (7, 2, 'HR');
INSERT INTO Depts VALUES (8, 3, 'Marketing');
INSERT INTO Depts VALUES (9, 3, 'Sales');
INSERT INTO Depts VALUES (10, 3, 'SMM');

INSERT INTO ext.Persons VALUES ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 'Borduk',   'Petro', '1946-07-07', 1005, 'active');
INSERT INTO ext.Persons VALUES ('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A', 'Stasuk',   'Pablo', '1952-10-05', null, null);
INSERT INTO ext.Persons VALUES ('BBF7C3E7-408D-485A-B21C-78BA300B0EF1', 'Porosuk',  'Sergio',        null, 1015, 'pending');
INSERT INTO ext.Persons VALUES ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 'Krasuk',   'Stan',  '1984-06-09', 1034, 'active');
INSERT INTO ext.Persons VALUES ('731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA', 'Pevshitz', null,    '1988-12-05', 1089, 'hold');

INSERT INTO Rooms VALUES (2, 'Ontario');
INSERT INTO Rooms VALUES (9, 'Board');
INSERT INTO Rooms VALUES (12, 'Reception');
INSERT INTO Rooms VALUES (13, 'HR');
INSERT INTO Rooms VALUES (20, 'Kitchen');

INSERT INTO Own VALUES ('731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA', 2);
INSERT INTO Own VALUES ('731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA', 12);
INSERT INTO Own VALUES ('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A', 9);
INSERT INTO Own VALUES ('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A', 12);
INSERT INTO Own VALUES ('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A', 20);
INSERT INTO Own VALUES ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 12);
INSERT INTO Own VALUES ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 20);
INSERT INTO Own VALUES ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 2);

-- DROP PROCEDURE Persons_GetAll;
CREATE PROCEDURE Persons_GetAll (INOUT Users refcursor, INOUT ownership refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN Users FOR
    SELECT  ROW_NUMBER() OVER (ORDER BY dob, lastname) AS num,
            *
    FROM ext.Persons
    ORDER BY id
    ;

    OPEN ownership FOR
    SELECT  P.lastname,
            CASE WHEN VALUES.id NOTNULL THEN ROW_NUMBER ( /* number rooms for each owner */ ) OVER (PARTITION BY P.id ORDER BY VALUES.id) END num,
            VALUES.*
    FROM ext.Persons AS P
        LEFT JOIN Own ON Own.id_person = P.id
        LEFT JOIN Rooms VALUES ON VALUES.id = Own.id_room
    ORDER BY P.lastname
    ;
END;
$$;

-- DROP PROCEDURE GetDeptChain;
CREATE PROCEDURE GetDeptChain (p_id int, INOUT res01 refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN res01 FOR
        -- special comment
        /*
            multiline comment
            # 1
            about semantics
        */
    WITH RECURSIVE C AS
        (
            SELECT 5 AS done
        )
        , R AS
        (
            SELECT  id,
                    id_parent,
                    name,
                    C.done AS "order"
            FROM Depts
                CROSS JOIN C
            WHERE id = GetDeptChain.id

            UNION ALL

            SELECT  D.id,
                    D.id_parent,
                    D.name,
                    R."order" + 1
            FROM R
                     INNER JOIN Depts AS D ON D.id = R.id_parent
        )
    SELECT  id,
            name,
            p_id as float
    FROM R
    ORDER BY R.order;
END;
$$;

-- DROP PROCEDURE ext.Calc;
CREATE PROCEDURE ext.Calc ()
LANGUAGE plpgsql
AS $$
    DECLARE
        dt date;
        over int := 9;
        "g h" real;
        bwahaha int[];
    BEGIN
        dt := now();
        --SELECT 6 as done;
        --SELECT 't' as over;

        SELECT name
        FROM shifts, unions u;      -- join through comma
    END;
$$;

CREATE FUNCTION ext.Sum (a money, b money)
RETURNS decimal
AS $$
    BEGIN
        RETURN a + b;
    END
$$ LANGUAGE plpgsql;

CREATE FUNCTION :owner.Sum (a money, b money)
    RETURNS bigint
AS $$
    BEGIN
        RETURN (a + b * 1.3)::decimal::bigint;
    END
$$ LANGUAGE plpgsql;

-- DROP PROCEDURE RoomsForPerson;
CREATE PROCEDURE RoomsForPerson (
        id_person uuid,
        INOUT res01 refcursor,
        INOUT res02 refcursor,
        bwahaha int[],
        INOUT get_array int[],
        name varchar(100) = 'none',
        over bool = true,
        dt01 date = null,
        dt02 timestamp = null,
        dt03 interval = null,
        dt04 time = null,
        txt text = null,
        amount money = null,
        INOUT came bigint = 115,
        INOUT done char(5) = 'emp'      -- argument type read as bpchar
        )
LANGUAGE 'plpgsql'
AS $$
    DECLARE
        sample int := 34;       -- try
    BEGIN
        OPEN res01 FOR
        SELECT  id,
                lastname,
                firstname,
                dob,
                tab_num,
                '{5, 8, 2, 0, 1}'::int[] AS them_all,
                ('{5, 8, 2, 0, 1}'::int [/* inside */ ] )[3] AS piece,
                sample,
                sample::reAL,
                sample + 45 AS "had it",
                Persons.status
        FROM ext.Persons
        WHERE id = id_person
        ;

        UPDATE ext.Persons
        SET effect = effect + 1
        WHERE id = id_person
        ;

        /*
            second resultset
        */
        OPEN res02 FOR
        SELECT  R.*,
                '{ 1, 5, 8 }'::int[] AS ord,
                '{"T":"done"}'::json as json
        FROM Own O
            INNER JOIN Rooms AS R ON R.id = O.id_room
        WHERE O.id_person = RoomsForPerson.id_person
        ;

        sample := sample + 1;
        came := 31 + sample;
        done := came::varchar;
        get_array := '{5, 8, 2, 0, 1}'::int[];
    END;
$$;

-- DROP PROCEDURE ext.Empty;
CREATE PROCEDURE ext.Empty ()
LANGUAGE plpgsql
AS $$
    BEGIN
    END;
$$;

-- DROP PROCEDURE get_single_row;
CREATE PROCEDURE get_single_row (INOUT partial refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN partial FOR
            -- # 1
    SELECT  id,
            name,
            name as float
    FROM Rooms;
END;
$$;

-- DROP PROCEDURE get_scalar;
CREATE PROCEDURE get_scalar (INOUT partial refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN partial FOR
            -- # 1
    SELECT  name
    FROM Rooms R
    ORDER BY R.name;
END;
$$;

-- DROP PROCEDURE get_user_and_details;
CREATE PROCEDURE get_user_and_details (INOUT "user" refcursor, INOUT details refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN "user" FOR
            -- # 1
    SELECT  name
    FROM Rooms R;

    OPEN details FOR
    SELECT
        id,
        lastname,
        firstname,
        dob,
        tab_num,
        effect
    FROM ext.Persons;
END;
$$;

-- DROP PROCEDURE get_array;
CREATE PROCEDURE get_array (INOUT names refcursor, INOUT by_person refcursor, INOUT "unnest" refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN names FOR
    SELECT  extents,
            --array[null, null, true, false],   -- nulls in arrays don't come through
            array[true, false],
            array(with r as (select name from Depts) select distinct * from r) as names,
            '{5, 8, 2}'::int[] "order",
            array[7, 3, 1]||12 array_plus_item,
            array[7, 3, 1] || array[4, 0] array_plus_array,
            12||array[7, 3, 1] item_plus_array
    FROM Rooms;

    OPEN by_person FOR
    SELECT  id_person,
            array_agg(id_room)
    FROM Own
    GROUP BY id_person;

    OPEN "unnest" FOR
    SELECT  unnest(array[2, 4, 9]),      -- unnamed (goes 'unnest'), converted to rows
            unnest(array[7, 11]) AS e,   -- named ('e'), converted to rows
            W,  -- simple name
            W.W AS QW   -- self-qualified name
    FROM unnest(array['X', 'Y']) W
    ;
END;
$$;

-- DROP PROCEDURE get_join_single;
CREATE PROCEDURE get_join_single (INOUT "joined" refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN "joined" FOR
    SELECT  DISTINCT ON (documents.id) *
    FROM    (
            VALUES
            (1, 'Test Title'),
            (2, 'Test Title 2')
            ) documents (id, title)
    JOIN    (
            VALUES
            (1, 1, '2006-01-01'::DATE),
            (2, 1, '2007-01-01'::DATE),
            (3, 1, '2008-01-01'::DATE),
            (4, 2, '2009-01-01'::DATE),
            (5, 2, '2010-01-01'::DATE)
            ) updates (id, document_id, date)
    ON      updates.document_id = documents.id
    ORDER BY
            documents.id, updates.date DESC;

    -- insert, returning nothing
    INSERT INTO Own (id_person, id_room)
    SELECT  P.id,
            R.id
    FROM ext.Persons AS P
        CROSS JOIN Rooms AS R
    WHERE P.lastname = 'Pevshitz'
        AND R.name = 'HR'
    ;
END;
$$;

-- DROP PROCEDURE get_inserted;
CREATE PROCEDURE get_inserted (INOUT inserted refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN inserted FOR
    INSERT INTO Own (id_person, id_room)
    SELECT  P.id,
            R.id,
            'literal ''''' literal
    FROM ext.Persons AS P
        CROSS JOIN Rooms AS R
    WHERE P.lastname = 'Pevshitz'
        AND R.name = 'HR'
    RETURNING *
    ;
END;
$$;

-- DROP PROCEDURE get_value_types;
CREATE PROCEDURE get_value_types
(
    INOUT result refcursor,
    INOUT expressions_2 refcursor,
    INOUT nulls refcursor/*,
    INOUT nulled_arrays refcursor*/
)
LANGUAGE 'plpgsql'
AS $$
DECLARE
    owner_sum bigint;
BEGIN
    owner_sum := Sum (100::money, 200::money);

    OPEN result FOR
    -- # 1
    SELECT  2 as int,
            1.5 as numeric,
            1e-3 as numeric_e_neg,
            1.26e+3 as numeric_e_pos,
            1.26e2 as numeric_e_def,
            .238::real as real,
            4.::float as float,
            .238::money as money,
            'n'::character varying as varchar,
            'name'::char(5) given,
            '1983-06-01'::timestamp without time zone remote,
            false as bool,
            pg_typeof(5) regtype,
            'hold'::app_status as last_status,
            array['enclosed', 'sealed']::no_proc.package[] packages,
            owner_sum,
            'open'::no_proc.package AS full_qual,
            'sealed'::"no_proc"."package" AS full_qual_quot,
            'enclosed'::"no_proc".package AS full_qual_quot_2
        ;

    OPEN expressions_2 FOR
    -- # 1
    SELECT  noW() + interval '10 days'+interval'2d' as timestamptz,
            ext.SUM(3::money, 2::money) as money,
            '2020-03-01'::date + '14:50'::interval AS "timestamp 2",
            '2020-03-01'::date + '14:50'::time AS "timestamp 3",
            now() - '2020-03-01'::date AS interval,
            5 > 4 AS bool,
            5 <= 2*3 AnD NOT 4.5 isnull AS "bool 2",
            EXISTS (SELECT 1 FROM Rooms WHERE id = 12) "bool 3",
            'x' || 'y' "varchar 1",
            'x_'||true "varchar 2",
            false||'_y' "varchar 3",
            800 - (select 50::money * 6.1)::numeric::bigint AS bigint,
            5 * 1 betWEEN 1 and 6 + 2 AS "betWEEN 2",
            'ABC'::bytea loop,
            50::money * 6.1 AS "money 2",
            (with rooms as (select id::bigint as name from rooms order by id) select array_agg(name) from rooms)[2],     -- array_agg bigint
            (select array_agg(name) from rooms)[2] array_agg_2,     -- array_agg_2 varchar
            CASE WHEN 6 * ( 2 + 3 ) betWEEN 1 and 6 THEN 'test' WHEN 6 > 5 THEN 'done' ELSE 'none' END  -- "case" varchar
        ;

    OPEN nulls FOR
    -- # 1
    SELECT  null::int as int,
            null::numeric as numeric,
            null::float as float,
            null::real as real,
            null::bigint as bigint,
            null::smallint as smallint,
            null::money as money,
            null::varchar as varchar,
            null::uuid as uuid,
            null::timestamp without time zone as timestamp,
            null::date as date,
            null::bool as bool,
            coalesce(true, false) AS coalesce_first,
            coalesce(null, 1e-2) AS coalesce_second
        ;

    /*
    -- nulls in arrays don't come through
    OPEN nulled_arrays FOR
    SELECT  array[null, 2, 9] as int,
            array[null, 2.5] as numeric,
            array[null, 2.5::float] as float,
            array[null, '7702f204a409546693f71875b263e804'::uuid] as uuid,
            array[null, true, false] as bool
        ;
    */
END;
$$;

-- DROP PROCEDURE get_numeric_types_math;
CREATE PROCEDURE get_numeric_types_math (INOUT result refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    /*
A=(numeric real float int smallint bigint)
for F in "${A[@]}"; do
    for S in "${A[@]}"; do
        echo "1::$F + 1::$S AS res_""$F""_$S,"
    done
done

for F in "${A[@]}"; do
    for S in "${A[@]}"; do
        echo "res_""$F""_$S,"
        echo "pg_typeof(res_""$F""_$S) AS type_""$F""_$S,"
    done
done
    */
    OPEN result FOR
        -- # 1
    WITH R AS
    (
        SELECT
          1::numeric + 1::numeric AS res_numeric_numeric,
          1::numeric + 1::real AS res_numeric_real,
          1::numeric + 1::float AS res_numeric_float,
          1::numeric + 1::int AS res_numeric_int,
          1::numeric + 1::smallint AS res_numeric_smallint,
          1::numeric + 1::bigint AS res_numeric_bigint,
          1::real + 1::numeric AS res_real_numeric,
          1::real + 1::real AS res_real_real,
          1::real + 1::float AS res_real_float,
          1::real + 1::int AS res_real_int,
          1::real + 1::smallint AS res_real_smallint,
          1::real + 1::bigint AS res_real_bigint,
          1::float + 1::numeric AS res_float_numeric,
          1::float + 1::real AS res_float_real,
          1::float + 1::float AS res_float_float,
          1::float + 1::int AS res_float_int,
          1::float + 1::smallint AS res_float_smallint,
          1::float + 1::bigint AS res_float_bigint,
          1::int + 1::numeric AS res_int_numeric,
          1::int + 1::real AS res_int_real,
          1::int + 1::float AS res_int_float,
          1::int + 1::int AS res_int_int,
          1::int + 1::smallint AS res_int_smallint,
          1::int + 1::bigint AS res_int_bigint,
          1::smallint + 1::numeric AS res_smallint_numeric,
          1::smallint + 1::real AS res_smallint_real,
          1::smallint + 1::float AS res_smallint_float,
          1::smallint + 1::int AS res_smallint_int,
          1::smallint + 1::smallint AS res_smallint_smallint,
          1::smallint + 1::bigint AS res_smallint_bigint,
          1::bigint + 1::numeric AS res_bigint_numeric,
          1::bigint + 1::real AS res_bigint_real,
          1::bigint + 1::float AS res_bigint_float,
          1::bigint + 1::int AS res_bigint_int,
          1::bigint + 1::smallint AS res_bigint_smallint,
          1::bigint + 1::bigint AS res_bigint_bigint
    )
        SELECT
          res_numeric_numeric,
          pg_typeof(res_numeric_numeric)::varchar(30) AS type_numeric_numeric,
          res_numeric_real,
          pg_typeof(res_numeric_real)::varchar(30) AS type_numeric_real,
          res_numeric_float,
          pg_typeof(res_numeric_float)::varchar(30) AS type_numeric_float,
          res_numeric_int,
          pg_typeof(res_numeric_int)::varchar(30) AS type_numeric_int,
          res_numeric_smallint,
          pg_typeof(res_numeric_smallint)::varchar(30) AS type_numeric_smallint,
          res_numeric_bigint,
          pg_typeof(res_numeric_bigint)::varchar(30) AS type_numeric_bigint,
          res_real_numeric,
          pg_typeof(res_real_numeric)::varchar(30) AS type_real_numeric,
          res_real_real,
          pg_typeof(res_real_real)::varchar(30) AS type_real_real,
          res_real_float,
          pg_typeof(res_real_float)::varchar(30) AS type_real_float,
          res_real_int,
          pg_typeof(res_real_int)::varchar(30) AS type_real_int,
          res_real_smallint,
          pg_typeof(res_real_smallint)::varchar(30) AS type_real_smallint,
          res_real_bigint,
          pg_typeof(res_real_bigint)::varchar(30) AS type_real_bigint,
          res_float_numeric,
          pg_typeof(res_float_numeric)::varchar(30) AS type_float_numeric,
          res_float_real,
          pg_typeof(res_float_real)::varchar(30) AS type_float_real,
          res_float_float,
          pg_typeof(res_float_float)::varchar(30) AS type_float_float,
          res_float_int,
          pg_typeof(res_float_int)::varchar(30) AS type_float_int,
          res_float_smallint,
          pg_typeof(res_float_smallint)::varchar(30) AS type_float_smallint,
          res_float_bigint,
          pg_typeof(res_float_bigint)::varchar(30) AS type_float_bigint,
          res_int_numeric,
          pg_typeof(res_int_numeric)::varchar(30) AS type_int_numeric,
          res_int_real,
          pg_typeof(res_int_real)::varchar(30) AS type_int_real,
          res_int_float,
          pg_typeof(res_int_float)::varchar(30) AS type_int_float,
          res_int_int,
          pg_typeof(res_int_int)::varchar(30) AS type_int_int,
          res_int_smallint,
          pg_typeof(res_int_smallint)::varchar(30) AS type_int_smallint,
          res_int_bigint,
          pg_typeof(res_int_bigint)::varchar(30) AS type_int_bigint,
          res_smallint_numeric,
          pg_typeof(res_smallint_numeric)::varchar(30) AS type_smallint_numeric,
          res_smallint_real,
          pg_typeof(res_smallint_real)::varchar(30) AS type_smallint_real,
          res_smallint_float,
          pg_typeof(res_smallint_float)::varchar(30) AS type_smallint_float,
          res_smallint_int,
          pg_typeof(res_smallint_int)::varchar(30) AS type_smallint_int,
          res_smallint_smallint,
          pg_typeof(res_smallint_smallint)::varchar(30) AS type_smallint_smallint,
          res_smallint_bigint,
          pg_typeof(res_smallint_bigint)::varchar(30) AS type_smallint_bigint,
          res_bigint_numeric,
          pg_typeof(res_bigint_numeric)::varchar(30) AS type_bigint_numeric,
          res_bigint_real,
          pg_typeof(res_bigint_real)::varchar(30) AS type_bigint_real,
          res_bigint_float,
          pg_typeof(res_bigint_float)::varchar(30) AS type_bigint_float,
          res_bigint_int,
          pg_typeof(res_bigint_int)::varchar(30) AS type_bigint_int,
          res_bigint_smallint,
          pg_typeof(res_bigint_smallint)::varchar(30) AS type_bigint_smallint,
          res_bigint_bigint,
          pg_typeof(res_bigint_bigint)::varchar(30) AS type_bigint_bigint
        FROM R
    ;
END;
$$;

-- DROP PROCEDURE get_operators;
CREATE PROCEDURE get_operators (INOUT result refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN result FOR
    -- # 1
    SELECT  5 > all(array[2, 1, 4]) AS t1,
            10 > any(select id from rooms) as t2,
            10 > some(select id from rooms) as t2,
            2 IN (select id from rooms) as t3,
            - 5 - - - - 11 sum  -- no 'as', and unary minuses
    ;
END;
$$;

-- DROP PROCEDURE test_loops;
CREATE PROCEDURE test_loops ()  -- no parameters
LANGUAGE 'plpgsql'
AS $$
DECLARE
    VoidThings_2 varchar(20);
    fake int;
    t varchar(20);
    i int;
BEGIN
    INSERT INTO VoidThings (category, height, stub)
    VALUES ('empty', 2, default);

    INSERT INTO VoidThings
    VALUES ('exhaust', 7, default);

    INSERT INTO VoidThings (category, height)
    VALUES ('guest', 11), ('need', 10), ('empty', 21);

    INSERT INTO VoidThings (category, height)
    SELECT 'need', DATE_PART('month',dob)
    FROM ext.Persons
    WHERE not dob is null;

    VoidThings_2 := 'dreams';
    SELECT 'guest' AS cat, DATE_PART('month',dob) * 2 + 1 AS height
    INTO VoidThings_2       -- here: what var goes here?
    FROM ext.Persons
    WHERE not dob is null;

    DELETE FROM Own
    WHERE id_person = '4ef2fcf9-8f5b-41c3-8127-1a1c464bb10a';

    -- here: add 'delete using'
    -- use https://stackoverflow.com/questions/5170546/how-do-i-delete-a-fixed-number-of-rows-with-sorting-in-postgresql

    /*
    SELECT 'guest' AS cat, DATE_PART('month',dob) * 2 + 1 AS height
    INTO TEMP VoidThings_3
    FROM ext.Persons
    WHERE not dob is null;
    */

    UPDATE ext.Persons
    SET effect = effect + 1
    WHERE id = '731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA'
    ;

    FOR t IN SELECT * FROM json_array_elements_text('["barber", "plumber"]')
    LOOP
        FOR i IN 45..54 BY 3
        LOOP
            INSERT INTO VoidThings (category, height)
            VALUES (t, i);
        END LOOP;

        FOR i IN REVERSE 81..78
        LOOP
            INSERT INTO VoidThings (category, height)
            VALUES (t || '_rev', i);
        END LOOP;
    END LOOP;

    FOREACH i IN ARRAY array[21, 22, 23, 24]
    LOOP
        CASE WHEN i <= 23
                AND 'name' IN ('row', 'name', 'index')      -- test for 'in row'
            THEN
                INSERT INTO VoidThings (category, height)
                VALUES ('foreach-case', i);
            WHEN i BETWEEN 23 AND 24 THEN
                INSERT INTO VoidThings (category, height)
                VALUES ('foreach-case-bw', i);
        END CASE;

        IF i <= 23 THEN
            INSERT INTO VoidThings (category, height)
            VALUES ('foreach-if', i);
        ELSIF i = 24 THEN
            INSERT INTO VoidThings (category, height)
            VALUES ('foreach-elsif', i);
        ELSE
            INSERT INTO VoidThings (category, height)
            VALUES ('foreach-else', i);
        END IF;
    END LOOP;

    i := 1;
    WHILE i < 3
    LOOP
        i := i + 1;
        INSERT INTO VoidThings (category, height)
        VALUES ('trust', i * 11 + 6);
    END LOOP;
END;
$$;

-- DROP PROCEDURE get_returning;
CREATE PROCEDURE get_returning
(
    INOUT insert_result_1 refcursor,
    INOUT insert_result_2 refcursor,
    INOUT delete_result_1 refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    -- insert values
    OPEN insert_result_1 FOR
    INSERT INTO VoidThings (category, height)
    VALUES ('guest', 16), ('need', 22), ('empty', 36)
    RETURNING height AS notch, category
    ;

    -- insert select
    OPEN insert_result_2 FOR
    INSERT INTO VoidThings (category, height)
    SELECT 'need', DATE_PART('month',dob)
    FROM ext.Persons
    WHERE not dob is null
    RETURNING *
    ;

    OPEN delete_result_1 FOR
    DELETE FROM Own
    WHERE id_person = '4ef2fcf9-8f5b-41c3-8127-1a1c464bb10a'
    RETURNING *
    ;
END;
$$;

-- DROP PROCEDURE insert_conflict;
CREATE PROCEDURE insert_conflict ()
LANGUAGE 'plpgsql'
AS $$
BEGIN
    INSERT INTO Rooms
    VALUES (15, 'Yukon')
    ON CONFLICT DO NOTHING
    ;

    INSERT INTO ext.Persons (id, lastname, firstname, dob, tab_num)
    VALUES ('8261e6b17b5f07c3bf1925ee434ebcd9', 'Bodoia', 'Mario', '1978-01-09', 1091)
    ON CONFLICT (id) DO UPDATE
    SET effect = ext.Persons.effect + 1
    ;
END;
$$;

-- DROP PROCEDURE get_aggregates;
CREATE PROCEDURE get_aggregates
(
    coef real,
    INOUT result refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    -- insert values
    OPEN result FOR
    WITH C AS
    (
        SELECT  id_person AS id_agent,
                input,
                date::date,
                'use ''quotes''' "use ""quotes"" """""
        FROM
          ( VALUES
              ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 59, '2020-09-03'),
              ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 40, '2021-07-06'),
              ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 20, '2022-05-09'),
              ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 54, '2020-03-12'),
              ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 36, '2021-01-15')
          ) transactions (id_person, input, date)

        UNION ALL

        SELECT  *
        FROM
        ( VALUES
            ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 61, '2022-02-18'::date, 't1'),
            ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 28, '2020-04-21'::date, 't2')
        ) t2

        UNION

        SELECT '731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA', 32, '2021-10-24'::date, 't3'
    ), FILTERED AS
    (
        SELECT *
        FROM C
    )
    SELECT DISTINCT ON (ext.Persons.id)
            FILTERED.id_agent,
            ext.Persons.lastname,
            SUM(FILTERED.input * coef) AS input,
            COUNT(FILTERED.input),
            FIRST.date "first",
            FIRST."use ""quotes"" """"" use_quotes
    FROM ext.Persons
        LEFT JOIN FILTERED ON FILTERED.id_agent::uuid = ext.Persons.id
        LEFT JOIN FILTERED FIRST ON FIRST.id_agent::uuid = ext.Persons.id
    GROUP BY ext.Persons.id, "first", ext.Persons.lastname, FILTERED.id_agent, FIRST."use ""quotes"" """""
    ORDER BY ext.Persons.id
    ;
END;
$$;

-- DROP PROCEDURE test_from_select;
CREATE PROCEDURE test_from_select
(
    INOUT result refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN result FOR
    SELECT  C.lastname,
            C.room,
            CASE WHEN OWN.id_person NOTNULL THEN 'x' END own
    FROM Own
        RIGHT JOIN
        (
            SELECT  P.id id_person,
                    P.lastname,
                    R.id id_room,
                    R.name room
            FROM ext.Persons AS P
                CROSS JOIN Rooms R
        ) C ON C.id_person = Own.id_person
                    AND C.id_room = Own.id_room
    ;
END;
$$;

-- DROP PROCEDURE test_out;
CREATE PROCEDURE test_out
(
    INOUT p_int int,
    INOUT p_int_arr int[],
    INOUT p_bool bool,
    INOUT p_bool_arr bool[],
    INOUT p_date date,
    INOUT p_date_arr date[],
    INOUT p_instant timestamptz,
    INOUT p_instant_arr timestamptz[],
    INOUT p_datetime timestamp,
    INOUT p_datetime_arr timestamp[],
    INOUT p_varchar varchar(3),
    INOUT p_varchar_arr varchar(3)[],
    INOUT p_bytea bytea,
    INOUT p_status app_status,
    INOUT p_valid_statuses app_status[],
    INOUT result_1 refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    p_int := p_int + 1;
    p_int_arr := p_int_arr || array[-4, 61];
    p_bool := not p_bool;
    p_bool_arr := p_bool_arr || false;
    p_date := '2022-04-01'::date;
    p_date_arr := p_date_arr || array[p_date];

    OPEN result_1 FOR
    -- # 1
    SELECT 5 "in";

    p_instant := '2020-06-19'::timestamptz;
    p_instant_arr := p_instant_arr || array[p_instant, '2019-08-10 23:41'::timestamptz];
    p_datetime := '2020-06-19'::timestamp;
    p_datetime_arr := p_datetime_arr || array[p_datetime, '2019-08-10 23:41'::timestamp];
    p_varchar := 'TRY';
    p_varchar_arr := p_varchar_arr || array[null, p_varchar];       -- nulls are okay for string arrays
    p_bytea := p_bytea || '123'::bytea;
    p_status := 'hold';
    p_valid_statuses := p_valid_statuses || 'pending'::app_status;

    /*
    -- for INOUT values, nulls in arrays don't come through
    p_int_arr := p_int_arr || array[null, -4];
    p_bool_arr := p_bool_arr || array[null, false];
    p_date_arr := p_date_arr || array[null, p_date];
    p_timestamp_arr := p_timestamp_arr || array[null, p_timestamp];
    */
END;
$$;

-- DROP PROCEDURE get_composite;
CREATE PROCEDURE get_composite
(
    INOUT result refcursor
)
LANGUAGE 'plpgsql'
AS $$
DECLARE
    arrow Depts%rowtype;
BEGIN
    UPDATE financial_history
    SET diff.paid.id_currency = 3
    WHERE (diff).paid.id_currency = 2
    ;

    SELECT
        5 as id,
        11 as id_parent,
        'Hall (East)' as name
    INTO arrow;     -- here: use in test

    OPEN result FOR
    SELECT  id,
            diff as_block,
            (diff).date,
            (diff).paid,
            (diff).paid.amount,
            'hold'::app_status as last_status,
            null::app_status as aux_status
    FROM financial_history
    ORDER BY (diff).date DESC
    ;
END;
$$;

-- DROP PROCEDURE test_duplicate_open;
CREATE PROCEDURE test_duplicate_open
(
    i int,
    INOUT scalar refcursor,
    INOUT single refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    IF i > 10 THEN
        OPEN scalar FOR
        -- # 1
        SELECT 4 AS id;

        OPEN single FOR
        SELECT  4 AS id,
                'name' AS name;
    ELSE
        -- another branch to fulfill the same refcursors

        OPEN scalar FOR
        SELECT 5 AS id;

        OPEN single FOR
        -- # 1
        -- comment from second instance
        SELECT  5 AS id,
                'name' AS name;
    END IF;
END;
$$;
