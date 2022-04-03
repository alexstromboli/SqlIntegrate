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

		public static string ValidCsName (this string Name)
		{
			if (Name.Contains (' '))
			{
				return Name.Replace (' ', '_');
			}

			if (Program.CsKeywords.Contains (Name))
			{
				return "_" + Name;
			}

			return Name;
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

	class WrapperProcedureArgument
	{
		public Argument Origin;
		public string NativeName;
		public string CsName;
		public string CallParamName;
		public string ClrType;
		public bool IsCursor;
		public bool IsOut;
	}

	class Program
	{
		public static SortedSet<string> CsKeywords = new SortedSet<string> (new[] { "abstract", "event", "new", "struct", "as", "explicit", "null", "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw", "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float", "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected", "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe", "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal", "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate", "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock", "stackalloc", "else", "long", "static", "enum", "namespace", "string" });

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
				.AppendLine ()
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

					using (sb.UseCurlyBraces ("namespace " + ns.Value.Key.ValidCsName ()))
					{
						foreach (var p in ns.Value.OrderBy (p => p.Name).Indexed ())
						{
							if (!p.IsFirst)
							{
								sb.AppendLine ();
							}

							sb.AppendLine ("#region " + p.Value.Name);

							WrapperProcedureArgument[] WrapperProcedureArguments = p.Value.Arguments
									.Select (a => new WrapperProcedureArgument
									{
										Origin = a,
										NativeName = a.Name,
										CallParamName = ("@" + a.Name).ToDoubleQuotes (),
										CsName = a.Name.ValidCsName (),
										ClrType = CastToDict.TryGetValue (a.SqlType.ToString (), out var t) ? t : null,
										IsOut = a.IsOut,
										IsCursor = a.SqlType.SqlBaseType == "refcursor"
									})
									.ToArray ()
								;

							string[] Args = WrapperProcedureArguments
									.Where (a => !a.IsCursor)
									.Select (a => (a.IsOut ? "ref " : "") + $"{a.ClrType} {a.CsName}")
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
								bool UseTransaction = p.Value.ResultSets.Count > 0;

								using (UseTransaction
									       ? sb.UseCurlyBraces ("using (var Tran = Conn.BeginTransaction ())")
									       : null)
								{
									using (sb.UseCurlyBraces ("using (var Cmd = Conn.CreateCommand ())"))
									{
										string Params = string.Join (", ", WrapperProcedureArguments.Select (a => a.CallParamName));
										string Call =
											$"call {p.Value.Schema.ToDoubleQuotes ()}.{p.Value.Name.ToDoubleQuotes ()} ({Params});"
												.ToDoubleQuotes ();
										sb.AppendLine ($"Cmd.CommandText = {Call};");

										foreach (var a in WrapperProcedureArguments)
										{
											if (a.IsCursor)
											{
												if (a.IsOut)
												{
													sb.AppendLine ($"Cmd.Parameters.Add (new NpgsqlParameter ({a.CallParamName}, NpgsqlDbType.Refcursor) {{ Direction = ParameterDirection.InputOutput, Value = \"{a.CsName}\" }});");
												}
											}
											else
											{
												sb.AppendLine ($"Cmd.Parameters.AddWithValue ({a.CallParamName}, (object){a.CsName} ?? DBNull.Value)"
												               + (a.IsOut
													               ? ".Direction = ParameterDirection.InputOutput"
													               : "")
												               + ";");
											}
										}

										sb.AppendLine ()
											.AppendLine ("Cmd.ExecuteNonQuery ();");

										foreach (var oa in WrapperProcedureArguments.Where (a => !a.IsCursor && a.IsOut).Indexed ())
										{
											if (oa.IsFirst)
											{
												sb.AppendLine ();
											}

											sb.AppendLine ($"{oa.Value.CsName} = Cmd.Parameters[{oa.Value.CallParamName}].Value as {oa.Value.ClrType};");
										}

										foreach (var Set in p.Value.ResultSets)
										{
											sb.AppendLine ();
											using (sb.UseCurlyBraces ($"using (var ResCmd = Conn.CreateCommand ())"))
											{
												sb.AppendLine ($"ResCmd.CommandText = {$"FETCH ALL IN {Set.Name.ToDoubleQuotes ()};".ToDoubleQuotes ()};")
													.AppendLine ();

												using (sb.UseCurlyBraces ("using (var Rdr = ResCmd.ExecuteReader ())"))
												{
													using (sb.UseCurlyBraces ("while (Rdr.Read ())"))
													{
														foreach (var c in Set.Columns)
														{
															string ClrType = CastToDict[c.SqlType.ToString ()];
															sb.AppendLine (
																$"{ClrType} {c.Name} = Rdr[{c.Name.ToDoubleQuotes ()}] as {ClrType};");
														}
													}
												}
											}
										}

										if (UseTransaction)
										{
											sb.AppendLine ()
												.AppendLine ("Tran.Commit ();");
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
