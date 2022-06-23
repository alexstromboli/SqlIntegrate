using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace DbAnalysis.Datasets
{
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

	public class GSqlType<TColumn>
		where TColumn : Column, new()
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
		public TColumn[] Properties;

		public GSqlType ()
		{
			Name = null;
		}

		public GSqlType (PSqlType Origin)
		{
			this.Origin = Origin;
			Schema = Origin.Schema;
			Name = Origin.OwnName;
			Enum = Origin.EnumValues;

			if (Origin.Properties != null && Origin.Properties.Length > 0)
			{
				Properties = Origin.Properties.Select (p => new TColumn
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

	public class GResultSet<TColumn>
	{
		public string Name;
		public List<string> Comments;
		public List<TColumn> Columns;

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

	public class GProcedure<TColumn, TArgument, TResultSet>
		where TArgument : Argument, new()
		where TResultSet : GResultSet<TColumn>, new()
	{
		public string Schema;
		public string Name;
		public List<TArgument> Arguments;
		public List<TResultSet> ResultSets;

		public override string ToString ()
		{
			return $"{Schema}.{Name}";
		}
	}

	public class GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
		where TColumn : Column, new()
		where TArgument : Argument, new()
		where TResultSet : GResultSet<TColumn>, new()
		where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
		where TSqlType : GSqlType<TColumn>, new()
	{
		public List<TSqlType> Types;
		public List<TProcedure> Procedures;
	}
	
	// specific classes

	#region specific
	public class SqlType : GSqlType<Column>
	{
		public SqlType ()
		{
			
		}

		public SqlType (PSqlType Origin)
			: base (Origin)
		{
			
		}
	}

	public class ResultSet : GResultSet<Column>
	{
	}

	public class Procedure : GProcedure<Column, Argument, ResultSet>
	{
	}

	public class Module : GModule<SqlType, Procedure, Column, Argument, ResultSet>
	{
	}
	#endregion
}
