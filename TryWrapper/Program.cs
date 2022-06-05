using System;
using System.Data;
using Npgsql;
using NodaTime;
using Npgsql.NodaTime;
using NpgsqlTypes;

namespace TryWrapper
{
	public enum EStatus
	{
		r_pending = 1,
		r_active,
		r_hold
	}

	class Program
	{
		static void Main (string[] args)
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection (args[0]))
			{
				Conn.Open ();
				Conn.TypeMapper.UseNodaTime ();
				//Conn.TypeMapper.MapEnum<EStatus> ("alexey.app_status");
				//Conn.TypeMapper.MapComposite<Generated.alexey.monetary> ("alexey.monetary");
				//Conn.TypeMapper.MapComposite<Generated.alexey.payment> ("alexey.payment");
				//Conn.TypeMapper.MapComposite<Generated.alexey.indirectly_used_type> ("alexey.indirectly_used_type");
				var DbProc = new Generated.DbProc (Conn//, "alexey", "ext", "no_proc"
					);

				//
				var r01 = DbProc.alexey.get_composite ();

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
				//EStatus? t_status = EStatus.r_active;

				var Result = DbProc.alexey.test_out (ref t_int, ref t_int_arr, ref t_bool, ref t_bool_arr,
					ref t_date, ref t_date_arr,
					ref t_instant, ref t_instant_arr,
					ref t_datetime, ref t_datetime_arr,
					ref t_string, ref t_string_arr,
					ref t_bytea, ref t_status, ref t_valid_statuses);

				//
				var test_from_select = DbProc.alexey.test_from_select ();
				var get_aggregates = DbProc.alexey.get_aggregates (1.7f);
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
