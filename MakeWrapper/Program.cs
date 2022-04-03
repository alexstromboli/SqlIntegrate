using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Newtonsoft.Json;

using ParseProcs;
using ParseProcs.Datasets;
using Utils.CodeGeneration;

namespace MakeWrapper
{
	public static class Utils
	{
		public class Item<T>
		{
			public T Value;
			public bool IsFirst;
			public int Index;
		}

		public static IEnumerable<Item<T>> Indexed<T> (this IEnumerable<T> coll)
		{
			int i = -1;
			return coll.Select (e =>
			{
				++i;
				return new Item<T> { Value = e, Index = i, IsFirst = i == 0 };
			});
		}

		public static string ToDoubleQuotes (this string Input)
		{
			return new StringBuilder ("\"")
					.Append (Input.Replace ("\"", "\\\""))
					.Append ('\"')
					.ToString ()
				;
		}
	}

	// this map is closed
	// only for native .NET types
	// third parties' types (like NodaTime) must be handled separately
	// as their assemblies have no real need to be linked to this utility
	// names are sufficient for purposes of code generator
	public class ClrType
	{
		protected static Dictionary<Type, ClrType> _Map;
		public static IReadOnlyDictionary<Type, ClrType> Map => _Map;

		public string CsName { get; protected set; }
		public string CsNullableName { get; protected set; }

		static ClrType ()
		{
			_Map = new Dictionary<Type, ClrType> ();

			Add (typeof (bool), "bool");
			Add (typeof (int), "int");
			Add (typeof (Int16), "short");
			Add (typeof (Int64), "long");
			Add (typeof (decimal), "decimal");
			Add (typeof (float), "float");
			Add (typeof (double), "double");
			Add (typeof (string), "string");
			Add (typeof (Guid), "Guid");
			Add (typeof (DateTime), "DateTime");
			Add (typeof (TimeSpan), "TimeSpan");
		}

		protected static void Add (Type NetType, string CsName)
		{
			string CsNullableName = NetType.IsValueType ? CsName + "?" : CsName;

			ClrType T = new ClrType { CsName = CsName, CsNullableName = CsNullableName };
			_Map[NetType] = T;
		}
	}

