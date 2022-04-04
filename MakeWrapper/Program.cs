using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

		public class CollItem<T> : Item<T>
		{
			public bool IsLast;
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

		public static IEnumerable<CollItem<T>> Indexed<T> (this ICollection<T> coll)
		{
			int i = -1;
			return coll.Select (e =>
			{
				++i;
				return new CollItem<T> { Value = e, Index = i, IsFirst = i == 0, IsLast = i == coll.Count - 1 };
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

		public static string ValidCsNamePart (this string Name)
		{
			if (Name.Contains (' '))
			{
				return Name.Replace (' ', '_');
			}

			return Name;
		}

		public static string ValidCsName (this string Name)
		{
			Name = Name.ValidCsNamePart ();

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

	class Schema
	{
		public class Procedure
		{
			public class Set
			{
				public class Property
				{
					public Column Origin;
					public string NativeName;
					public string CsName;
					public string ClrType;
					public Func<string, string> ReaderExpression;

					public override string ToString ()
					{
						return (CsName ?? NativeName) + " " + ClrType;
					}
				}

				public ResultSet Origin;
				public string CursorName;
				public string RowCsClassName;
				public string SetCsTypeName;
				public string PropertyName;
				public bool IsSingleRow;
				public bool IsSingleColumn => Properties.Count == 1;
				public bool IsScalar => IsSingleRow && IsSingleColumn;
				public List<Property> Properties;

				public override string ToString ()
				{
					return RowCsClassName;
				}
			}

			public ParseProcs.Datasets.Procedure Origin;
			public string NativeName;
			public string CsName;
			public WrapperProcedureArgument[] Arguments;
			public string ResultClassName;
			public List<Set> ResultSets;
			public bool HasResults => ResultSets.Count > 0;
			public bool IsSingleSet => ResultSets.Count == 1;

			public override string ToString ()
			{
				return ResultClassName;
			}
		}

		public string NativeName;
		public string CsClassName;
		public Procedure[] Procedures;
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

			//
			Schema[] Schemas = Module.Procedures
					.GroupBy (p => p.Schema)
					.OrderBy (g => g.Key)
					.Select (s =>
					{
						return new Schema
						{
							NativeName = s.Key,
							CsClassName = s.Key.ValidCsName (),
							Procedures = s
								.OrderBy (p => p.Name)
								.Select (p => new Schema.Procedure
								{
									Origin = p,
									NativeName = p.Name,
									CsName = p.Name.ValidCsName (),
									Arguments = p.Arguments
										.Select (a => new WrapperProcedureArgument
										{
											Origin = a,
											NativeName = a.Name,
											CallParamName = ("@" + a.Name).ToDoubleQuotes (),
											CsName = a.Name.ValidCsName (),
											ClrType = CastToDict.TryGetValue (a.SqlType.ToString (), out var t)
												? t
												: null,
											IsOut = a.IsOut,
											IsCursor = a.SqlType.SqlBaseType == "refcursor"
										})
										.ToArray (),
									ResultClassName = p.Name.ValidCsNamePart () + "_Result",
									ResultSets = p.ResultSets
										.Select (s =>
										{
											var Set = new Schema.Procedure.Set
											{
												Origin = s,
												CursorName = s.Name,
												RowCsClassName = p.Name.ValidCsNamePart () + "_Result_" +
												                 s.Name.ValidCsName (),
												PropertyName = s.Name.ValidCsName (),
												IsSingleRow = s.Comments
													.SelectMany (c => c.Split ('\n'))
													.Any (c => Regex.IsMatch (c, @"\s*#\s+1(\s|$)")),
												Properties = s.Columns
													.Select (c =>
													{
														var Property = new Schema.Procedure.Set.Property
														{
															Origin = c,
															NativeName = c.Name,
															ClrType = CastToDict.TryGetValue (c.SqlType.ToString (),
																out var t)
																? t
																: null,
															CsName = c.Name.ValidCsName ()
														};

														Property.ReaderExpression = rdr =>
															$"{rdr}[{Property.NativeName.ToDoubleQuotes ()}] as {Property.ClrType}";

														return Property;
													})
													.ToList ()
											};

											if (Set.IsSingleColumn)
											{
												Set.RowCsClassName = Set.Properties[0].ClrType;
											}

											Set.SetCsTypeName = Set.IsSingleRow
												? Set.RowCsClassName
												: $"List<{Set.RowCsClassName}>";

											return Set;
										})
										.ToList ()
								})
								.ToArray ()
						};
					})
					.ToArray ()
				;

			//
			IndentedTextBuilder sb = new IndentedTextBuilder ();
			sb.AppendLine (CodeGenerationUtils.AutomaticWarning)
				.AppendLine ("using System.Collections.Generic;")
				.AppendLine ()
				.AppendLine ("using Npgsql;")
				.AppendLine ()
				;

			using (sb.UseCurlyBraces ("namespace Gen"))
			{
				foreach (var nsi in Schemas.Indexed ())
				{
					var ns = nsi.Value;

					if (!nsi.IsFirst)
					{
						sb.AppendLine ();
					}

					using (sb.UseCurlyBraces ($"public class {ns.CsClassName}"))
					{
						sb.AppendLine ("public NpgsqlConnection Conn;")
							.AppendLine ("public string SchemaName;")
							.AppendLine ();

						using (sb.UseCurlyBraces ($"public {ns.CsClassName} (NpgsqlConnection Conn, string SchemaName)"))
						{
							sb.AppendLine ("this.Conn = Conn;")
								.AppendLine ("this.SchemaName = SchemaName;")
								;
						}

						sb.AppendLine ();

						foreach (var pi in ns.Procedures.Indexed ())
						{
							var p = pi.Value;

							if (!p.HasResults)
							{
								p.ResultClassName = "void";
							}
							if (p.IsSingleSet)
							{
								p.ResultClassName = p.ResultSets[0].SetCsTypeName;
							}

							//
							if (!pi.IsFirst)
							{
								sb.AppendLine ();
							}

							sb.AppendLine ("#region " + pi.Value.CsName);

							string[] Args = p.Arguments
									.Where (a => !a.IsCursor)
									.Select (a => (a.IsOut ? "ref " : "") + $"{a.ClrType} {a.CsName}")
									.ToArray ()
								;

							foreach (var Set in p.ResultSets.Where (s => !s.IsSingleColumn))
							{
								using (sb.UseCurlyBraces ($"public class {Set.RowCsClassName}"))
								{
									foreach (var P in Set.Properties)
									{
										sb.AppendLine ($"public {P.ClrType} {P.CsName};");
									}
								}

								sb.AppendLine ();
							}

							if (p.HasResults && !p.IsSingleSet)
							{
								using (sb.UseCurlyBraces ($"public class {p.ResultClassName}"))
								{
									foreach (var Set in p.ResultSets)
									{
										sb.AppendLine ($"public {Set.SetCsTypeName} {Set.CursorName};");
									}
								}

								sb.AppendLine ();
							}

							string MethodDeclPrefix = $"public {p.ResultClassName} {pi.Value.CsName} (";
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
									sb.AppendLine (a.Value + (a.IsLast ? " " : ","), 2);
								}
								sb.AppendLine (")", 1);
							}

							using (sb.UseCurlyBraces ())
							{
								bool UseTransaction = pi.Value.ResultSets.Count > 0;

								using (UseTransaction
									       ? sb.UseCurlyBraces ("using (var Tran = Conn.BeginTransaction ())")
									       : null)
								{
									using (sb.UseCurlyBraces ("using (var Cmd = Conn.CreateCommand ())"))
									{
										string Params = string.Join (", ", p.Arguments.Select (a => a.CallParamName));
										string Call =
											"call \"".ToDoubleQuotes () + " + SchemaName + " +
											$"\".{pi.Value.NativeName.ToDoubleQuotes ()} ({Params});".ToDoubleQuotes ();
										sb.AppendLine ($"Cmd.CommandText = {Call};");

										foreach (var a in p.Arguments)
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

										foreach (var oa in p.Arguments.Where (a => !a.IsCursor && a.IsOut).Indexed ())
										{
											if (oa.IsFirst)
											{
												sb.AppendLine ();
											}

											sb.AppendLine ($"{oa.Value.CsName} = Cmd.Parameters[{oa.Value.CallParamName}].Value as {oa.Value.ClrType};");
										}

										if (p.HasResults)
										{
											sb.AppendLine ()
												.AppendLine (
													$"{p.ResultClassName} Result = {(p.IsSingleSet ? "null" : "new " + p.ResultClassName + " ()")};"
												);
										}

										foreach (var Set in p.ResultSets)
										{
											sb.AppendLine ();
											using (sb.UseCurlyBraces ($"using (var ResCmd = Conn.CreateCommand ())"))
											{
												sb.AppendLine (
														$"ResCmd.CommandText = {$"FETCH ALL IN {Set.CursorName.ToDoubleQuotes ()};".ToDoubleQuotes ()};")
													.AppendLine (
														$"{Set.SetCsTypeName} Set = {(Set.IsSingleRow ? $"null" : $"new {Set.SetCsTypeName} ()")};")
													.AppendLine ()
													;

												using (sb.UseCurlyBraces ("using (var Rdr = ResCmd.ExecuteReader ())"))
												{
													using (sb.UseCurlyBraces ((Set.IsSingleRow ? "if" : "while") +
													                          " (Rdr.Read ())"))
													{
														sb.TypeIndent ();
														if (Set.IsSingleRow)
														{
															sb.TypeText ("Set = ");
														}
														else
														{
															sb.TypeText ("Set.Add (");
														}

														if (Set.IsSingleColumn)
														{
															sb.TypeText (Set.Properties[0].ReaderExpression ("Rdr"));
														}
														else
														{
															sb.AppendLine ($"new {Set.RowCsClassName}")
																.AppendLine ("{");

															using (sb.UseBlock ())
															{
																foreach (var c in Set.Properties.Indexed ())
																{
																	sb.AppendLine (
																		$"{c.Value.CsName} = {c.Value.ReaderExpression ("Rdr")}{(c.IsLast ? "" : ",")}");
																}
															}

															sb.TypeIndent ()
																.TypeText ("}");
														}

														if (Set.IsSingleRow)
														{
															sb.AppendLine (";");
														}
														else
														{
															sb.AppendLine (");");
														}
													}
												}

												sb.AppendLine ();
												if (p.IsSingleSet)
												{
													sb.AppendLine ($"Result = Set;");
												}
												else
												{
													sb.AppendLine ($"Result.{Set.PropertyName} = Set;");
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

								if (p.HasResults)
								{
									sb.AppendLine ()
										.AppendLine ($"return Result;");
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
