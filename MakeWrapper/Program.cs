using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Newtonsoft.Json;

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
		public string NameHolderVar;
		public Procedure[] Procedures;
	}

	partial class Program
	{
		public static SortedSet<string> CsKeywords = new SortedSet<string> (new[] { "abstract", "event", "new", "struct", "as", "explicit", "null", "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw", "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float", "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected", "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe", "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal", "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate", "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock", "stackalloc", "else", "long", "static", "enum", "namespace", "string" });

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
