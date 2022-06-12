using System;
using System.Collections.Generic;

using DbAnalysis.Datasets;

namespace Wrapper
{
	public class Database
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
					public DbAnalysis.Datasets.Argument Origin;
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

				public DbAnalysis.Datasets.Procedure Origin;
				public string NativeName;
				public string CsName;
				public Database.Schema.Procedure.Argument[] Arguments;
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
}
