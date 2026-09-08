using System;
using System.Collections.Generic;

using Utils;
using DbAnalysis;
using DbAnalysis.Datasets;

namespace Wrapper
{
	// A type the report names that the type map does not cover. Thrown instead of the
	// dictionary's own KeyNotFoundException, which carries the type name and nothing
	// else: the type alone does not say which procedure, argument or column named it,
	// and that is the half a reader can act on. A pseudo-type reaches here the same way
	// a genuinely unmapped one does, and neither can be described in C#.
	public class UnmappedTypeException : Exception
	{
		public string SqlTypeName { get; }
		public string Site { get; }

		public UnmappedTypeException (string SqlTypeName, string Site)
			: base ($"{Site} has type \"{SqlTypeName}\", which no C# type is mapped to")
		{
			this.SqlTypeName = SqlTypeName;
			this.Site = Site;
		}
	}

	public static class TypeMappingUtils
	{
		// The one way a reported type name is turned into a mapping. Every lookup goes
		// through it so that a missing mapping names its own origin, whatever the shape
		// that named it.
		public static TypeMapping<TSqlType, TColumn> Mapping<TSqlType, TColumn> (this Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap,
				string SqlTypeName,
				string Site
				)
			where TColumn : Column, new()
			where TSqlType : GSqlType<TColumn>, new()
		{
			if (!TypeMap.TryGetValue (SqlTypeName, out var Result))
			{
				throw new UnmappedTypeException (SqlTypeName, Site);
			}

			return Result;
		}

		public static Dictionary<string, TypeMapping<TSqlType, TColumn>> AddSynonym<TSqlType, TColumn> (this Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap,
				string SourceName,
				string Synonym
				)
			where TColumn : Column, new()
			where TSqlType : GSqlType<TColumn>, new()
		{
			TypeMap[Synonym] = TypeMap[SourceName];

			if (TypeMap[Synonym].PSqlType.ArrayType != null)
			{
				TypeMap[Synonym + "[]"] = TypeMap[SourceName + "[]"];
			}

			return TypeMap;
		}

		public static Dictionary<string, TypeMapping<TSqlType, TColumn>> Add<TSqlType, TColumn> (this Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap,
				string SqlTypeName,
				string CsNullableName,	// can have '?' in the end
				PSqlType PSqlType
				)
			where TColumn : Column, new()
			where TSqlType : GSqlType<TColumn>, new()
		{
			var Single = new TypeMapping<TSqlType, TColumn>
			{
				SqlTypeName = SqlTypeName,
				CsTypeName = fnu => CsNullableName,
				PSqlType = PSqlType
			};
			Single.GetValue = v => $"{v} as {Single.CsTypeName (true)}";		// use closure
			TypeMap[SqlTypeName] = Single;

			if (PSqlType.ArrayType != null)
			{
				// refer to 'single' type
				string ArrKey = SqlTypeName + "[]";
				var Array = new TypeMapping<TSqlType, TColumn>
				{
					SqlTypeName = ArrKey,
					CsTypeName = fnu => Single.CsTypeName (false).TrimEnd ('?') + "[]",
					PSqlType = PSqlType.ArrayType
				};
				Array.GetValue = v => $"{v} as {Single.CsTypeName (false).TrimEnd ('?')}[]";
				TypeMap[ArrKey] = Array;
			}

			return TypeMap;
		}

		public static string GetReaderExpression<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule, A, B> (this Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Set<A, B>.Property Property, string rdr)
			where TColumn : Column, new()
			where TArgument : Argument, new()
			where TResultSet : GResultSet<TColumn>, new()
			where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
			where TSqlType : GSqlType<TColumn>, new()
			where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
		{
			return Property.TypeMapping.GetValue ($"{rdr}[{Property.NativeName.ToDoubleQuotes ()}]");
		}
	}

	public class TypeMapping<TSqlType, TColumn>
		where TColumn : Column, new()
		where TSqlType : GSqlType<TColumn>, new()
	{
		// here: store PSqlType?
		public string SqlTypeName;
		public Func<bool, string> CsTypeName;		// bool ForceNullability => string display
		public PSqlType PSqlType;
		public TSqlType ReportedType;
		public Func<string, string> SetValue = v => v;
		public Func<string, string> GetValue;
	}

	public class Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>
		where TColumn : Column, new()
		where TArgument : Argument, new()
		where TResultSet : GResultSet<TColumn>, new()
		where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
		where TSqlType : GSqlType<TColumn>, new()
		where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
	{
		public class Schema
		{
			public class Set<TResultSet, TColumnI>
			{
				public class Property
				{
					public TColumnI Origin;
					public string NativeName;
					public string CsName;
					public TypeMapping<TSqlType, TColumn> TypeMapping;

					public override string ToString ()
					{
						return (CsName ?? NativeName) + " " + (TypeMapping?.CsTypeName (true) ?? "???");
					}
				}

				public TResultSet Origin;
				public string RowCsClassName;
				public bool GenerateEnum;
				public List<Property> Properties;
			}

			public class CustomType : Set<TSqlType, TColumn>
			{
				public string NativeName;

				public string[] EnumValues;
			}

			public class Procedure
			{
				public class Argument
				{
					public TArgument Origin;
					public string NativeName;
					public string CsName;
					public string CallParamName;
					public TypeMapping<TSqlType, TColumn> TypeMapping;
					public bool IsCursor;
					public bool IsOut;
				}

				public class Set : Set<TResultSet, TColumn>
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

				public TProcedure Origin;
				public string NativeName;
				public string CsName;
				public Argument[] Arguments;
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

		public TModule Origin;
		public Schema[] Schemata;

		public string TitleComment;
		public List<string> Usings;
		public string CsNamespace;
		public string CsClassName;
		public Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap;
	}
}
