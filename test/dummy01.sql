CREATE SCHEMA ext;
CREATE SCHEMA :owner;     -- for test, needs to match the username

CREATE TABLE ext.Persons
(
    id uuid,
    lastname varchar(50),
    firstname varchar(50),
    dob date,
    tab_num bigint,
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

INSERT INTO ext.Persons VALUES ('9CF9848C-E056-4E58-895F-B7C428B81FBA', 'Borduk', 'Petro', '1946-07-07', 1005);
INSERT INTO ext.Persons VALUES ('4EF2FCF9-8F5B-41C3-8127-1A1C464BB10A', 'Stasuk', 'Pablo', '1952-10-05', null);
INSERT INTO ext.Persons VALUES ('BBF7C3E7-408D-485A-B21C-78BA300B0EF1', 'Porosuk', 'Sergio', null, 1015);
INSERT INTO ext.Persons VALUES ('A581E1EB-24DF-4C31-A428-14857EC29E7D', 'Krasuk', 'Stan', '1984-06-09', 1034);
INSERT INTO ext.Persons VALUES ('731B7BD8-AEEA-4A67-80C7-3A9E666F1FDA', 'Pevshitz', null, '1988-12-05', 1089);

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
CREATE PROCEDURE Persons_GetAll (INOUT Users refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN Users FOR
    SELECT  ROW_NUMBER() OVER (ORDER BY id) AS num,
            *
    FROM Persons
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
        INOUT done char(5) = 'emp'
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
                sample + 45 AS "had it"
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
    SELECT  name::uuid
    FROM Rooms R
    ORDER BY R.order;
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
            array[null, null, true, false],
            array(with r as (select name from Depts) select distinct * from r) as names,
            '{5, 8, 2}'::int[] "order"
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
            R.id
    FROM ext.Persons AS P
        CROSS JOIN Rooms AS R
    WHERE P.lastname = 'Pevshitz'
        AND R.name = 'HR'
    RETURNING *
    ;
END;
$$;

-- DROP PROCEDURE get_literals;
CREATE PROCEDURE get_literals (INOUT result refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN result FOR
    SELECT  2 as int,
            1.5 as numeric,
            1.26e-3 as numeric_e_neg,
            1.26e+3 as numeric_e_pos,
            1.26e2 as numeric_e_def,
            .238::real as real,
            4.::float as float,
            .238::money as money,
            'n'::varchar(5) as varchar,
            false as bool
        ;
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
          pg_typeof(res_numeric_numeric) AS type_numeric_numeric,
          res_numeric_real,
          pg_typeof(res_numeric_real) AS type_numeric_real,
          res_numeric_float,
          pg_typeof(res_numeric_float) AS type_numeric_float,
          res_numeric_int,
          pg_typeof(res_numeric_int) AS type_numeric_int,
          res_numeric_smallint,
          pg_typeof(res_numeric_smallint) AS type_numeric_smallint,
          res_numeric_bigint,
          pg_typeof(res_numeric_bigint) AS type_numeric_bigint,
          res_real_numeric,
          pg_typeof(res_real_numeric) AS type_real_numeric,
          res_real_real,
          pg_typeof(res_real_real) AS type_real_real,
          res_real_float,
          pg_typeof(res_real_float) AS type_real_float,
          res_real_int,
          pg_typeof(res_real_int) AS type_real_int,
          res_real_smallint,
          pg_typeof(res_real_smallint) AS type_real_smallint,
          res_real_bigint,
          pg_typeof(res_real_bigint) AS type_real_bigint,
          res_float_numeric,
          pg_typeof(res_float_numeric) AS type_float_numeric,
          res_float_real,
          pg_typeof(res_float_real) AS type_float_real,
          res_float_float,
          pg_typeof(res_float_float) AS type_float_float,
          res_float_int,
          pg_typeof(res_float_int) AS type_float_int,
          res_float_smallint,
          pg_typeof(res_float_smallint) AS type_float_smallint,
          res_float_bigint,
          pg_typeof(res_float_bigint) AS type_float_bigint,
          res_int_numeric,
          pg_typeof(res_int_numeric) AS type_int_numeric,
          res_int_real,
          pg_typeof(res_int_real) AS type_int_real,
          res_int_float,
          pg_typeof(res_int_float) AS type_int_float,
          res_int_int,
          pg_typeof(res_int_int) AS type_int_int,
          res_int_smallint,
          pg_typeof(res_int_smallint) AS type_int_smallint,
          res_int_bigint,
          pg_typeof(res_int_bigint) AS type_int_bigint,
          res_smallint_numeric,
          pg_typeof(res_smallint_numeric) AS type_smallint_numeric,
          res_smallint_real,
          pg_typeof(res_smallint_real) AS type_smallint_real,
          res_smallint_float,
          pg_typeof(res_smallint_float) AS type_smallint_float,
          res_smallint_int,
          pg_typeof(res_smallint_int) AS type_smallint_int,
          res_smallint_smallint,
          pg_typeof(res_smallint_smallint) AS type_smallint_smallint,
          res_smallint_bigint,
          pg_typeof(res_smallint_bigint) AS type_smallint_bigint,
          res_bigint_numeric,
          pg_typeof(res_bigint_numeric) AS type_bigint_numeric,
          res_bigint_real,
          pg_typeof(res_bigint_real) AS type_bigint_real,
          res_bigint_float,
          pg_typeof(res_bigint_float) AS type_bigint_float,
          res_bigint_int,
          pg_typeof(res_bigint_int) AS type_bigint_int,
          res_bigint_smallint,
          pg_typeof(res_bigint_smallint) AS type_bigint_smallint,
          res_bigint_bigint,
          pg_typeof(res_bigint_bigint) AS type_bigint_bigint
        FROM R
    ;
END;
$$;
