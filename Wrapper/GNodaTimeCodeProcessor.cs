using System.Collections.Generic;

using CodeTypes;
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

			TypeLike tlInstant = new TypeLike ("Instant", true);
			TypeLike tlLocalDateTime = new TypeLike ("LocalDateTime", true);
			TypeLike tlLocalTime = new TypeLike ("LocalTime", true);
			TypeLike tlLocalDate = new TypeLike ("LocalDate", true);
			TypeLike tlPeriod = new TypeLike ("Period", false);

			foreach (var cm in new[]
			         {
				         new { sql_type = "timestamptz", type_like = tlInstant },
				         new { sql_type = "timestamp with time zone", type_like = tlInstant },
				         new { sql_type = "timestamp", type_like = tlLocalDateTime },
				         new { sql_type = "timestamp without time zone", type_like = tlLocalDateTime },
				         new { sql_type = "timetz", type_like = tlLocalTime },
				         new { sql_type = "time with time zone", type_like = tlLocalTime },
				         new { sql_type = "time", type_like = tlLocalTime },
				         new { sql_type = "time without time zone", type_like = tlLocalTime },
				         new { sql_type = "interval", type_like = tlPeriod },
				         new { sql_type = "date", type_like = tlLocalDate }
			         })
			{
				foreach (var prefix in new[] { "", PgCatalogPrefix })
				{
					TypeMap[prefix + cm.sql_type].CoreTypeLike = cm.type_like;
				}
			}
		}

		public override void OnHaveWrapper (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database)
		{
			Database.Usings.Add ("NodaTime");
		}
	}

	public class NodaTimeCodeProcessor : GNodaTimeCodeProcessor<SqlType, Procedure, Column, Argument, ResultSet, Module>
	{
	}
}
