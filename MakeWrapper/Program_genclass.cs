using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Utils;
using ParseProcs;
using ParseProcs.Datasets;
using Utils.CodeGeneration;

namespace MakeWrapper
{
	static class ProcessorUtils
	{
		public static CodeProcessor[] Act (this CodeProcessor[] Processors, Action<CodeProcessor> Action)
		{
			foreach (var p in Processors)
			{
				Action (p);
			}

			return Processors;
		}
	}

	partial class Program
	{
		static string GenerateCode (Module Module, SqlTypeMap DbTypeMap, bool UseSchemaSettings, params CodeProcessor[] Processors)
		{
			Processors.Act (p => p.OnHaveModule (Module));

			// build type map
			// Postgres type name to C# type name
			// including arrays
			Dictionary<string, string> TypeMap = new Dictionary<string, string> ();
			foreach (var p in DbTypeMap.Map.Where (p => !p.Value.IsArray))
			{
				if (ClrType.Map.TryGetValue (p.Value.ClrType, out var ct))
				{
					TypeMap[p.Key] = ct.CsNullableName;
					TypeMap[p.Key + "[]"] = ct.CsName + "[]";

					if (p.Value.ShortName != null)
					{
						TypeMap[p.Value.ShortName] = ct.CsNullableName;
						TypeMap[p.Value.ShortName + "[]"] = ct.CsName + "[]";
					}
				}
			}
			TypeMap["bytea"] = "byte[]";
			TypeMap["pg_catalog.bytea"] = "byte[]";

			foreach (var t in Module.Types.Where (ct => ct.Properties != null
			         || ct.Enum != null && ct.GenerateEnum))
			{
				string PsqlKey = t.Schema + "." + t.Name;
				// must match names filled in Wrapper below
				string ClrKey = t.Schema.ValidCsName () + "." + t.Name.ValidCsName ();

				TypeMap[PsqlKey] = ClrKey;
				TypeMap[PsqlKey + "[]"] = ClrKey + "[]";
			}

			Processors.Act (p => p.OnHaveTypeMap (DbTypeMap, TypeMap));

			//
			Wrapper Wrapper = new Wrapper
			{
				Origin = Module,
				TitleComment = CodeGenerationUtils.AutomaticWarning,
				Usings = new List<string>
				{
					"using System;",
					"using System.Data;",
					"using System.Collections.Generic;",
					"using Npgsql;",
					"using NpgsqlTypes;"
				},
				ClrNamespace = "Generated",
				TypeMap = TypeMap,
				Schemata = Module.Procedures
					.Select (p => p.Schema)
					.Concat (Module.Types.Select (t => t.Schema))
					.Distinct ()
					.OrderBy (ns => ns)
					.Select (s =>
					{
						return new Wrapper.Schema
						{
							NativeName = s,
							CsClassName = s.ValidCsName (),
							NameHolderVar = "Name_" + s.ValidCsNamePart (),
							EnumTypes = Module.Types
								.Where (t => t.Schema == s && t.Enum != null && t.Enum.Length > 0)
								.Select (t => new Wrapper.Schema.CustomType
								{
									Origin = t,
									NativeName = t.Name,
									RowCsClassName = t.Name.ValidCsName (),
									EnumValues = t.Enum,
									GenerateEnum = t.GenerateEnum
								})
								.ToArray (),
							CompositeTypes = Module.Types
								.Where (t => t.Schema == s && t.Properties != null && t.Properties.Length > 0)
								.Select (t => new Wrapper.Schema.CustomType
								{
									Origin = t,
									NativeName = t.Name,
									RowCsClassName = t.Name.ValidCsName (),
									Properties = t.Properties
										.Select (p => new Wrapper.Schema.Set<SqlType, Column>.Property
										{
											Origin = p,
											NativeName = p.Name,
											ClrType = TypeMap.TryGetValue (p.Type,
												out var t)
												? t
												: null,
											CsName = p.Name.ValidCsName ()
										})
										.ToList ()
								})
								.ToArray (),
							Procedures = Module.Procedures
								.Where (p => p.Schema == s)
								.OrderBy (p => p.Name)
								.Select (p => new Wrapper.Schema.Procedure
								{
									Origin = p,
									NativeName = p.Name,
									CsName = p.Name.ValidCsName (),
									Arguments = p.Arguments
										.Select (a => new Wrapper.Schema.Procedure.Argument
										{
											Origin = a,
											NativeName = a.Name,
											CallParamName = "@" + a.Name,
											CsName = a.Name.ValidCsName (),
											ClrType = TypeMap.TryGetValue (a.Type, out var t)
												? t
												: null,
											IsOut = a.IsOut,
											IsCursor = a.Type == "refcursor"
											           || a.Type ==
											           "pg_catalog.refcursor" // here: find more elegant way
										})
										.ToArray (),
									ResultClassName = p.Name.ValidCsNamePart () + "_Result",
									ResultSets = p.ResultSets
										.Select (s =>
										{
											var Set = new Wrapper.Schema.Procedure.Set
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
														var Property = new Wrapper.Schema.Procedure.Set.Property
														{
															Origin = c,
															NativeName = c.Name,
															ClrType = TypeMap.TryGetValue (c.Type,
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
			};

			Processors.Act (p => p.OnHaveWrapper (Wrapper));

			//
			IndentedTextBuilder sb = new IndentedTextBuilder ();

			if (!string.IsNullOrWhiteSpace (Wrapper.TitleComment))
			{
				sb.AppendLine (CodeGenerationUtils.AutomaticWarning);
			}

			foreach (var u in Wrapper.Usings)
			{
				sb.AppendLine (u);
			}

			sb.AppendLine ();

			using (!string.IsNullOrWhiteSpace (Wrapper.ClrNamespace)
				       ? sb.UseCurlyBraces ($"namespace {Wrapper.ClrNamespace}")
				       : null)
			{
				bool HasCustomMapping = Module.Types.Any (t => t.Enum != null && t.GenerateEnum
					|| t.Properties != null);

				using (sb.UseCurlyBraces ("public class DbProc"))
				{
					sb.AppendLine ("public NpgsqlConnection Conn;");

					foreach (var ns in Wrapper.Schemata)
					{
						string ValueHolderName = "m_" + ns.CsClassName;

						sb.AppendLine ();
						sb.AppendLine ($"protected {ns.CsClassName} {ValueHolderName} = null;");
						sb.AppendLine ($"protected string {ns.NameHolderVar} = null;");
						using (sb.UseCurlyBraces ($"public {ns.CsClassName} {ns.CsClassName}"))
						{
							using (sb.UseCurlyBraces ("get"))
							{
								using (sb.UseCurlyBraces ($"if ({ValueHolderName} == null)"))
								{
									sb.AppendLine ($"{ValueHolderName} = new {ns.CsClassName} (Conn, {ns.NameHolderVar});");
								}

								sb.AppendLine ($"return {ValueHolderName};");
							}
						}
					}

					sb.AppendLine ();

					// constructor
					using (sb.UseCurlyBraces ($"public DbProc (NpgsqlConnection Conn{(UseSchemaSettings ? string.Join ("", Wrapper.Schemata.Select (s => ", string " + s.NameHolderVar)) : "")})"))
					{
						sb.AppendLine ("this.Conn = Conn;");

						if (HasCustomMapping)
						{
							sb.AppendLine ("UseCustomMapping (this.Conn);");
						}

						foreach (var ns in Wrapper.Schemata)
						{
							sb.AppendLine ($"this.{ns.NameHolderVar} = {(UseSchemaSettings ? ns.NameHolderVar : ns.NativeName.ToDoubleQuotes ())};");
						}
					}

					if (HasCustomMapping)
					{
						sb.AppendLine ();

						using (sb.UseCurlyBraces ($"public static void UseCustomMapping (NpgsqlConnection Conn)"))
						{
							// check connection state
							using (sb.UseCurlyBraces (
								       "if (Conn.State == ConnectionState.Closed || Conn.State == ConnectionState.Broken || Conn.State == ConnectionState.Connecting)"))
							{
								sb.AppendLine ("return;");
							}

							sb.AppendLine ();

							foreach (var s in Wrapper.Schemata)
							{
								foreach (var t in s.EnumTypes.Where (et => et.Origin.GenerateEnum))
								{
									sb.AppendLine ($"Conn.TypeMapper.MapEnum<{s.CsClassName}.{t.RowCsClassName}> (\"{s.NativeName}.{t.NativeName}\");");
								}

								foreach (var t in s.CompositeTypes.Where (ct => ct.Properties != null))
								{
									sb.AppendLine ($"Conn.TypeMapper.MapComposite<{s.CsClassName}.{t.RowCsClassName}> (\"{s.NativeName}.{t.NativeName}\");");
								}
							}
						}
					}
				}

				foreach (var ns in Wrapper.Schemata)
				{
					sb.AppendLine ();

					using (sb.UseCurlyBraces ($"public class {ns.CsClassName}"))
					{
						// enum types
						foreach (var e in ns.EnumTypes)
						{
							if (e.GenerateEnum)
							{
								using (sb.UseCurlyBraces ($"public enum {e.RowCsClassName}"))
								{
									foreach (var v in e.EnumValues.Indexed ())
									{
										sb.AppendLine ($"{v.Value}{(v.IsLast ? "" : ",")}");
									}
								}
							}
							else
							{
								using (sb.UseCurlyBraces ($"public static class {e.RowCsClassName}"))
								{
									foreach (var v in e.EnumValues)
									{
										sb.AppendLine (
											$"public const string {v.ValidCsName ()} = {v.ToDoubleQuotes ()};");
									}
								}
							}

							sb.AppendLine ();
						}

						// composite types
						foreach (var ct in ns.CompositeTypes)
						{
							using (sb.UseCurlyBraces ($"public class {ct.RowCsClassName}"))
							{
								foreach (var p in ct.Properties)
								{
									sb.AppendLine ($"public {p.ClrType} {p.CsName};");
								}
							}

							sb.AppendLine ();
						}

						// properties
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
										sb.AppendLine ($"public {Set.SetCsTypeName} {Set.PropertyName};");
									}
								}

								sb.AppendLine ();
							}

							// type comments
							foreach (var e in p.Arguments.Where (a => a.Origin.PSqlType?.BaseType.EnumValues != null))
							{
								sb.AppendLine ($"/// <param name=\"{e.CsName}\">Value from {e.Origin.PSqlType.BaseType.Display}</param>");
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

								if (p.HasResults)
								{
									sb.AppendLine (
											$"{p.ResultClassName} Result = {(p.IsSingleSet ? "null" : "new " + p.ResultClassName + " ()")};"
										)
										.AppendLine ();
								}

								using (UseTransaction
									       ? sb.UseCurlyBraces ("using (var Tran = Conn.BeginTransaction ())")
									       : null)
								{
									using (sb.UseCurlyBraces ("using (var Cmd = Conn.CreateCommand ())"))
									{
										string Params = string.Join (", ", p.Arguments.Select (a => a.CallParamName
												// here: schema hard-coded, not using SchemaName
											+ (a.Origin.PSqlType?.BaseType.EnumValues == null ? "" : ($"::{a.Origin.PSqlType.BaseType.Schema.ToDoubleQuotes ()}.{a.Origin.PSqlType.BaseType.OwnName.ToDoubleQuotes ()}"
											+ (a.Origin.PSqlType.IsArray ? "[]" : "")))
										));
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
													sb.AppendLine ($"Cmd.Parameters.Add (new NpgsqlParameter ({a.CallParamName.ToDoubleQuotes ()}, NpgsqlDbType.Refcursor) {{ Direction = ParameterDirection.InputOutput, Value = \"{a.CsName}\" }});");
												}
											}
											else
											{
												sb.AppendLine ($"Cmd.Parameters.AddWithValue ({a.CallParamName.ToDoubleQuotes ()}, (object){a.CsName} ?? DBNull.Value)"
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

											sb.AppendLine ($"{oa.Value.CsName} = Cmd.Parameters[{oa.Value.CallParamName.ToDoubleQuotes ()}].Value as {oa.Value.ClrType};");
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

			return sb.ToString ();
		}
	}
}
