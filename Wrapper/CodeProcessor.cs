using System.Collections.Generic;

using DbAnalysis;
using DbAnalysis.Datasets;

namespace Wrapper
{
	public class GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>
			where TColumn : Column, new()
			where TArgument : Argument, new()
			where TResultSet : GResultSet<TColumn>, new()
			where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
			where TSqlType : GSqlType<TColumn>, new()
			where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
	{
		public virtual void OnHaveModule (TModule Module)
		{
		}

		public virtual void OnHaveTypeMap (SqlTypeMap DbTypeMap, Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap)
		{
		}

		public virtual void OnHaveWrapper (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database)
		{
		}
	}

	public class CodeProcessor : GCodeProcessor<SqlType, Procedure, Column, Argument, ResultSet, Module>
	{
	}
}
