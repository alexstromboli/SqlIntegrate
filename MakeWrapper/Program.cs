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

			Add (typeof (object), "object");
			Add (typeof (bool), "bool");
			Add (typeof (int), "int");
			Add (typeof (uint), "uint");
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

	public class Wrapper
	{
		public class Schema
		{
			public class Set<TResultSet, TColumn>
			{
				public class Property
				{
					public TColumn Origin;
					public string NativeName;
					public string CsName;
					public string ClrType;
					public Func<string, string> ReaderExpression;

					public override string ToString ()
					{
						return (CsName ?? NativeName) + " " + ClrType;
					}
				}

				public TResultSet Origin;
				public string RowCsClassName;
				public bool GenerateEnum;
				public List<Property> Properties;
			}

			public class CustomType : Set<SqlType, Column>
			{
				public string NativeName;
				//public string CsName;

				public string[] EnumValues;
			}

			public class Procedure
			{
				public class Argument
				{
					public ParseProcs.Datasets.Argument Origin;
					public string NativeName;
					public string CsName;
					public string CallParamName;
					public string ClrType;
					public bool IsCursor;
					public bool IsOut;
				}

				public class Set : Set<ResultSet, Column>
				{
					public string CursorName;
					public string SetCsTypeName;
					public string PropertyName;
					public bool IsSingleRow;
					public bool IsSingleColumn => Properties.Count == 1;
					public bool IsScalar => IsSingleRow && IsSingleColumn;

					public override string ToString ()
					{
						return RowCsClassName;
					}
				}

				public ParseProcs.Datasets.Procedure Origin;
				public string NativeName;
				public string CsName;
				public Wrapper.Schema.Procedure.Argument[] Arguments;
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
			public CustomType[] EnumTypes;
			public CustomType[] CompositeTypes;
			public Procedure[] Procedures;
		}

		public Module Origin;
		public Schema[] Schemata;

		public string TitleComment;
		public List<string> Usings;
		public string CsNamespace;
		public string CsClassName;
		public Dictionary<string, string> TypeMap;
	}

	class ChangeNameCodeProcessor : CodeProcessor
	{
		public override void OnHaveWrapper (Wrapper Wrapper)
		{
			base.OnHaveWrapper (Wrapper);
			Wrapper.CsNamespace = "FirstSolution";
			Wrapper.CsClassName = "Proxy";
		}
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
				         new { target = "dbproc.cs", processors = new[] { (CodeProcessor)new ChangeNameCodeProcessor () } },
				         new { target = "dbproc_sch_noda.cs", processors = new[] { (CodeProcessor)new NodaTimeCodeProcessor () } }
			         }
			        )
			{
				string Code = GenerateCode (Module, run.processors);
				CodeGenerationUtils.EnsureFileContents (run.target, Code);
			}
		}
	}
}
