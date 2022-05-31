﻿using System.Collections.Generic;

using Newtonsoft.Json;

namespace ParseProcs.Datasets
{
	public class SqlType
	{
		public string Name;

		[JsonIgnore]
		public PSqlType Origin;

		[JsonProperty (DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string[] Enum;

		public SqlType ()
		{
			Name = null;
		}

		public SqlType (PSqlType Origin)
		{
			this.Origin = Origin;
			Name = Origin.ShortName ?? Origin.Display;
			Enum = Origin.EnumValues;
		}

		public override string ToString ()
		{
			return Name;
		}
	}

	public class Column
	{
		public string Name;

		[JsonIgnore]
		public SqlType SqlType;
		[JsonIgnore]
		public string _Type;
		public string Type
		{
			get
			{
				return SqlType?.Name ?? _Type ?? "unknown";
			}

			set
			{
				_Type = value;
				SqlType = null;
			}
		}

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

		[JsonIgnore]
		public SqlType SqlType;
		[JsonIgnore]
		public string _Type;
		public string Type
		{
			get
			{
				return SqlType?.Name ?? _Type ?? "unknown";
			}

			set
			{
				_Type = value;
				SqlType = null;
			}
		}

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
		public List<SqlType> Types;
		public List<Procedure> Procedures;
	}
}
