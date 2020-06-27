\c postgres;
DROP DATABASE dummy01;
CREATE DATABASE dummy01;
\c dummy01;

CREATE SCHEMA ext;
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
    id int,
    name varchar(30)
);

CREATE TABLE Own
(
    id_person uuid,
    id_room int
);

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
    
-- DROP PROCEDURE ext.Calc;
CREATE PROCEDURE ext.Calc ()
LANGUAGE plpgsql
AS $$
    DECLARE
        dt date;
        over int := 9;
    BEGIN
        dt := now();
        --SELECT 6 as done;
        --SELECT 't' as over;
    END;
$$;
    
-- DROP PROCEDURE RoomsForPerson;
CREATE PROCEDURE RoomsForPerson (id_person uuid,
        INOUT res01 refcursor,
        INOUT res02 refcursor,
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
                tab_num
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
    END;
$$;