	class Program
	{
		static void Main (string[] args)
		{
			string ModuleInputPath = Path.GetFullPath (args[0]);
			string ModuleJson = File.ReadAllText (ModuleInputPath);
			Module Module = JsonConvert.DeserializeObject<Module> (ModuleJson);

			bool UseNodaTime = true;
			string OutputPath = Path.GetFullPath ("dbproc.cs");

			// build type map
			// Postgres type name to C# type name
			// including arrays
			Dictionary<string, string> CastToDict = new Dictionary<string, string> ();
			foreach (var p in PSqlType.Map.Where (p => !p.Value.IsArray))
			{
				if (ClrType.Map.TryGetValue (p.Value.ClrType, out var ct))
				{
					CastToDict[p.Key] = ct.CsNullableName;
					CastToDict[p.Key + "[]"] = ct.CsNullableName + "[]";
				}
			}
			CastToDict["bytea"] = "byte[]";

			if (UseNodaTime)
			{
				foreach (var p in PSqlType.Map.Where (p => !p.Value.IsArray))
				{
					if (ClrType.Map.TryGetValue (p.Value.ClrType, out var ct))
					{
						if (p.Value.IsDate)
						{
							CastToDict[p.Key] = "Instant?";
							CastToDict[p.Key + "[]"] = "Instant?[]";
						}
						else if (p.Value.IsTimeSpan)
						{
							CastToDict[p.Key] = "LocalTime?";
							CastToDict[p.Key + "[]"] = "LocalTime?[]";
						}
					}
				}

				CastToDict["date"] = "LocalDate?";
				CastToDict["date[]"] = "LocalDate?[]";

				CastToDict["interval"] = "Duration?";
				CastToDict["interval[]"] = "Duration?[]";
			}

			IndentedTextBuilder sb = new IndentedTextBuilder ();
			sb.AppendLine (CodeGenerationUtils.AutomaticWarning)
				.AppendLine ("using Npgsql;")
				;

			using (sb.UseCurlyBraces ("namespace Gen"))
			{
				foreach (var ns in Module.Procedures
					         .GroupBy (p => p.Schema)
					         .OrderBy (g => g.Key)
					         .Indexed ()
				         )
				{
					if (!ns.IsFirst)
					{
						sb.AppendLine ();
					}

					using (sb.UseCurlyBraces ("namespace " + ns.Value.Key))
					{
						foreach (var p in ns.Value.OrderBy (p => p.Name).Indexed ())
						{
							if (!p.IsFirst)
							{
								sb.AppendLine ();
							}

							sb.AppendLine ("#region " + p.Value.Name);

							string[] Args = p.Value.Arguments
									.Where (a => a.SqlType.SqlBaseType != "refcursor")
									.Select (a => (a.IsOut ? "ref " : "") + $"{CastToDict[a.SqlType.ToString ()]} {a.Name}")
									.ToArray ()
								;

							string MethodDeclPrefix = "public void " + p.Value.Name + " (";
							if (Args.Length <= 2)
							{
								string ArgsDef = string.Join (", ", Args);
								sb.AppendLine (MethodDeclPrefix + ArgsDef + ")");
							}
							else
							{
								sb.AppendLine (MethodDeclPrefix);
								foreach (var a in Args.Indexed ())
								{
									sb.AppendLine ((a.IsFirst ? "  " : ", ") + a.Value, 2);
								}
								sb.AppendLine (")", 1);
							}

							using (sb.UseCurlyBraces ())
							{
								using (p.Value.ResultSets.Count > 0
									       ? sb.UseCurlyBraces ("using (var Tran = Conn.BeginTransaction ())")
									       : null)
								{
									using (sb.UseCurlyBraces ("using (var Cmd = Conn.CreateCommand ())"))
									{
										string Params = string.Join (", ", p.Value.Arguments.Select (a => "@" + a.Name));
										string Call =
											$"call {p.Value.Schema.ToDoubleQuotes ()}.{p.Value.Name.ToDoubleQuotes ()} ({Params});"
												.ToDoubleQuotes ();
										sb.AppendLine ($"Cmd.CommandText = {Call};");

										foreach (var a in p.Value.Arguments)
										{
											if (a.SqlType.SqlBaseType == "refcursor")
											{
												if (a.IsOut)
												{
													sb.AppendLine ($"Cmd.Parameters.Add (new NpgsqlParameter (\"@{a.Name}\", NpgsqlDbType.Refcursor) {{ Direction = ParameterDirection.InputOutput, Value = \"{a.Name}\" }});");
												}
											}
											else
											{
												sb.AppendLine ($"Cmd.Parameters.AddWithValue (\"@{a.Name}\", (object){a.Name} ?? DBNull.Value)"
												               + (a.IsOut
													               ? ".Direction = ParameterDirection.InputOutput"
													               : "")
												               + ";");
											}
										}

										sb.AppendLine ("cmd.ExecuteNonQuery ();");

										foreach (var oa in p.Value.Arguments.Where (a => a.SqlType.SqlBaseType != "refcursor" && a.IsOut).Indexed ())
										{
											if (oa.IsFirst)
											{
												sb.AppendLine ();
											}

											sb.AppendLine ($"{oa.Value.Name} = Cmd.Parameters[{("@"+oa.Value.Name).ToDoubleQuotes ()}].Value as string;");
										}
									}
								}

								foreach (var Set in p.Value.ResultSets.Indexed ())
								{
									if (!Set.IsFirst)
									{
										sb.AppendLine ();
									}

									using (sb.AppendLine ("// " + Set.Value.Name).UseCurlyBraces ())
									{
										foreach (var c in Set.Value.Columns)
										{
											sb.AppendLine ($"{CastToDict[c.SqlType.ToString ()]} {c.Name};");
										}
									}
								}
							}

							sb.AppendLine ("#endregion ");
						}
					}
				}
			}

			CodeGenerationUtils.EnsureFileContents (OutputPath, sb.ToString ());
		}
	}
}
