﻿using System;
using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;

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
		public string NameHolderVar;
		public Procedure[] Procedures;
	}

	partial class Program
	{
		static void Main (string[] args)
		{
			string ModuleInputPath = Path.GetFullPath (args[0]);
			string ModuleJson = File.ReadAllText (ModuleInputPath);
			Module Module = JsonConvert.DeserializeObject<Module> (ModuleJson);

			//
			foreach (var run in new[]
			         {
				         new { UseNodaTime = false, UseSchemaSettings = false, target = "dbproc.cs" },
				         new { UseNodaTime = false, UseSchemaSettings = true, target = "dbproc_sch.cs" },
				         new { UseNodaTime = true, UseSchemaSettings = true, target = "dbproc_sch_noda.cs" }
			         }
			        )
			{
				string Code = GenerateCode (Module, run.UseNodaTime, run.UseSchemaSettings);
				CodeGenerationUtils.EnsureFileContents (run.target, Code);
			}
		}
	}
}
