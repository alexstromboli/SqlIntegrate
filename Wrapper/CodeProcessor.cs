using System.Collections.Generic;

using DbAnalysis;
using DbAnalysis.Datasets;
using Utils.CodeGeneration;

namespace Wrapper
{
	public class DbProcProperty
	{
		public string Type;
		public string Name;
	}
	
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

		public virtual void OnCodeGenerationStarted (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			IndentedTextBuilder Builder, List<DbProcProperty> DbProcProperties)
		{
		}

		public virtual void OnCodeGeneratingDbProc (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			IndentedTextBuilder Builder)
		{
		}

		public virtual void OnEncodingParameter (
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema Schema,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure Procedure,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentCsType)
		{
		}

		public virtual void OnPassingParameter (
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema Schema,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure Procedure,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentValue)
		{
		}

		public virtual void OnReadingParameter (
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema Schema,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure Procedure,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentValue)
		{
		}

		public virtual void OnEncodingResultSetColumn (
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema Schema,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure Procedure,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set ResultSet,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set.Property Property,
			ref string ColumnCsType)
		{
		}

		public virtual void OnReadingResultSetColumn (
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema Schema,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure Procedure,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set ResultSet,
			Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>.Schema.Procedure.Set.Property Property,
			ref string ColumnValue)
		{
		}
	}

	public class CodeProcessor : GCodeProcessor<SqlType, Procedure, Column, Argument, ResultSet, Module>
	{
	}
}
