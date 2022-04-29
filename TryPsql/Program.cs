using System;
using System.Data;

using Npgsql;
using NpgsqlTypes;

/*
https://stackoverflow.com/questions/49547761/create-procedure-to-execute-query-in-postgresql/56147768#56147768
*/

namespace TryPsql
{
	partial class Program
	{
		static void Main (string[] args)
		{
			using (var conn = new NpgsqlConnection ("host=/var/run/postgresql;database=dummy01;Integrated Security=true"))
			{
				conn.Open ();

				//
				TestComposite (conn);

				//
				using (var cmd = conn.CreateCommand ())
				{
					cmd.CommandText = "SELECT *, now()::time as span FROM ext.Persons";

					using (var rdr = cmd.ExecuteReader ())
					{
						while (rdr.Read ())
						{
							Guid? id = rdr["id"] as Guid?;
							string lastname = rdr["lastname"] as string;
							string firstname = rdr["firstname"] as string;
							DateTime? dob = rdr["dob"] as DateTime?;
							long? tab_num = rdr["tab_num"] as long?;
							object span = rdr["span"];
						}
					}
				}

				using (var tran = conn.BeginTransaction ())
				using (var cmd = conn.CreateCommand ())
				{
					//cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "call public.RoomsForPerson(@id, @res01, @res02, @name/*, @came, @done*/)";
					cmd.Parameters.AddWithValue ("@id", new Guid ("9CF9848C-E056-4E58-895F-B7C428B81FBA"));
					cmd.Parameters.Add (new NpgsqlParameter ("@res01", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "output01"});
					cmd.Parameters.Add (new NpgsqlParameter ("@res02", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "output02"});
					cmd.Parameters.AddWithValue ("@name", NpgsqlDbType.Varchar, "given");
					cmd.Parameters.Add (new NpgsqlParameter ("@came", NpgsqlDbType.Bigint) { Direction = ParameterDirection.InputOutput, Value = 8814});
					cmd.Parameters.Add (new NpgsqlParameter ("@done", NpgsqlDbType.Varchar) { Direction = ParameterDirection.InputOutput, Value = "tried"});

					cmd.ExecuteNonQuery ();
					string done = cmd.Parameters["@done"].Value as string;
					long came = (long) cmd.Parameters["@came"].Value;

					using (var rescmd = conn.CreateCommand ())
					{
						rescmd.CommandText = "FETCH ALL IN \"output01\"";

						using (var rdr = rescmd.ExecuteReader ())
						{
							while (rdr.Read ())
							{
								Guid? id = rdr["id"] as Guid?;
								string lastname = rdr["lastname"] as string;
								string firstname = rdr["firstname"] as string;
								DateTime? dob = rdr["dob"] as DateTime?;
								long? tab_num = rdr["tab_num"] as long?;
							}
						}
					}

					using (var rescmd = conn.CreateCommand ())
					{
						rescmd.CommandText = "FETCH ALL IN \"output02\"";

						using (var rdr = rescmd.ExecuteReader ())
						{
							while (rdr.Read ())
							{
								int? id = rdr["id"] as int?;
								string name = rdr["name"] as string;
								int[] ord = rdr["ord"] as int[];
							}
						}
					}

					tran.Commit ();
				}
			}
		}
	}
}
