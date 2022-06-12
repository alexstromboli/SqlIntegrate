using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace DbAnalysis.Datasets
{
	public class SqlType
	{
		public string Schema;
		public string Name;

		[JsonIgnore]
		public PSqlType Origin;

		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string[] Enum;
		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool GenerateEnum = false;

		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Column[] Properties;

		public SqlType ()
		{
			Name = null;
		}

		public SqlType (PSqlType Origin)
		{
			this.Origin = Origin;
			Schema = Origin.Schema;
			Name = Origin.OwnName;
			Enum = Origin.EnumValues;

			if (Origin.Properties != null && Origin.Properties.Length > 0)
			{
				Properties = Origin.Properties.Select (p => new Column
						{
							Name = p.Name,
							Type = p.Type.ToString (),
							PSqlType = p.Type
						})
						.ToArray ()
					;
			}
		}

		public override string ToString ()
		{
			return Name;
		}
	}

	public class Column
	{
		public string Name;
		public string Type;

		[JsonIgnore]
		public PSqlType PSqlType;

		public override string ToString ()
		{
			return $"{Name} {Type}";
		}
	}

	public class ResultSet
	{
		public string Name;
		public List<string> Comments;
		public List<Column> Columns;

		public override string ToString ()
		{
			return Name;
		}
	}

	public class Argument
	{
		public string Name;
		public string Type;

		[JsonIgnore]
		public PSqlType PSqlType;

		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool IsOut = false;

		public override string ToString ()
		{
			return (IsOut ? "INOUT " : "") + Name + " " + Type;
		}
	}

	public class Procedure
	{
		public string Schema;
		public string Name;
		public List<Argument> Arguments;
		public List<ResultSet> ResultSets;

		public override string ToString ()
		{
			return $"{Schema}.{Name}";
		}
	}

	public class Module
	{
		public List<SqlType> Types;
		public List<Procedure> Procedures;
	}
}
