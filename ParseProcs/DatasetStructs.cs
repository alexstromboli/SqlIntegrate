using System.Collections.Generic;

using Newtonsoft.Json;

namespace ParseProcs.Datasets
{
	public class Column
	{
		public string Name;

		public string SqlBaseType;
		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool IsArray = false;

		public override string ToString ()
		{
			return $"{Name} {SqlBaseType}";
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

	public class Procedure
	{
		public string Schema;
		public string Name;
		public List<ResultSet> ResultSets;

		public override string ToString ()
		{
			return $"{Schema}.{Name}";
		}
	}

	public class Module
	{
		public List<Procedure> Procedures;
	}
}
