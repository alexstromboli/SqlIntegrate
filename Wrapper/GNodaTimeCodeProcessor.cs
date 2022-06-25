using System.Collections.Generic;

using DbAnalysis;
using DbAnalysis.Datasets;

namespace Wrapper
{
	public class GNodaTimeCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> : GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>
		where TColumn : Column, new()
		where TArgument : Argument, new()
		where TResultSet : GResultSet<TColumn>, new()
		where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
		where TSqlType : GSqlType<TColumn>, new()
		where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
	{
		public override void OnHaveTypeMap (SqlTypeMap DbTypeMap, Dictionary<string, TypeMapping<TSqlType, TColumn>> TypeMap)
		{
			string PgCatalogPrefix = "pg_catalog.";

			foreach (var cm in new[]
			         {
				         new { sql_type = "timestamptz", clr_type = "Instant?" },
				         new { sql_type = "timestamp with time zone", clr_type = "Instant?" },
				         new { sql_type = "timestamp", clr_type = "LocalDateTime?" },
				         new { sql_type = "timestamp without time zone", clr_type = "LocalDateTime?" },
				         new { sql_type = "timetz", clr_type = "LocalTime?" },
				         new { sql_type = "time with time zone", clr_type = "LocalTime?" },
				         new { sql_type = "time", clr_type = "LocalTime?" },
				         new { sql_type = "time without time zone", clr_type = "LocalTime?" },
				         new { sql_type = "interval", clr_type = "Period" },
				         new { sql_type = "date", clr_type = "LocalDate?" }
			         })
			{
				foreach (var prefix in new[] { "", PgCatalogPrefix })
				{
					TypeMap[prefix + cm.sql_type].CsTypeName = () => cm.clr_type;
				}
			}
		}

		public override void OnHaveWrapper (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database)
		{
			Database.Usings.Add ("using NodaTime;");
		}
	}

	public class NodaTimeCodeProcessor : GNodaTimeCodeProcessor<SqlType, Procedure, Column, Argument, ResultSet, Module>
	{
	}
}
