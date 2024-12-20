﻿/*
   * This file has been generated automatically.
   * Do not edit, or you will lose your changes after the next run.
   */

using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Npgsql.NameTranslation;
using NodaTime;
using System.Text;
using Newtonsoft.Json;

namespace Generated
{
	public class DbProc
	{
		public NpgsqlConnection Conn;
		public Func<byte[], byte[]> Encryptor;
		public Func<byte[], byte[]> Decryptor;

		public async ValueTask<NpgsqlTransaction> BeginTransactionOptionalAsync ()
		{
			try
			{
				return await Conn.BeginTransactionAsync ();
			}
			catch (InvalidOperationException)
			{
				return await ValueTask.FromResult<NpgsqlTransaction> (null);
			}
		}

		protected alexey m_alexey = null;
		public alexey alexey
		{
			get
			{
				if (m_alexey == null)
				{
					m_alexey = new alexey (this);
				}
				return m_alexey;
			}
		}

		protected ext m_ext = null;
		public ext ext
		{
			get
			{
				if (m_ext == null)
				{
					m_ext = new ext (this);
				}
				return m_ext;
			}
		}

		protected no_proc m_no_proc = null;
		public no_proc no_proc
		{
			get
			{
				if (m_no_proc == null)
				{
					m_no_proc = new no_proc (this);
				}
				return m_no_proc;
			}
		}

		public DbProc (NpgsqlConnection Conn, Func<byte[], byte[]> Encryptor, Func<byte[], byte[]> Decryptor)
		{
			this.Conn = Conn;
			this.Encryptor = Encryptor;
			this.Decryptor = Decryptor;
			UseCustomMapping (this.Conn);
		}

		public static void UseCustomMapping (NpgsqlConnection Conn)
		{
			if (Conn.State == ConnectionState.Closed || Conn.State == ConnectionState.Broken || Conn.State == ConnectionState.Connecting)
			{
				return;
			}

			Conn.TypeMapper.MapComposite<TryWrapper.Town> ("alexey.city_locale");
			Conn.TypeMapper.MapEnum<alexey.indirectly_used_enum> ("alexey.indirectly_used_enum", new NpgsqlNullNameTranslator ());
			Conn.TypeMapper.MapComposite<alexey.indirectly_used_type> ("alexey.indirectly_used_type");
			Conn.TypeMapper.MapEnum<TryWrapper.CardType> ("alexey.mapped", new NpgsqlNullNameTranslator ());
			Conn.TypeMapper.MapComposite<alexey.monetary> ("alexey.monetary");
			Conn.TypeMapper.MapComposite<alexey.payment> ("alexey.payment");
		}

		public T ReadEncrypted<T> (object Input)
		{
			if (Input == null || Input == DBNull.Value)
			{
				return default (T);
			}

			return JsonConvert.DeserializeObject<T> (Encoding.UTF8.GetString (Decryptor ((byte[])Input)));
		}

