﻿using System.Collections.Generic;

using Newtonsoft.Json;

namespace ParseProcs.Datasets
{
	public class SqlType
	{
		public string SqlBaseType;
		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool IsArray = false;

		public SqlType ()
		{
			SqlBaseType = null;
			IsArray = false;
		}

		public SqlType (PSqlType Type)
		{
			SqlBaseType = Type.BaseType.Display;
			IsArray = Type.IsArray;
		}

		public override string ToString ()
		{
			return SqlBaseType + (IsArray ? "[]" : "");
		}
	}

	public class Column
	{
		public string Name;
		public SqlType SqlType;

		public override string ToString ()
		{
			return $"{Name} {SqlType}";
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
		public SqlType SqlType;
		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool IsOut = false;

		public override string ToString ()
		{
			return (IsOut ? "INOUT " : "") + Name + " " + SqlType;
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
		public List<Procedure> Procedures;
	}
}