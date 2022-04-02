using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

using ParseProcs;
using ParseProcs.Datasets;
using Utils.CodeGeneration;

namespace MakeWrapper
{
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
			sb.AppendLine (CodeGenerationUtils.AutomaticWarning);

			using (sb.AppendLine ("namespace Gen").UseCurlyBraces ())
			{
				foreach (var ns in Module.Procedures.GroupBy (p => p.Schema).OrderBy (g => g.Key))
				{
					sb.AppendLine ();
					using (sb.AppendLine ("namespace " + ns.Key).UseCurlyBraces ())
					{
						foreach (var p in ns.OrderBy (p => p.Name))
						{
							sb.AppendLine ("#region " + p.Name);

							string[] Args = p.Arguments
									.Where (a => a.SqlType.SqlBaseType != "refcursor")
									.Select (a => $"{CastToDict[a.SqlType.ToString ()]} {a.Name}")
									.ToArray ()
								;

							string MethodDeclPrefix = "public void " + p.Name + " (";
							if (Args.Length <= 2)
							{
								string ArgsDef = string.Join (", ", Args);
								sb.AppendLine (MethodDeclPrefix + ArgsDef + ")");
							}
							else
							{
								sb.AppendLine (MethodDeclPrefix);
								bool First = true;
								foreach (string a in Args)
								{
									sb.TypeIndent (2)
										.TypeText ((First ? "  " : ", ") + a)
										.AppendLine ()
										;
									First = false;
								}
								sb.AppendLine (")", 1);
							}

							using (sb.UseCurlyBraces ())
							{
								foreach (var Set in p.ResultSets)
								{
									using (sb.AppendLine ("// " + Set.Name).UseCurlyBraces ())
									{
										foreach (var c in Set.Columns)
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
