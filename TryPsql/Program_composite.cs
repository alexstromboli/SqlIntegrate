using System;
using System.Data;

using Npgsql;
using NpgsqlTypes;

namespace TryPsql
{
	class Monetary
	{
		[PgName ("amount")]
		public decimal Amount;

		[PgName ("id_currency")]
		public decimal CurrencyId;
	}

	class Payment
	{
		[PgName ("paid")]
		public Monetary Paid;

		[PgName ("date")]
		public DateTime Date;
	}

	partial class Program
	{
		static void TestComposite (NpgsqlConnection conn)
		{
			conn.TypeMapper.MapComposite<Monetary> ("monetary");
			conn.TypeMapper.MapComposite<Payment> ("payment");

			using (var tran = conn.BeginTransaction ())
			using (var cmd = conn.CreateCommand ())
			{
				cmd.CommandText = "call get_monetary (@result)";
				cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor)
					{ Direction = ParameterDirection.InputOutput, Value = "result" });

				cmd.ExecuteNonQuery ();

				using (var rescmd = conn.CreateCommand ())
				{
					rescmd.CommandText = "FETCH ALL IN \"result\"";

					using (var rdr = rescmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							var m = rdr["m"];
						}
					}
				}
			}
		}
	}
}