		public byte[] WriteEncrypted<T> (T Input)
		{
			if (Input == null)
			{
				return null;
			}

			return Encryptor (Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (Input)));
		}
	}

	public class alexey
	{
		public static class app_status
		{
			public const string pending = "pending";
			public const string active = "active";
			public const string hold = "hold";
			public const string half_reviewed = "half-reviewed";
			public const string _13_digits = "13 digits";
		}

		public enum indirectly_used_enum
		{
			first,
			second,
			put_out,
			_2_digit
		}

		public class indirectly_used_type
		{
			public string sign;
			public bool? is_on;
			public alexey.indirectly_used_enum order;
		}

		public class monetary
		{
			public decimal? amount;
			public int? id_currency;
		}

		public class payment
		{
			public alexey.monetary paid;
			public LocalDate? date;
			public alexey.indirectly_used_type[] indi;
		}

		public DbProc DbProc;
		public NpgsqlConnection Conn => DbProc.Conn;

		public alexey (DbProc DbProc)
		{
			this.DbProc = DbProc;
		}

		#region get_aggregates
		public class get_aggregates_Result_result
		{
			public string id_agent;
			public string lastname;
			public double? input;
			public long? count;
			public LocalDate? first;
			public string use_quotes;
		}

		public class get_aggregates_Result_ag_sum
		{
			public long? sum_int;
			public long? sum_small;
			public decimal? sum_big;
			public decimal? sum_numeric;
			public decimal? sum_money;
			public float? sum_real;
		}

		public class get_aggregates_Result_ag_avg
		{
			public decimal? avg_int;
			public decimal? avg_small;
			public decimal? avg_big;
			public decimal? avg_numeric;
			public float? avg_real;
		}

		public class get_aggregates_Result
		{
			public List<get_aggregates_Result_result> result;
			public List<get_aggregates_Result_ag_sum> ag_sum;
			public List<get_aggregates_Result_ag_avg> ag_avg;
		}

		public get_aggregates_Result get_aggregates (float? coef)
		{
			return get_aggregatesAsync (coef).Result;
		}

		public async Task<get_aggregates_Result> get_aggregatesAsync (float? coef)
		{
			get_aggregates_Result Result = new get_aggregates_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_aggregates\" (@coef, @result, @ag_sum, @ag_avg);";
					Cmd.Parameters.AddWithValue ("@coef", (object)coef ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@ag_sum", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "ag_sum" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@ag_avg", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "ag_avg" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						List<get_aggregates_Result_result> Set = new List<get_aggregates_Result_result> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_aggregates_Result_result
								{
									id_agent = Rdr["id_agent"] as string,
									lastname = Rdr["lastname"] as string,
									input = Rdr["input"] as double?,
									count = Rdr["count"] as long?,
									first = Rdr["first"] as LocalDate?,
									use_quotes = Rdr["use_quotes"] as string
								});
							}
						}

						Result.result = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"ag_sum\";";
						List<get_aggregates_Result_ag_sum> Set = new List<get_aggregates_Result_ag_sum> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_aggregates_Result_ag_sum
								{
									sum_int = Rdr["sum_int"] as long?,
									sum_small = Rdr["sum_small"] as long?,
									sum_big = Rdr["sum_big"] as decimal?,
									sum_numeric = Rdr["sum_numeric"] as decimal?,
									sum_money = Rdr["sum_money"] as decimal?,
									sum_real = Rdr["sum_real"] as float?
								});
							}
						}

						Result.ag_sum = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"ag_avg\";";
						List<get_aggregates_Result_ag_avg> Set = new List<get_aggregates_Result_ag_avg> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_aggregates_Result_ag_avg
								{
									avg_int = Rdr["avg_int"] as decimal?,
									avg_small = Rdr["avg_small"] as decimal?,
									avg_big = Rdr["avg_big"] as decimal?,
									avg_numeric = Rdr["avg_numeric"] as decimal?,
									avg_real = Rdr["avg_real"] as float?
								});
							}
						}

						Result.ag_avg = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"ag_sum\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"ag_avg\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_array
		public class get_array_Result_names
		{
			public int[] extents;
			public bool[] array;
			public string[] names;
			public int[] order;
			public string[] enum_range;
			public int[] array_plus_item;
			public int[] array_plus_array;
			public int[] item_plus_array;
		}

		public class get_array_Result_by_person
		{
			public Guid? id_person;
			public int[] array_agg;
			public int[] w1;
			public int[] w2;
		}

		public class get_array_Result_unnest
		{
			public int? unnest;
			public int? e;
			public string w;
			public string qw;
		}

		public class get_array_Result_second
		{
			public int? generate_series;
			public int? w;
		}

		public class get_array_Result
		{
			public List<get_array_Result_names> names;
			public List<get_array_Result_by_person> by_person;
			public List<get_array_Result_unnest> unnest;
			public List<LocalDateTime?> generate_series;
			public List<get_array_Result_second> second;
		}

		public get_array_Result get_array ()
		{
			return get_arrayAsync ().Result;
		}

		public async Task<get_array_Result> get_arrayAsync ()
		{
			get_array_Result Result = new get_array_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_array\" (@names, @by_person, @unnest, @generate_series, @second);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@names", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "names" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@by_person", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "by_person" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@unnest", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "unnest" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@generate_series", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "generate_series" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@second", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "second" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"names\";";
						List<get_array_Result_names> Set = new List<get_array_Result_names> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_array_Result_names
								{
									extents = Rdr["extents"] as int[],
									array = Rdr["array"] as bool[],
									names = Rdr["names"] as string[],
									order = Rdr["order"] as int[],
									enum_range = Rdr["enum_range"] as string[],
									array_plus_item = Rdr["array_plus_item"] as int[],
									array_plus_array = Rdr["array_plus_array"] as int[],
									item_plus_array = Rdr["item_plus_array"] as int[]
								});
							}
						}

						Result.names = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"by_person\";";
						List<get_array_Result_by_person> Set = new List<get_array_Result_by_person> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_array_Result_by_person
								{
									id_person = Rdr["id_person"] as Guid?,
									array_agg = Rdr["array_agg"] as int[],
									w1 = Rdr["w1"] as int[],
									w2 = Rdr["w2"] as int[]
								});
							}
						}

						Result.by_person = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"unnest\";";
						List<get_array_Result_unnest> Set = new List<get_array_Result_unnest> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_array_Result_unnest
								{
									unnest = Rdr["unnest"] as int?,
									e = Rdr["e"] as int?,
									w = Rdr["w"] as string,
									qw = Rdr["qw"] as string
								});
							}
						}

						Result.unnest = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"generate_series\";";
						List<LocalDateTime?> Set = new List<LocalDateTime?> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (Rdr["generate_series"] as LocalDateTime?);
							}
						}

						Result.generate_series = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"second\";";
						List<get_array_Result_second> Set = new List<get_array_Result_second> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_array_Result_second
								{
									generate_series = Rdr["generate_series"] as int?,
									w = Rdr["w"] as int?
								});
							}
						}

						Result.second = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"names\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"by_person\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"unnest\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"generate_series\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"second\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_composite
		public class get_composite_Result_result
		{
			public int? id;
			public alexey.payment as_block;
			public LocalDate? date;
			public alexey.monetary paid;
			public decimal? amount;
			public decimal? double_amount;
			public string last_status;
			public string aux_status;
			public TryWrapper.Town town;
			public TryWrapper.Town[] locations;
		}

		public class get_composite_Result
		{
			public List<get_composite_Result_result> result;
			public TryWrapper.Town matched;
		}

		public get_composite_Result get_composite (TryWrapper.Town destination)
		{
			return get_compositeAsync (destination).Result;
		}

		public async Task<get_composite_Result> get_compositeAsync (TryWrapper.Town destination)
		{
			get_composite_Result Result = new get_composite_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_composite\" (@destination, @result, @matched);";
					Cmd.Parameters.AddWithValue ("@destination", (object)destination ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@matched", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "matched" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						List<get_composite_Result_result> Set = new List<get_composite_Result_result> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_composite_Result_result
								{
									id = Rdr["id"] as int?,
									as_block = Rdr["as_block"] as alexey.payment,
									date = Rdr["date"] as LocalDate?,
									paid = Rdr["paid"] as alexey.monetary /* financial */,
									amount = Rdr["amount"] as decimal?,
									double_amount = Rdr["double_amount"] as decimal?,
									last_status = Rdr["last_status"] as string,
									aux_status = Rdr["aux_status"] as string,
									town = Rdr["town"] as TryWrapper.Town,
									locations = Rdr["locations"] as TryWrapper.Town[]
								});
							}
						}

						Result.result = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"matched\";";
						TryWrapper.Town Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["town"] as TryWrapper.Town;
							}
						}

						Result.matched = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"matched\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_db_qualified
		public class get_db_qualified_Result_own
		{
			public Guid? id_person;
			public int? id_room;
			public int? id;
			public string name;
			public int[] extents;
		}

		public List<get_db_qualified_Result_own> get_db_qualified ()
		{
			return get_db_qualifiedAsync ().Result;
		}

		public async Task<List<get_db_qualified_Result_own>> get_db_qualifiedAsync ()
		{
			List<get_db_qualified_Result_own> Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_db_qualified\" (@own);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@own", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "own" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"own\";";
						List<get_db_qualified_Result_own> Set = new List<get_db_qualified_Result_own> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_db_qualified_Result_own
								{
									id_person = Rdr["id_person"] as Guid?,
									id_room = Rdr["id_room"] as int?,
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string,
									extents = Rdr["extents"] as int[]
								});
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"own\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_inserted
		public class get_inserted_Result_inserted
		{
			public Guid? id_person;
			public int? id_room;
		}

		public List<get_inserted_Result_inserted> get_inserted ()
		{
			return get_insertedAsync ().Result;
		}

		public async Task<List<get_inserted_Result_inserted>> get_insertedAsync ()
		{
			List<get_inserted_Result_inserted> Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_inserted\" (@inserted);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@inserted", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "inserted" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"inserted\";";
						List<get_inserted_Result_inserted> Set = new List<get_inserted_Result_inserted> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_inserted_Result_inserted
								{
									id_person = Rdr["id_person"] as Guid?,
									id_room = Rdr["id_room"] as int?
								});
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"inserted\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_join_single
		public class get_join_single_Result_joined
		{
			public string title;
			public int? document_id;
			public LocalDate? date;
		}

		public List<get_join_single_Result_joined> get_join_single ()
		{
			return get_join_singleAsync ().Result;
		}

		public async Task<List<get_join_single_Result_joined>> get_join_singleAsync ()
		{
			List<get_join_single_Result_joined> Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_join_single\" (@joined);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@joined", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "joined" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"joined\";";
						List<get_join_single_Result_joined> Set = new List<get_join_single_Result_joined> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_join_single_Result_joined
								{
									title = Rdr["title"] as string,
									document_id = Rdr["document_id"] as int?,
									date = Rdr["date"] as LocalDate?
								});
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"joined\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_numeric_types_math
		public class get_numeric_types_math_Result_result
		{
			public decimal? res_numeric_numeric;
			public string type_numeric_numeric;
			public double? res_numeric_real;
			public string type_numeric_real;
			public double? res_numeric_float;
			public string type_numeric_float;
			public decimal? res_numeric_int;
			public string type_numeric_int;
			public decimal? res_numeric_smallint;
			public string type_numeric_smallint;
			public decimal? res_numeric_bigint;
			public string type_numeric_bigint;
			public double? res_real_numeric;
			public string type_real_numeric;
			public float? res_real_real;
			public string type_real_real;
			public double? res_real_float;
			public string type_real_float;
			public double? res_real_int;
			public string type_real_int;
			public double? res_real_smallint;
			public string type_real_smallint;
			public double? res_real_bigint;
			public string type_real_bigint;
			public double? res_float_numeric;
			public string type_float_numeric;
			public double? res_float_real;
			public string type_float_real;
			public double? res_float_float;
			public string type_float_float;
			public double? res_float_int;
			public string type_float_int;
			public double? res_float_smallint;
			public string type_float_smallint;
			public double? res_float_bigint;
			public string type_float_bigint;
			public decimal? res_int_numeric;
			public string type_int_numeric;
			public double? res_int_real;
			public string type_int_real;
			public double? res_int_float;
			public string type_int_float;
			public int? res_int_int;
			public string type_int_int;
			public int? res_int_smallint;
			public string type_int_smallint;
			public long? res_int_bigint;
			public string type_int_bigint;
			public decimal? res_smallint_numeric;
			public string type_smallint_numeric;
			public double? res_smallint_real;
			public string type_smallint_real;
			public double? res_smallint_float;
			public string type_smallint_float;
			public int? res_smallint_int;
			public string type_smallint_int;
			public short? res_smallint_smallint;
			public string type_smallint_smallint;
			public long? res_smallint_bigint;
			public string type_smallint_bigint;
			public decimal? res_bigint_numeric;
			public string type_bigint_numeric;
			public double? res_bigint_real;
			public string type_bigint_real;
			public double? res_bigint_float;
			public string type_bigint_float;
			public long? res_bigint_int;
			public string type_bigint_int;
			public long? res_bigint_smallint;
			public string type_bigint_smallint;
			public long? res_bigint_bigint;
			public string type_bigint_bigint;
		}

		public get_numeric_types_math_Result_result get_numeric_types_math ()
		{
			return get_numeric_types_mathAsync ().Result;
		}

		public async Task<get_numeric_types_math_Result_result> get_numeric_types_mathAsync ()
		{
			get_numeric_types_math_Result_result Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_numeric_types_math\" (@result);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						get_numeric_types_math_Result_result Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_numeric_types_math_Result_result
								{
									res_numeric_numeric = Rdr["res_numeric_numeric"] as decimal?,
									type_numeric_numeric = Rdr["type_numeric_numeric"] as string,
									res_numeric_real = Rdr["res_numeric_real"] as double?,
									type_numeric_real = Rdr["type_numeric_real"] as string,
									res_numeric_float = Rdr["res_numeric_float"] as double?,
									type_numeric_float = Rdr["type_numeric_float"] as string,
									res_numeric_int = Rdr["res_numeric_int"] as decimal?,
									type_numeric_int = Rdr["type_numeric_int"] as string,
									res_numeric_smallint = Rdr["res_numeric_smallint"] as decimal?,
									type_numeric_smallint = Rdr["type_numeric_smallint"] as string,
									res_numeric_bigint = Rdr["res_numeric_bigint"] as decimal?,
									type_numeric_bigint = Rdr["type_numeric_bigint"] as string,
									res_real_numeric = Rdr["res_real_numeric"] as double?,
									type_real_numeric = Rdr["type_real_numeric"] as string,
									res_real_real = Rdr["res_real_real"] as float?,
									type_real_real = Rdr["type_real_real"] as string,
									res_real_float = Rdr["res_real_float"] as double?,
									type_real_float = Rdr["type_real_float"] as string,
									res_real_int = Rdr["res_real_int"] as double?,
									type_real_int = Rdr["type_real_int"] as string,
									res_real_smallint = Rdr["res_real_smallint"] as double?,
									type_real_smallint = Rdr["type_real_smallint"] as string,
									res_real_bigint = Rdr["res_real_bigint"] as double?,
									type_real_bigint = Rdr["type_real_bigint"] as string,
									res_float_numeric = Rdr["res_float_numeric"] as double?,
									type_float_numeric = Rdr["type_float_numeric"] as string,
									res_float_real = Rdr["res_float_real"] as double?,
									type_float_real = Rdr["type_float_real"] as string,
									res_float_float = Rdr["res_float_float"] as double?,
									type_float_float = Rdr["type_float_float"] as string,
									res_float_int = Rdr["res_float_int"] as double?,
									type_float_int = Rdr["type_float_int"] as string,
									res_float_smallint = Rdr["res_float_smallint"] as double?,
									type_float_smallint = Rdr["type_float_smallint"] as string,
									res_float_bigint = Rdr["res_float_bigint"] as double?,
									type_float_bigint = Rdr["type_float_bigint"] as string,
									res_int_numeric = Rdr["res_int_numeric"] as decimal?,
									type_int_numeric = Rdr["type_int_numeric"] as string,
									res_int_real = Rdr["res_int_real"] as double?,
									type_int_real = Rdr["type_int_real"] as string,
									res_int_float = Rdr["res_int_float"] as double?,
									type_int_float = Rdr["type_int_float"] as string,
									res_int_int = Rdr["res_int_int"] as int?,
									type_int_int = Rdr["type_int_int"] as string,
									res_int_smallint = Rdr["res_int_smallint"] as int?,
									type_int_smallint = Rdr["type_int_smallint"] as string,
									res_int_bigint = Rdr["res_int_bigint"] as long?,
									type_int_bigint = Rdr["type_int_bigint"] as string,
									res_smallint_numeric = Rdr["res_smallint_numeric"] as decimal?,
									type_smallint_numeric = Rdr["type_smallint_numeric"] as string,
									res_smallint_real = Rdr["res_smallint_real"] as double?,
									type_smallint_real = Rdr["type_smallint_real"] as string,
									res_smallint_float = Rdr["res_smallint_float"] as double?,
									type_smallint_float = Rdr["type_smallint_float"] as string,
									res_smallint_int = Rdr["res_smallint_int"] as int?,
									type_smallint_int = Rdr["type_smallint_int"] as string,
									res_smallint_smallint = Rdr["res_smallint_smallint"] as short?,
									type_smallint_smallint = Rdr["type_smallint_smallint"] as string,
									res_smallint_bigint = Rdr["res_smallint_bigint"] as long?,
									type_smallint_bigint = Rdr["type_smallint_bigint"] as string,
									res_bigint_numeric = Rdr["res_bigint_numeric"] as decimal?,
									type_bigint_numeric = Rdr["type_bigint_numeric"] as string,
									res_bigint_real = Rdr["res_bigint_real"] as double?,
									type_bigint_real = Rdr["type_bigint_real"] as string,
									res_bigint_float = Rdr["res_bigint_float"] as double?,
									type_bigint_float = Rdr["type_bigint_float"] as string,
									res_bigint_int = Rdr["res_bigint_int"] as long?,
									type_bigint_int = Rdr["type_bigint_int"] as string,
									res_bigint_smallint = Rdr["res_bigint_smallint"] as long?,
									type_bigint_smallint = Rdr["type_bigint_smallint"] as string,
									res_bigint_bigint = Rdr["res_bigint_bigint"] as long?,
									type_bigint_bigint = Rdr["type_bigint_bigint"] as string
								};
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_operators
		public class get_operators_Result_result
		{
			public bool? t1;
			public bool? t2;
			public bool? t3;
			public int? sum;
		}

		public get_operators_Result_result get_operators ()
		{
			return get_operatorsAsync ().Result;
		}

		public async Task<get_operators_Result_result> get_operatorsAsync ()
		{
			get_operators_Result_result Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_operators\" (@result);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						get_operators_Result_result Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_operators_Result_result
								{
									t1 = Rdr["t1"] as bool?,
									t2 = Rdr["t2"] as bool?,
									t3 = Rdr["t3"] as bool?,
									sum = Rdr["sum"] as int?
								};
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_returning
		public class get_returning_Result_insert_result_1
		{
			public int? notch;
			public string category;
		}

		public class get_returning_Result_insert_result_2
		{
			public int? id;
			public string category;
			public int? height;
			public int? stub;
		}

		public class get_returning_Result_delete_result_1
		{
			public Guid? id_person;
			public int? id_room;
		}

		public class get_returning_Result
		{
			public List<get_returning_Result_insert_result_1> insert_result_1;
			public List<get_returning_Result_insert_result_2> insert_result_2;
			public List<get_returning_Result_delete_result_1> delete_result_1;
		}

		public get_returning_Result get_returning ()
		{
			return get_returningAsync ().Result;
		}

		public async Task<get_returning_Result> get_returningAsync ()
		{
			get_returning_Result Result = new get_returning_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_returning\" (@insert_result_1, @insert_result_2, @delete_result_1);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@insert_result_1", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "insert_result_1" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@insert_result_2", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "insert_result_2" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@delete_result_1", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "delete_result_1" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"insert_result_1\";";
						List<get_returning_Result_insert_result_1> Set = new List<get_returning_Result_insert_result_1> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_returning_Result_insert_result_1
								{
									notch = Rdr["notch"] as int?,
									category = Rdr["category"] as string
								});
							}
						}

						Result.insert_result_1 = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"insert_result_2\";";
						List<get_returning_Result_insert_result_2> Set = new List<get_returning_Result_insert_result_2> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_returning_Result_insert_result_2
								{
									id = Rdr["id"] as int?,
									category = Rdr["category"] as string,
									height = Rdr["height"] as int?,
									stub = Rdr["stub"] as int?
								});
							}
						}

						Result.insert_result_2 = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"delete_result_1\";";
						List<get_returning_Result_delete_result_1> Set = new List<get_returning_Result_delete_result_1> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_returning_Result_delete_result_1
								{
									id_person = Rdr["id_person"] as Guid?,
									id_room = Rdr["id_room"] as int?
								});
							}
						}

						Result.delete_result_1 = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"insert_result_1\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"insert_result_2\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"delete_result_1\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_scalar
		public string get_scalar ()
		{
			return get_scalarAsync ().Result;
		}

		public async Task<string> get_scalarAsync ()
		{
			string Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_scalar\" (@partial);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@partial", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "partial" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"partial\";";
						string Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["name"] as string;
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"partial\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_single_row
		public class get_single_row_Result_partial
		{
			public int? id;
			public string name;
			public string _float;
		}

		public get_single_row_Result_partial get_single_row ()
		{
			return get_single_rowAsync ().Result;
		}

		public async Task<get_single_row_Result_partial> get_single_rowAsync ()
		{
			get_single_row_Result_partial Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_single_row\" (@partial);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@partial", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "partial" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"partial\";";
						get_single_row_Result_partial Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_single_row_Result_partial
								{
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string,
									_float = Rdr["float"] as string
								};
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"partial\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_user_and_details
		public class get_user_and_details_Result_details
		{
			public Guid? id;
			public string lastname;
			public string firstname;
			public LocalDate? dob;
			public long? tab_num;
			public int? effect;
		}

		public class get_user_and_details_Result
		{
			public string user;
			public List<get_user_and_details_Result_details> details;
		}

		public get_user_and_details_Result get_user_and_details ()
		{
			return get_user_and_detailsAsync ().Result;
		}

		public async Task<get_user_and_details_Result> get_user_and_detailsAsync ()
		{
			get_user_and_details_Result Result = new get_user_and_details_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_user_and_details\" (@user, @details);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@user", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "user" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@details", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "details" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"user\";";
						string Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["name"] as string;
							}
						}

						Result.user = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"details\";";
						List<get_user_and_details_Result_details> Set = new List<get_user_and_details_Result_details> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new get_user_and_details_Result_details
								{
									id = Rdr["id"] as Guid?,
									lastname = Rdr["lastname"] as string,
									firstname = Rdr["firstname"] as string,
									dob = Rdr["dob"] as LocalDate?,
									tab_num = Rdr["tab_num"] as long?,
									effect = Rdr["effect"] as int?
								});
							}
						}

						Result.details = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"user\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"details\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region get_value_types
		public class get_value_types_Result_result
		{
			public int? _int;
			public decimal? numeric;
			public decimal? numeric_e_neg;
			public decimal? numeric_e_pos;
			public decimal? numeric_e_def;
			public float? real;
			public double? _float;
			public decimal? money;
			public string varchar;
			public string given;
			public LocalDateTime? remote;
			public bool? _bool;
			public uint? regtype;
			public string last_status;
			public string[] packages;
			public long? owner_sum;
			public TryWrapper.CardType? card_type;
			public string full_qual;
			public string full_qual_quot;
			public string full_qual_quot_2;
		}

		public class get_value_types_Result_expressions_2
		{
			public Instant? timestamptz;
			public decimal? money;
			public LocalDateTime? timestamp_2;
			public LocalDateTime? timestamp_3;
			public Period interval;
			public bool? _bool;
			public bool? bool_2;
			public bool? bool_3;
			public string varchar_1;
			public string varchar_2;
			public string varchar_3;
			public long? bigint;
			public bool? between_2;
			public byte[] loop;
			public decimal? money_2;
			public long? array_agg;
			public string array_agg_2;
			public string _case;
			public bool? r1;
			public bool? r2;
			public bool? r3;
			public bool? r4;
			public bool? r5;
			public bool? r6;
			public bool? r7;
			public bool? r8;
			public bool? array_overlap;
		}

		public class get_value_types_Result_nulls
		{
			public int? _int;
			public decimal? numeric;
			public double? _float;
			public float? real;
			public long? bigint;
			public short? smallint;
			public decimal? money;
			public string varchar;
			public Guid? uuid;
			public LocalDateTime? timestamp;
			public LocalDate? date;
			public bool? _bool;
			public TryWrapper.CardType? card_type;
			public bool? coalesce_first;
			public decimal? coalesce_second;
		}

		public class get_value_types_Result
		{
			public get_value_types_Result_result result;
			public get_value_types_Result_expressions_2 expressions_2;
			public get_value_types_Result_nulls nulls;
		}

		public get_value_types_Result get_value_types ()
		{
			return get_value_typesAsync ().Result;
		}

		public async Task<get_value_types_Result> get_value_typesAsync ()
		{
			get_value_types_Result Result = new get_value_types_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_value_types\" (@result, @expressions_2, @nulls);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@expressions_2", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "expressions_2" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@nulls", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "nulls" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						get_value_types_Result_result Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_value_types_Result_result
								{
									_int = Rdr["int"] as int?,
									numeric = Rdr["numeric"] as decimal?,
									numeric_e_neg = Rdr["numeric_e_neg"] as decimal?,
									numeric_e_pos = Rdr["numeric_e_pos"] as decimal?,
									numeric_e_def = Rdr["numeric_e_def"] as decimal?,
									real = Rdr["real"] as float?,
									_float = Rdr["float"] as double?,
									money = Rdr["money"] as decimal?,
									varchar = Rdr["varchar"] as string,
									given = Rdr["given"] as string,
									remote = Rdr["remote"] as LocalDateTime?,
									_bool = Rdr["bool"] as bool?,
									regtype = Rdr["regtype"] as uint?,
									last_status = Rdr["last_status"] as string,
									packages = Rdr["packages"] as string[],
									owner_sum = Rdr["owner_sum"] as long?,
									card_type = Rdr["card_type"] as TryWrapper.CardType?,
									full_qual = Rdr["full_qual"] as string,
									full_qual_quot = Rdr["full_qual_quot"] as string,
									full_qual_quot_2 = Rdr["full_qual_quot_2"] as string
								};
							}
						}

						Result.result = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"expressions_2\";";
						get_value_types_Result_expressions_2 Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_value_types_Result_expressions_2
								{
									timestamptz = Rdr["timestamptz"] as Instant?,
									money = Rdr["money"] as decimal?,
									timestamp_2 = Rdr["timestamp 2"] as LocalDateTime?,
									timestamp_3 = Rdr["timestamp 3"] as LocalDateTime?,
									interval = Rdr["interval"] as Period,
									_bool = Rdr["bool"] as bool?,
									bool_2 = Rdr["bool 2"] as bool?,
									bool_3 = Rdr["bool 3"] as bool?,
									varchar_1 = Rdr["varchar 1"] as string,
									varchar_2 = Rdr["varchar 2"] as string,
									varchar_3 = Rdr["varchar 3"] as string,
									bigint = Rdr["bigint"] as long?,
									between_2 = Rdr["between 2"] as bool?,
									loop = Rdr["loop"] as byte[],
									money_2 = Rdr["money 2"] as decimal?,
									array_agg = Rdr["array_agg"] as long?,
									array_agg_2 = Rdr["array_agg_2"] as string,
									_case = Rdr["case"] as string,
									r1 = Rdr["r1"] as bool?,
									r2 = Rdr["r2"] as bool?,
									r3 = Rdr["r3"] as bool?,
									r4 = Rdr["r4"] as bool?,
									r5 = Rdr["r5"] as bool?,
									r6 = Rdr["r6"] as bool?,
									r7 = Rdr["r7"] as bool?,
									r8 = Rdr["r8"] as bool?,
									array_overlap = Rdr["array_overlap"] as bool?
								};
							}
						}

						Result.expressions_2 = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"nulls\";";
						get_value_types_Result_nulls Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new get_value_types_Result_nulls
								{
									_int = Rdr["int"] as int?,
									numeric = Rdr["numeric"] as decimal?,
									_float = Rdr["float"] as double?,
									real = Rdr["real"] as float?,
									bigint = Rdr["bigint"] as long?,
									smallint = Rdr["smallint"] as short?,
									money = Rdr["money"] as decimal?,
									varchar = Rdr["varchar"] as string,
									uuid = Rdr["uuid"] as Guid?,
									timestamp = Rdr["timestamp"] as LocalDateTime?,
									date = Rdr["date"] as LocalDate?,
									_bool = Rdr["bool"] as bool?,
									card_type = Rdr["card_type"] as TryWrapper.CardType?,
									coalesce_first = Rdr["coalesce_first"] as bool?,
									coalesce_second = Rdr["coalesce_second"] as decimal?
								};
							}
						}

						Result.nulls = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"expressions_2\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"nulls\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region getdeptchain
		public class getdeptchain_Result_res01
		{
			public int? id;
			public string name;
			public int? _float;
			public decimal? extract;
		}

		public getdeptchain_Result_res01 getdeptchain (int? p_id)
		{
			return getdeptchainAsync (p_id).Result;
		}

		public async Task<getdeptchain_Result_res01> getdeptchainAsync (int? p_id)
		{
			getdeptchain_Result_res01 Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"getdeptchain\" (@p_id, @res01);";
					Cmd.Parameters.AddWithValue ("@p_id", (object)p_id ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@res01", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "res01" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"res01\";";
						getdeptchain_Result_res01 Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new getdeptchain_Result_res01
								{
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string,
									_float = Rdr["float"] as int?,
									extract = Rdr["extract"] as decimal?
								};
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"res01\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region insert_conflict
		public void insert_conflict ()
		{
			insert_conflictAsync ().Wait ();
		}

		public async Task insert_conflictAsync ()
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"alexey\".\"insert_conflict\" ();";

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion

		#region persons_getall
		public class persons_getall_Result_users
		{
			public int? num;
			public Guid? id;
			public string lastname;
			public string firstname;
			public LocalDate? dob;
			public long? tab_num;
			public string status;
			public int? effect;
		}

		public class persons_getall_Result_ownership
		{
			public string lastname;
			public int? num;
			public int? id;
			public string name;
			public int[] extents;
		}

		public class persons_getall_Result
		{
			public List<persons_getall_Result_users> users;
			public List<persons_getall_Result_ownership> ownership;
		}

		public persons_getall_Result persons_getall ()
		{
			return persons_getallAsync ().Result;
		}

		public async Task<persons_getall_Result> persons_getallAsync ()
		{
			persons_getall_Result Result = new persons_getall_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"persons_getall\" (@users, @ownership);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@users", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "users" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@ownership", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "ownership" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"users\";";
						List<persons_getall_Result_users> Set = new List<persons_getall_Result_users> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new persons_getall_Result_users
								{
									num = Rdr["num"] as int?,
									id = Rdr["id"] as Guid?,
									lastname = Rdr["lastname"] as string,
									firstname = Rdr["firstname"] as string,
									dob = Rdr["dob"] as LocalDate?,
									tab_num = Rdr["tab_num"] as long?,
									status = Rdr["status"] as string,
									effect = Rdr["effect"] as int?
								});
							}
						}

						Result.users = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"ownership\";";
						List<persons_getall_Result_ownership> Set = new List<persons_getall_Result_ownership> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new persons_getall_Result_ownership
								{
									lastname = Rdr["lastname"] as string,
									num = Rdr["num"] as int?,
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string,
									extents = Rdr["extents"] as int[]
								});
							}
						}

						Result.ownership = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"users\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"ownership\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region roomsforperson
		public class roomsforperson_Result_res01
		{
			public Guid? id;
			public string lastname;
			public string firstname;
			public LocalDate? dob;
			public long? tab_num;
			public int[] them_all;
			public int? piece;
			public int? sample;
			public int? had_it;
			public string status;
			public string redirect;
			public int? compound;
		}

		public class roomsforperson_Result_res02
		{
			public int? id;
			public string name;
			public int[] extents;
			public int[] ord;
			public string json;
		}

		public class roomsforperson_Result
		{
			public List<roomsforperson_Result_res01> res01;
			public List<roomsforperson_Result_res02> res02;
		}

		/// <param name="card_type">Value from alexey.mapped</param>
		public roomsforperson_Result roomsforperson (
				Guid? id_person,
				int[] bwahaha,
				ref int[] get_array,
				TryWrapper.CardType? card_type,
				string name,
				bool? over,
				LocalDate? dt01,
				LocalDateTime? dt02,
				Period dt03,
				LocalTime? dt04,
				string txt,
				decimal? amount,
				ref long? came,
				ref string done
			)
		{
			roomsforperson_Result Result = new roomsforperson_Result ();

			using (var Tran = DbProc.BeginTransactionOptionalAsync ().Result)
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"roomsforperson\" (@id_person, @res01, @res02, @bwahaha, @get_array, @card_type::\"alexey\".\"mapped\", @name, @over, @dt01, @dt02, @dt03, @dt04, @txt, @amount, @came, @done);";
					Cmd.Parameters.AddWithValue ("@id_person", (object)id_person ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@res01", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "res01" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@res02", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "res02" });
					Cmd.Parameters.AddWithValue ("@bwahaha", (object)bwahaha ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@get_array", (object)get_array ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@card_type", (object)card_type ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@name", (object)name ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@over", (object)over ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@dt01", (object)dt01 ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@dt02", (object)dt02 ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@dt03", (object)dt03 ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@dt04", (object)dt04 ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@txt", (object)txt ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@amount", (object)amount ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@came", (object)came ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@done", (object)done ?? DBNull.Value).Direction = ParameterDirection.InputOutput;

					Cmd.ExecuteNonQuery ();

					get_array = Cmd.Parameters["@get_array"].Value as int[];
					came = Cmd.Parameters["@came"].Value as long?;
					done = Cmd.Parameters["@done"].Value as string;

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"res01\";";
						List<roomsforperson_Result_res01> Set = new List<roomsforperson_Result_res01> ();

						using (var Rdr = ResCmd.ExecuteReader ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new roomsforperson_Result_res01
								{
									id = Rdr["id"] as Guid?,
									lastname = Rdr["lastname"] as string,
									firstname = Rdr["firstname"] as string,
									dob = Rdr["dob"] as LocalDate?,
									tab_num = Rdr["tab_num"] as long?,
									them_all = Rdr["them_all"] as int[],
									piece = Rdr["piece"] as int?,
									sample = Rdr["sample"] as int?,
									had_it = Rdr["had it"] as int?,
									status = Rdr["status"] as string,
									redirect = Rdr["redirect"] as string,
									compound = Rdr["compound"] as int?
								});
							}
						}

						Result.res01 = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"res02\";";
						List<roomsforperson_Result_res02> Set = new List<roomsforperson_Result_res02> ();

						using (var Rdr = ResCmd.ExecuteReader ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new roomsforperson_Result_res02
								{
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string,
									extents = Rdr["extents"] as int[],
									ord = Rdr["ord"] as int[],
									json = Rdr["json"] as string
								});
							}
						}

						Result.res02 = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"res01\";";
						cmdClose.ExecuteNonQuery ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"res02\";";
						cmdClose.ExecuteNonQuery ();
					}

					if (Tran != null)
					{
						Tran.Commit ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_duplicate_open
		public class test_duplicate_open_Result_single
		{
			public int? id;
			public string name;
		}

		public class test_duplicate_open_Result
		{
			public int? scalar;
			public test_duplicate_open_Result_single single;
		}

		public test_duplicate_open_Result test_duplicate_open (int? i)
		{
			return test_duplicate_openAsync (i).Result;
		}

		public async Task<test_duplicate_open_Result> test_duplicate_openAsync (int? i)
		{
			test_duplicate_open_Result Result = new test_duplicate_open_Result ();

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_duplicate_open\" (@i, @scalar, @single);";
					Cmd.Parameters.AddWithValue ("@i", (object)i ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@scalar", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "scalar" });
					Cmd.Parameters.Add (new NpgsqlParameter ("@single", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "single" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"scalar\";";
						int? Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["id"] as int?;
							}
						}

						Result.scalar = Set;
					}

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"single\";";
						test_duplicate_open_Result_single Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new test_duplicate_open_Result_single
								{
									id = Rdr["id"] as int?,
									name = Rdr["name"] as string
								};
							}
						}

						Result.single = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"scalar\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"single\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_exception
		public void test_exception (string message, int? retry)
		{
			test_exceptionAsync (message, retry).Wait ();
		}

		public async Task test_exceptionAsync (string message, int? retry)
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"alexey\".\"test_exception\" (@message, @retry);";
				Cmd.Parameters.AddWithValue ("@message", (object)message ?? DBNull.Value);
				Cmd.Parameters.AddWithValue ("@retry", (object)retry ?? DBNull.Value);

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion

		#region test_from_select
		public class test_from_select_Result_result
		{
			public string lastname;
			public string room;
			public string own;
		}

		public List<test_from_select_Result_result> test_from_select ()
		{
			return test_from_selectAsync ().Result;
		}

		public async Task<List<test_from_select_Result_result>> test_from_selectAsync ()
		{
			List<test_from_select_Result_result> Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_from_select\" (@result);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@result", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result\";";
						List<test_from_select_Result_result> Set = new List<test_from_select_Result_result> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new test_from_select_Result_result
								{
									lastname = Rdr["lastname"] as string,
									room = Rdr["room"] as string,
									own = Rdr["own"] as string
								});
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_json
		public int? test_json (string data, string data_b)
		{
			return test_jsonAsync (data, data_b).Result;
		}

		public async Task<int?> test_jsonAsync (string data, string data_b)
		{
			int? Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_json\" (@data::json, @data_b::jsonb, @sample);";
					Cmd.Parameters.AddWithValue ("@data", (object)data ?? DBNull.Value);
					Cmd.Parameters.AddWithValue ("@data_b", (object)data_b ?? DBNull.Value);
					Cmd.Parameters.Add (new NpgsqlParameter ("@sample", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "sample" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"sample\";";
						int? Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["result"] as int?;
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"sample\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_loops
		public void test_loops ()
		{
			test_loopsAsync ().Wait ();
		}

		public async Task test_loopsAsync ()
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"alexey\".\"test_loops\" ();";

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion

		#region test_out
		/// <param name="p_status">Value from alexey.app_status</param>
		/// <param name="p_valid_statuses">Value from alexey.app_status</param>
		public int? test_out (
				ref int? p_int,
				ref int[] p_int_arr,
				ref bool? p_bool,
				ref bool[] p_bool_arr,
				ref LocalDate? p_date,
				ref LocalDate[] p_date_arr,
				ref Instant? p_instant,
				ref Instant[] p_instant_arr,
				ref LocalDateTime? p_datetime,
				ref LocalDateTime[] p_datetime_arr,
				ref string p_varchar,
				ref string[] p_varchar_arr,
				ref byte[] p_bytea,
				ref string p_status,
				ref string[] p_valid_statuses,
				ref TryWrapper.Town p_city,
				ref TryWrapper.Payer p_enc_pi_payer
			)
		{
			int? Result = null;

			using (var Tran = DbProc.BeginTransactionOptionalAsync ().Result)
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_out\" (@p_int, @p_int_arr, @p_bool, @p_bool_arr, @p_date, @p_date_arr, @p_instant, @p_instant_arr, @p_datetime, @p_datetime_arr, @p_varchar, @p_varchar_arr, @p_bytea, @p_status::\"alexey\".\"app_status\", @p_valid_statuses::\"alexey\".\"app_status\"[], @p_city, @p_enc_pi_payer, @result_1);";
					Cmd.Parameters.AddWithValue ("@p_int", (object)p_int ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_int_arr", (object)p_int_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_bool", (object)p_bool ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_bool_arr", (object)p_bool_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_date", (object)p_date ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_date_arr", (object)p_date_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_instant", (object)p_instant ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_instant_arr", (object)p_instant_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_datetime", (object)p_datetime ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_datetime_arr", (object)p_datetime_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_varchar", (object)p_varchar ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_varchar_arr", (object)p_varchar_arr ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_bytea", (object)p_bytea ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_status", (object)p_status ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_valid_statuses", (object)p_valid_statuses ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_city", (object)p_city ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.AddWithValue ("@p_enc_pi_payer", (object)DbProc.WriteEncrypted (p_enc_pi_payer) ?? DBNull.Value).Direction = ParameterDirection.InputOutput;
					Cmd.Parameters.Add (new NpgsqlParameter ("@result_1", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "result_1" });

					Cmd.ExecuteNonQuery ();

					p_int = Cmd.Parameters["@p_int"].Value as int?;
					p_int_arr = Cmd.Parameters["@p_int_arr"].Value as int[];
					p_bool = Cmd.Parameters["@p_bool"].Value as bool?;
					p_bool_arr = Cmd.Parameters["@p_bool_arr"].Value as bool[];
					p_date = Cmd.Parameters["@p_date"].Value as LocalDate?;
					p_date_arr = Cmd.Parameters["@p_date_arr"].Value as LocalDate[];
					p_instant = Cmd.Parameters["@p_instant"].Value as Instant?;
					p_instant_arr = Cmd.Parameters["@p_instant_arr"].Value as Instant[];
					p_datetime = Cmd.Parameters["@p_datetime"].Value as LocalDateTime?;
					p_datetime_arr = Cmd.Parameters["@p_datetime_arr"].Value as LocalDateTime[];
					p_varchar = Cmd.Parameters["@p_varchar"].Value as string;
					p_varchar_arr = Cmd.Parameters["@p_varchar_arr"].Value as string[];
					p_bytea = Cmd.Parameters["@p_bytea"].Value as byte[];
					p_status = Cmd.Parameters["@p_status"].Value as string;
					p_valid_statuses = Cmd.Parameters["@p_valid_statuses"].Value as string[];
					p_city = Cmd.Parameters["@p_city"].Value as TryWrapper.Town;
					p_enc_pi_payer = DbProc.ReadEncrypted<TryWrapper.Payer> (Cmd.Parameters["@p_enc_pi_payer"].Value as byte[]);

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"result_1\";";
						int? Set = null;

						using (var Rdr = ResCmd.ExecuteReader ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["in"] as int?;
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"result_1\";";
						cmdClose.ExecuteNonQuery ();
					}

					if (Tran != null)
					{
						Tran.Commit ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_read_encrypted
		public class test_read_encrypted_Result_sample
		{
			public int? id;
			public byte[] hash;
			public TryWrapper.Payer enc_pi_payer;
		}

		public test_read_encrypted_Result_sample test_read_encrypted ()
		{
			return test_read_encryptedAsync ().Result;
		}

		public async Task<test_read_encrypted_Result_sample> test_read_encryptedAsync ()
		{
			test_read_encrypted_Result_sample Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_read_encrypted\" (@sample);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@sample", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "sample" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"sample\";";
						test_read_encrypted_Result_sample Set = null;

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = new test_read_encrypted_Result_sample
								{
									id = Rdr["id"] as int?,
									hash = Rdr["hash"] as byte[],
									enc_pi_payer = DbProc.ReadEncrypted<TryWrapper.Payer> (Rdr["enc_pi_payer"] as byte[])
								};
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"sample\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_temp_table
		public class test_temp_table_Result_sample
		{
			public string id;
			public string lastname;
			public bool? is_artist;
			public Instant? now;
		}

		public List<test_temp_table_Result_sample> test_temp_table ()
		{
			return test_temp_tableAsync ().Result;
		}

		public async Task<List<test_temp_table_Result_sample>> test_temp_tableAsync ()
		{
			List<test_temp_table_Result_sample> Result = null;

			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"test_temp_table\" (@sample);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@sample", NpgsqlDbType.Refcursor) { Direction = ParameterDirection.InputOutput, Value = "sample" });

					await Cmd.ExecuteNonQueryAsync ();

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"sample\";";
						List<test_temp_table_Result_sample> Set = new List<test_temp_table_Result_sample> ();

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							while (Rdr.Read ())
							{
								Set.Add (new test_temp_table_Result_sample
								{
									id = Rdr["id"] as string,
									lastname = Rdr["lastname"] as string,
									is_artist = Rdr["is_artist"] as bool?,
									now = Rdr["now"] as Instant?
								});
							}
						}

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"sample\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		#endregion

		#region test_write_encrypted
		public void test_write_encrypted (byte[] p_hash, TryWrapper.Payer p_enc_pi_payer)
		{
			test_write_encryptedAsync (p_hash, p_enc_pi_payer).Wait ();
		}

		public async Task test_write_encryptedAsync (byte[] p_hash, TryWrapper.Payer p_enc_pi_payer)
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"alexey\".\"test_write_encrypted\" (@p_hash, @p_enc_pi_payer);";
				Cmd.Parameters.AddWithValue ("@p_hash", (object)p_hash ?? DBNull.Value);
				Cmd.Parameters.AddWithValue ("@p_enc_pi_payer", (object)DbProc.WriteEncrypted (p_enc_pi_payer) ?? DBNull.Value);

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion
	}

	public class ext
	{
		public DbProc DbProc;
		public NpgsqlConnection Conn => DbProc.Conn;

		public ext (DbProc DbProc)
		{
			this.DbProc = DbProc;
		}

		#region calc
		public void calc ()
		{
			calcAsync ().Wait ();
		}

		public async Task calcAsync ()
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"ext\".\"calc\" ();";

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion

		#region empty
		public void empty ()
		{
			emptyAsync ().Wait ();
		}

		public async Task emptyAsync ()
		{
			using (var Cmd = Conn.CreateCommand ())
			{
				Cmd.CommandText = "call \"ext\".\"empty\" ();";

				await Cmd.ExecuteNonQueryAsync ();
			}
		}
		#endregion
	}

	public class no_proc
	{
		public static class package
		{
			public const string open = "open";
			public const string _sealed = "sealed";
			public const string enclosed = "enclosed";
		}

		public DbProc DbProc;
		public NpgsqlConnection Conn => DbProc.Conn;

		public no_proc (DbProc DbProc)
		{
			this.DbProc = DbProc;
		}

	}
}
