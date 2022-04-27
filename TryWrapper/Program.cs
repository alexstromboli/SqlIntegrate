using System;
using System.Data;
using Npgsql;
using NodaTime;
using Npgsql.NodaTime;
using NpgsqlTypes;

namespace TryWrapper
{
	class Program
	{
		static void Main (string[] args)
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection (args[0]))
			{
				Conn.Open ();
				Conn.TypeMapper.UseNodaTime ();
				var DbProc = new Generated.DbProc (Conn, "alexey", "ext");

				/*
-- DROP PROCEDURE test;
CREATE PROCEDURE test (INOUT p int, INOUT res01 refcursor)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    p := p + 17;

    OPEN res01 FOR
    SELECT 41 as id, false as is_built;
END;
$$;
				 */

				/*
				using (var tran = Conn.BeginTransaction ())
				using (var cmd = Conn.CreateCommand ())
				{
					cmd.CommandText = "call alexey.test(@t_p, @res01);";
					cmd.Parameters.AddWithValue ("@t_p", 11).Direction = ParameterDirection.InputOutput;
					cmd.Parameters.Add (new NpgsqlParameter ("@res01", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "res01"});

					cmd.ExecuteNonQuery ();
					long res_p = (int) cmd.Parameters["@t_p"].Value;

					using (var rescmd = Conn.CreateCommand ())
					{
						rescmd.CommandText = "FETCH ALL IN \"res01\"";

						using (var rdr = rescmd.ExecuteReader ())
						{
							if (rdr.Read ())
							{
								int? id = rdr["id"] as int?;
								bool? is_built = rdr["is_built"] as bool?;
							}
						}
					}

					tran.Commit ();
				}
				*/

				//
				int? t_int = 10;
				int?[] t_int_arr = { 9, 7, 2 };
				bool? t_bool = true;
				bool?[] t_bool_arr = { false, true };
				LocalDate? t_date = new LocalDate (1991, 08, 19);
				LocalDate?[] t_date_arr = { new LocalDate (1991, 08, 19), new LocalDate (1991, 12, 08) };
				Instant? t_instant = Instant.FromDateTimeUtc (new DateTime (1993, 06, 08, 12, 42, 0, DateTimeKind.Utc));
				Instant?[] t_instant_arr = { t_instant };
				string t_string = "town";
				string[] t_string_arr = { "town", "fly" };
				byte[] t_bytea = { 4, 8, 1 };

				var Result = DbProc.alexey.test_out (ref t_int, ref t_int_arr, ref t_bool, ref t_bool_arr,
					ref t_date, ref t_date_arr, ref t_instant, ref t_instant_arr, ref t_string, ref t_string_arr,
					ref t_bytea);
			}
		}
	}
}
