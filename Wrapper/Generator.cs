using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Utils;
using DbAnalysis;
using DbAnalysis.Datasets;
using Utils.CodeGeneration;

namespace Wrapper
{
	static class ProcessorUtils
	{
		public static GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>[] Act<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> (this GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>[] Processors, Action<GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>> Action)
			where TColumn : Column, new()
			where TArgument : Argument, new()
			where TResultSet : GResultSet<TColumn>, new()
			where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
			where TSqlType : GSqlType<TColumn>, new()
			where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
		{
			foreach (var p in Processors)
			{
				Action (p);
			}

			return Processors;
		}
	}

	public class Generator
	{
		public static string GenerateCode (Module Module, params CodeProcessor[] Processors)
		{
			return GGenerateCode (Module, Processors);
		}

		public static string GGenerateCode<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> (TModule Module, params GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>[] Processors)
			where TColumn : Column, new()
			where TArgument : Argument, new()
			where TResultSet : GResultSet<TColumn>, new()
			where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
			where TSqlType : GSqlType<TColumn>, new()
			where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
		{
			//
			SqlTypeMap DbTypeMap = SqlTypeMap.FromTypes (Module.Types);

			// origins
			foreach (var a in Module.Procedures.SelectMany (p => p.Arguments))
			{
				a.PSqlType = DbTypeMap.GetTypeForName (a.Type);
			}

			foreach (var c in Module.Procedures.SelectMany (p => p.ResultSets).SelectMany (rs => rs.Columns))
			{
				c.PSqlType = DbTypeMap.GetTypeForName (c.Type);
			}

			//
			Processors.Act (p => p.OnHaveModule (Module));

			// build type map
			// Postgres type name to C# type name
			// including arrays;
			// synonyms must go to the same entries
			Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap = new Dictionary<string, TypeMapping<TSqlType, TColumn>> ();
			foreach (var p in DbTypeMap.Map.Where (p => !p.Value.IsArray))
			{
				if (ClrType.Map.TryGetValue (p.Value.ClrType, out var ct))
				{
					TypeMap.Add (p.Key, ct.CsNullableName, p.Value);

					if (p.Value.ShortName != null)
					{
						TypeMap.AddSynonym (p.Key, p.Value.ShortName);
					}
				}
			}
			TypeMap.Add ("bytea", "byte[]", DbTypeMap.GetTypeForName ("bytea"));
			TypeMap.AddSynonym ("bytea", "pg_catalog.bytea");

			foreach (var t in Module.Types.Where (ct => ct.Properties != null
			         || ct.Enum != null && ct.GenerateEnum))
			{
				string PsqlKey = t.Schema + "." + t.Name;
				// must match names filled in Wrapper below
				string ClrKey = t.Schema.ValidCsName () + "." + t.Name.ValidCsName ();

				TypeMap.Add (PsqlKey, ClrKey, DbTypeMap.GetTypeForName (PsqlKey));
				TypeMap[PsqlKey].ReportedType = t;
			}

			// pre-matched types
			foreach (var t in TypeMap)
			{
				if (t.Value.ReportedType?.MapTo != null)
				{
					t.Value.CsTypeName = () => t.Value.ReportedType.MapTo;
				}
			}

			//
			Processors.Act (p => p.OnHaveTypeMap (DbTypeMap, TypeMap));

			//
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database = new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>
			{
				Origin = Module,
				TitleComment = CodeGenerationUtils.AutomaticWarning,
				Usings = new List<string>
				{
					"using System;",
					"using System.Data;",
					"using System.Threading.Tasks;",
					"using System.Collections.Generic;",
					"using Npgsql;",
					"using NpgsqlTypes;"
				},
				CsNamespace = "Generated",
				CsClassName = "DbProc",
				TypeMap = TypeMap,
				Schemata = Module.Procedures
					.Select (p => p.Schema)
					.Concat (Module.Types.Select (t => t.Schema))
					.Distinct ()
					.OrderBy (ns => ns)
					.Select (s =>
					{
						return new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema
						{
							NativeName = s,
							CsClassName = s.ValidCsName (),
							NameHolderVar = "Name_" + s.ValidCsNamePart (),
							EnumTypes = Module.Types
								.Where (t => t.Schema == s && t.Enum != null && t.Enum.Length > 0)
								.Select (t => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.CustomType
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
								.Select (t => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.CustomType
								{
									Origin = t,
									NativeName = t.Name,
									RowCsClassName = t.Name.ValidCsName (),
									Properties = t.Properties
										.Select (p => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Set<TSqlType, TColumn>.Property
										{
											Origin = p,
											NativeName = p.Name,
											TypeMapping = TypeMap[p.Type],
											CsName = p.Name.ValidCsName ()
										})
										.ToList ()
								})
								.ToArray (),
							Procedures = Module.Procedures
								.Where (p => p.Schema == s)
								.OrderBy (p => p.Name)
								.Select (p => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure
								{
									Origin = p,
									NativeName = p.Name,
									CsName = p.Name.ValidCsName (),
									Arguments = p.Arguments
										.Select (a => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Argument
										{
											Origin = a,
											NativeName = a.Name,
											CallParamName = "@" + a.Name,
											CsName = a.Name.ValidCsName (),
											TypeMapping = TypeMap[a.Type],
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
											var Set = new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set
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
													.Select (c => new Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set.Property
														{
															Origin = c,
															NativeName = c.Name,
															CsName = c.Name.ValidCsName (),
															TypeMapping = TypeMap[c.Type]
														})
													.ToList ()
											};

											if (Set.IsSingleColumn)
											{
												Set.RowCsClassName = Set.Properties[0].TypeMapping.CsTypeName ();
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

			Processors.Act (p => p.OnHaveWrapper (Database));

			//
			IndentedTextBuilder sb = new IndentedTextBuilder ();
			List<DbProcProperty> DbProcProperties = new List<DbProcProperty> ();
			Processors.Act (p => p.OnCodeGenerationStarted (Database, sb, DbProcProperties));

			if (!string.IsNullOrWhiteSpace (Database.TitleComment))
			{
				sb.AppendLine (Database.TitleComment);
			}

			foreach (var u in Database.Usings)
			{
				sb.AppendLine (u);
			}

			sb.AppendLine ();

			using (!string.IsNullOrWhiteSpace (Database.CsNamespace)
				       ? sb.UseCurlyBraces ($"namespace {Database.CsNamespace}")
				       : null)
			{
				bool HasCustomMapping = Module.Types.Any (t => t.Enum != null && t.GenerateEnum
				                                               || t.Properties != null);

				using (sb.UseCurlyBraces ($"public class {Database.CsClassName}"))
				{
					sb.AppendLine ("public NpgsqlConnection Conn;");
					foreach (var p in DbProcProperties)
					{
						sb.AppendLine ($"public {p.Type} {p.Name};");
					}

					foreach (var ns in Database.Schemata)
					{
						string ValueHolderName = "m_" + ns.CsClassName;

						sb.AppendLine ();
						sb.AppendLine ($"protected {ns.CsClassName} {ValueHolderName} = null;");
						using (sb.UseCurlyBraces ($"public {ns.CsClassName} {ns.CsClassName}"))
						{
							using (sb.UseCurlyBraces ("get"))
							{
								using (sb.UseCurlyBraces ($"if ({ValueHolderName} == null)"))
								{
									sb.AppendLine ($"{ValueHolderName} = new {ns.CsClassName} (this);");
								}

								sb.AppendLine ($"return {ValueHolderName};");
							}
						}
					}

					sb.AppendLine ();

					// constructor
					using (sb.UseCurlyBraces (
						       $"public {Database.CsClassName} (NpgsqlConnection Conn{string.Join ("", DbProcProperties.Select (p => $", {p.Type} {p.Name}"))})"))
					{
						sb.AppendLine ("this.Conn = Conn;");
						foreach (var p in DbProcProperties)
						{
							sb.AppendLine ($"this.{p.Name} = {p.Name};");
						}

						if (HasCustomMapping)
						{
							sb.AppendLine ("UseCustomMapping (this.Conn);");
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

							foreach (var t in Module.Types.OrderBy (t => t.Schema).ThenBy (t => t.Name))
							{
								string MapMethod = null;
								if (t.Enum != null && t.GenerateEnum)
								{
									MapMethod = "MapEnum";
								}
								else if (t.Properties != null)
								{
									MapMethod = "MapComposite";
								}

								if (MapMethod != null)
								{
									sb.AppendLine (
										$"Conn.TypeMapper.{MapMethod}<{TypeMap[$"{t.Schema}.{t.Name}"].CsTypeName ()}> (\"{t.Schema}.{t.Name}\");");
								}
							}
						}
					}

					//
					Processors.Act (p => p.OnCodeGeneratingDbProc (Database, sb));
				}

				foreach (var ns in Database.Schemata)
				{
					sb.AppendLine ();

					using (sb.UseCurlyBraces ($"public class {ns.CsClassName}"))
					{
						// enum types
						foreach (var e in ns.EnumTypes.Where (e => e.Origin.MapTo == null))
						{
							if (e.GenerateEnum)
							{
								using (sb.UseCurlyBraces ($"public enum {e.RowCsClassName}"))
								{
									foreach (var v in e.EnumValues.Indexed ())
									{
										sb.AppendLine ($"{v.Value.ValidCsName ()}{(v.IsLast ? "" : ",")}");
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
						foreach (var ct in ns.CompositeTypes.Where (ct => ct.Origin.MapTo == null))
						{
							using (sb.UseCurlyBraces ($"public class {ct.RowCsClassName}"))
							{
								foreach (var p in ct.Properties)
								{
									sb.AppendLine ($"public {p.TypeMapping.CsTypeName ()} {p.CsName};");
								}
							}

							sb.AppendLine ();
						}

						// properties
						sb.AppendLine ("public DbProc DbProc;")
							.AppendLine ("public NpgsqlConnection Conn => DbProc.Conn;")
							.AppendLine ();

						using (sb.UseCurlyBraces ($"public {ns.CsClassName} (DbProc DbProc)"))
						{
							sb.AppendLine ("this.DbProc = DbProc;");
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
									.Select (a =>
									{
										string ArgumentType = a.TypeMapping.CsTypeName ();
										Processors.Act (p =>
											p.OnEncodingParameter (Database, ns, pi.Value, a, ref ArgumentType));

										return ArgumentType == null
											? null
											: (a.IsOut ? "ref " : "") +
											  $"{ArgumentType} {a.CsName}";
									})
									.Where (a => a != null)
									.ToArray ()
								;
							var OutArguments = p.Arguments.Where (a => !a.IsCursor && a.IsOut).ToArray ();

							foreach (var Set in p.ResultSets.Where (s => !s.IsSingleColumn))
							{
								using (sb.UseCurlyBraces ($"public class {Set.RowCsClassName}"))
								{
									foreach (var P in Set.Properties)
									{
										string ColumnCsType = P.TypeMapping.CsTypeName ();
										Processors.Act (p =>
											p.OnEncodingResultSetColumn (Database, ns, pi.Value, Set, P,
												ref ColumnCsType));

										if (ColumnCsType != null)
										{
											sb.AppendLine ($"public {ColumnCsType} {P.CsName};");
										}
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

							// method in two modes, sync and async
							foreach (bool IsAsync in new[] { false, true })
							{
								if (IsAsync && OutArguments.Length > 0)
								{
									continue;
								}

								string Await = IsAsync ? "await " : "";
								string Async = IsAsync ? "Async" : "";

								if (IsAsync)
								{
									sb.AppendLine ();
								}

								// type comments
								foreach (var e in p.Arguments.Where (
									         a => a.Origin.PSqlType?.BaseType.EnumValues != null))
								{
									sb.AppendLine (
										$"/// <param name=\"{e.CsName}\">Value from {e.Origin.PSqlType.BaseType.Display}</param>");
								}

								string ReturnType = IsAsync
									? "async " + (p.ResultClassName == "void" ? "Task" : "Task<" + p.ResultClassName + ">")
									: p.ResultClassName;

								string MethodDeclPrefix =
									$"public {ReturnType} {pi.Value.CsName}{Async} (";
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
										sb.AppendLine (a.Value + (a.IsLast ? "" : ","), 2);
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
										       ? sb.UseCurlyBraces ($"using (var Tran = {Await}Conn.BeginTransaction{Async} ())")
										       : null)
									{
										using (sb.UseCurlyBraces ("using (var Cmd = Conn.CreateCommand ())"))
										{
											string Params = string.Join (", ", p.Arguments.Select (a => a.CallParamName
												+ (a.Origin.PSqlType.BaseType.EnumValues == null
													? ""
													: (
														$"::{a.Origin.PSqlType.BaseType.Schema.ToDoubleQuotes ()}.{a.Origin.PSqlType.BaseType.OwnName.ToDoubleQuotes ()}"
														+ (a.Origin.PSqlType.IsArray ? "[]" : "")))
												+ (a.Origin.PSqlType.ShortName == "jsonb" ||
												   a.Origin.PSqlType?.ShortName == "json"
													? $"::{a.Origin.PSqlType?.ShortName}"
													: "")
											));
											string Call =
												$"call {ns.NativeName.ToDoubleQuotes ()}.{pi.Value.NativeName.ToDoubleQuotes ()} ({Params});"
													.ToDoubleQuotes ();
											sb.AppendLine ($"Cmd.CommandText = {Call};");

											foreach (var a in p.Arguments)
											{
												if (a.IsCursor)
												{
													if (a.IsOut)
													{
														sb.AppendLine (
															$"Cmd.Parameters.Add (new NpgsqlParameter ({a.CallParamName.ToDoubleQuotes ()}, NpgsqlDbType.Refcursor) {{ Direction = ParameterDirection.InputOutput, Value = \"{a.CsName}\" }});");
													}
												}
												else
												{
													string Value = a.TypeMapping.SetValue (a.CsName);
													Processors.Act (p =>
														p.OnPassingParameter (Database, ns, pi.Value, a, ref Value));

													if (Value != null)
													{
														sb.AppendLine (
															$"Cmd.Parameters.AddWithValue ({a.CallParamName.ToDoubleQuotes ()}, (object){Value} ?? DBNull.Value)"
															+ (a.IsOut
																? ".Direction = ParameterDirection.InputOutput"
																: "")
															+ ";");
													}
												}
											}

											sb.AppendLine ()
												.AppendLine ($"{Await}Cmd.ExecuteNonQuery{Async} ();");

											// read OUT parameters returned
											foreach (var oa in OutArguments.Indexed ())
											{
												if (oa.IsFirst)
												{
													sb.AppendLine ();
												}

												string ValueRep = oa.Value.TypeMapping.GetValue (
													$"Cmd.Parameters[{oa.Value.CallParamName.ToDoubleQuotes ()}].Value");
												Processors.Act (p =>
													p.OnReadingParameter (Database, ns, pi.Value, oa.Value,
														ref ValueRep));

												sb.AppendLine ($"{oa.Value.CsName} = {ValueRep};");
											}

											// read result sets
											foreach (var Set in p.ResultSets)
											{
												sb.AppendLine ();
												using (sb.UseCurlyBraces (
													       $"using (var ResCmd = Conn.CreateCommand ())"))
												{
													sb.AppendLine (
															$"ResCmd.CommandText = {$"FETCH ALL IN {Set.CursorName.ToDoubleQuotes ()};".ToDoubleQuotes ()};")
														.AppendLine (
															$"{Set.SetCsTypeName} Set = {(Set.IsSingleRow ? $"null" : $"new {Set.SetCsTypeName} ()")};")
														.AppendLine ()
														;

													using (sb.UseCurlyBraces (
														       $"using (var Rdr = {Await}ResCmd.ExecuteReader{Async} ())"))
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

															var ReadProps = Set.Properties
																	.Select (prop =>
																	{
																		string Value = prop.GetReaderExpression ("Rdr");
																		Processors.Act (p =>
																			p.OnReadingResultSetColumn (Database, ns,
																				pi.Value, Set, prop, ref Value));

																		return Value == null
																				? null
																				: new { Property = prop, Value }
																			;
																	})
																	.Where (p => p != null)
																	.ToArray ()
																;

															if (Set.IsSingleColumn)
															{
																sb.TypeText (ReadProps[0].Value);
															}
															else
															{
																sb.AppendLine ($"new {Set.RowCsClassName}")
																	.AppendLine ("{");

																using (sb.UseBlock ())
																{
																	foreach (var c in ReadProps.Indexed ())
																	{
																		sb.AppendLine (
																			$"{c.Value.Property.CsName} = {c.Value.Value}{(c.IsLast ? "" : ",")}");
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
													.AppendLine ($"{Await}Tran.Commit{Async} ();");
											}
										}
									}

									if (p.HasResults)
									{
										sb.AppendLine ()
											.AppendLine ($"return Result;");
									}
								}
							}

							sb.AppendLine ("#endregion");
						}
					}
				}
			}

			return sb.ToString ();
		}
	}
}
