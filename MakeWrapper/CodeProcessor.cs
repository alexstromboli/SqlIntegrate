using System.Linq;
using System.Collections.Generic;

using ParseProcs;
using ParseProcs.Datasets;

namespace MakeWrapper
{
	public class CodeProcessor
	{
		public virtual void OnHaveModule (Module Module)
		{
		}

		public virtual void OnHaveTypeMap (SqlTypeMap DbTypeMap, Dictionary<string, string> TypeMap)
		{
		}

		public virtual void OnHaveWrapper (Wrapper Wrapper)
		{
		}
	}

	public class NodaTimeCodeProcessor : CodeProcessor
	{
		public override void OnHaveTypeMap (SqlTypeMap DbTypeMap, Dictionary<string, string> TypeMap)
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
					TypeMap[prefix + cm.sql_type] = cm.clr_type;
					TypeMap[prefix + cm.sql_type + "[]"] = cm.clr_type.Trim ('?') + "[]";
				}
			}
		}

		public override void OnHaveWrapper (Wrapper Wrapper)
		{
			Wrapper.Usings.Add ("using NodaTime;");
		}
	}
}
