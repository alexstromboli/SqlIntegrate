\c postgres;
DROP DATABASE dummy01;
CREATE DATABASE dummy01;
\c dummy01;

CREATE SCHEMA ext;
CREATE SCHEMA alexey;     -- for test, needs to match the username

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

CREATE FUNCTION alexey.Sum (a money, b money)
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
CREATE PROCEDURE get_array (INOUT names refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN names FOR
    SELECT  name,
            array[null, null, true, false],
            array(with r as (select name from Depts) select distinct * from r) as names
    FROM Rooms;
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
