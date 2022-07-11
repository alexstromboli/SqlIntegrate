using System;
using System.Linq;

using Npgsql;
using NodaTime;
using NpgsqlTypes;

namespace TryWrapper
{
	public class Town
	{
		public string city;
		[PgName("province")]
		public string region;

		public override string ToString ()
		{
			return $"{city ?? "???"}, {region ?? "???"}";
		}
	}

	public class Payer
	{
		public string PostalCode;
		public string Last4;
	}

	class Program
	{
		static void Main (string[] args)
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection (args[0]))
			{
				Conn.Open ();
				Conn.TypeMapper.UseNodaTime ();

				Func<byte[], byte[]> XorCryptor = buf => buf.Select (b => (byte)(b ^ 0x53)).ToArray ();
				var DbProc = new Generated.DbProc (Conn, XorCryptor, XorCryptor);

				// jsonb
				var t_json = DbProc.alexey.test_json ("{\"t\": 9}", "{\"t\": 20}");

				// encryption
				Payer Customer = new Payer { Last4 = "1502", PostalCode = "X3W" };
				DbProc.alexey.test_write_encrypted (new byte[] { 8, 9, 1, 3 }, Customer);
				var rc01 = DbProc.alexey.test_read_encrypted ();

				// exceptions
				try
				{
					DbProc.alexey.test_exception ("Check exception", 42);
				}
				catch (PostgresException e)
				{
					var M = e.Message;
				}

				//
				var r01 = DbProc.alexey.get_composite (new Town { city = "Ottawa", region = "Ontario" });

				//
				int? t_int = 10;
				int[] t_int_arr = { 9, 7, 2 };
				bool? t_bool = true;
				bool[] t_bool_arr = { false, true };
				LocalDate? t_date = new LocalDate (1991, 08, 19);
				LocalDate[] t_date_arr = { new LocalDate (1991, 08, 19), new LocalDate (1991, 12, 08) };
				Instant? t_instant = Instant.FromDateTimeUtc (new DateTime (1993, 06, 08, 12, 42, 0, DateTimeKind.Utc));
				Instant[] t_instant_arr = { t_instant.Value };
				LocalDateTime? t_datetime = LocalDateTime.FromDateTime (new DateTime (1993, 06, 08, 12, 42, 0));
				LocalDateTime[] t_datetime_arr = { t_datetime.Value };
				string t_string = "town";
				string[] t_string_arr = { "town", "fly" };
				byte[] t_bytea = { 4, 8, 1 };
				string t_status = Generated.alexey.app_status.active;
				string[] t_valid_statuses = new[] { Generated.alexey.app_status.active };
				Town t_town = new Town { city = "Oakville", region = "Ontario" };
				Payer t_payer = new Payer { Last4 = "4578", PostalCode = "10001" };

				var Result = DbProc.alexey.test_out (ref t_int, ref t_int_arr, ref t_bool, ref t_bool_arr,
					ref t_date, ref t_date_arr,
					ref t_instant, ref t_instant_arr,
					ref t_datetime, ref t_datetime_arr,
					ref t_string, ref t_string_arr,
					ref t_bytea, ref t_status, ref t_valid_statuses,
					ref t_town, ref t_payer);

				//
				var test_from_select = DbProc.alexey.test_from_select ();
				var get_aggregates = DbProc.alexey.get_aggregatesAsync (1.7f).Result;
				var rsons_getall = DbProc.alexey.persons_getall ();
				DbProc.ext.calc ();
				var get_single_row = DbProc.alexey.get_single_row ();
				var get_scalar = DbProc.alexey.get_scalar ();
				var get_user_and_details = DbProc.alexey.get_user_and_details ();
				var get_array = DbProc.alexey.get_array ();
				var get_join_single = DbProc.alexey.get_join_single ();
				var get_inserted = DbProc.alexey.get_inserted ();
				var get_value_types = DbProc.alexey.get_value_types ();
				var get_numeric_types_math = DbProc.alexey.get_numeric_types_math ();
				var get_operators = DbProc.alexey.get_operators ();
				var get_returning = DbProc.alexey.get_returning ();
				var get_db_qualified = DbProc.alexey.get_db_qualified ();
			}
		}
	}
}
