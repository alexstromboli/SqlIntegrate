using System;
using System.Data;

using Npgsql;
using NpgsqlTypes;

/*
CREATE TYPE monetary AS
(
    amount numeric,
    id_currency int
);

CREATE TYPE payment AS
(
    paid monetary,
    date date
);

-- DROP PROCEDURE get_monetary;
CREATE PROCEDURE get_monetary
(
    INOUT result refcursor
)
LANGUAGE 'plpgsql'
AS $$
BEGIN
    OPEN result FOR
    SELECT  ((10.5, 2), '2020-06-01')::payment AS m
    ;
END;
$$;
 */

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

	class Dept
	{
		[PgName ("id")]
		public int Id;

		[PgName ("id_parent")]
		public int? ParentId;

		[PgName ("name")]
		public string Name;
	}

	partial class Program
	{
		static void TestComposite (NpgsqlConnection conn)
		{
			conn.TypeMapper.MapComposite<Monetary> ("monetary");
			conn.TypeMapper.MapComposite<Payment> ("payment");
			//conn.TypeMapper.MapComposite<Dept> ("depts");
			//conn.TypeMapper. <Dept> ("depts");

			using (var tran = conn.BeginTransaction ())
			using (var cmd = conn.CreateCommand ())
			{
				cmd.CommandText = "call get_composite (@result)";
				cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor)
					{ Direction = ParameterDirection.InputOutput, Value = "result" });

				cmd.ExecuteNonQuery ();

				using (var rescmd = conn.CreateCommand ())
				{
					rescmd.CommandText = "FETCH ALL IN \"result\"";
					//rescmd.UnknownResultTypeList = new[] { false, true };

					using (var rdr = rescmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							//var m = rdr["m"];
							//var arrow = rdr["arrow"];
							object[] vs = new object[6];
							var arrow = rdr.GetValues (vs);
						}
					}
				}
			}
		}
	}
}
