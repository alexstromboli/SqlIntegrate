using System;
using System.Collections.Generic;

using Utils;
using DbAnalysis.Datasets;

namespace Wrapper
{
	public static class TypeMappingUtils
	{
		public static Dictionary<string, TypeMapping> Add (this Dictionary<string, TypeMapping> TypeMap, string SqlTypeName, string CsTypeName, string CsNullableName, bool AddArray = true)
		{
			TypeMap[SqlTypeName] = new TypeMapping
			{
				SqlTypeName = SqlTypeName,
				CsTypeName = CsNullableName,
				ValueConverter = v => $"{v} as {CsNullableName}"
			};

			if (AddArray)
			{
				string ArrKey = SqlTypeName + "[]";
				TypeMap[ArrKey] = new TypeMapping
				{
					SqlTypeName = ArrKey,
					CsTypeName = CsTypeName + "[]",
					ValueConverter = v => $"{v} as {CsTypeName}[]"
				};
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
			return Property.TypeMapping.ValueConverter ($"{rdr}[{Property.NativeName.ToDoubleQuotes ()}]");
		}
	}

	public class TypeMapping
	{
		// here: store PSqlType?
		public string SqlTypeName;
		public string CsTypeName;
		public Func<string, string> ValueConverter;
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
			public class Set<TResultSet, TColumn>
			{
				public class Property
				{
					public TColumn Origin;
					public string NativeName;
					public string CsName;
					public TypeMapping TypeMapping;

					public override string ToString ()
					{
						return (CsName ?? NativeName) + " " + (TypeMapping?.CsTypeName ?? "???");
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
					public TypeMapping TypeMapping;
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
		public Dictionary<string, TypeMapping> TypeMap;
	}
}
