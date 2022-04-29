-- DROP PROCEDURE get_db_qualified;
CREATE PROCEDURE get_db_qualified (INOUT own refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN own FOR
    SELECT  SCHEMA.Own.id_person,
            DBNAME.SCHEMA.Own.id_room,
            DBNAME.SCHEMA.Rooms.*
    FROM DBNAME.SCHEMA.Own
        LEFT JOIN DBNAME.SCHEMA.Rooms ON DBNAME.SCHEMA.Rooms.id = SCHEMA.Own.id_room
    ;
END;
$$;
