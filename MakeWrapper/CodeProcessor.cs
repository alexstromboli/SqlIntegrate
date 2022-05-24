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

			foreach (var p in DbTypeMap.Map.Where (p => !p.Value.IsArray))
			{
				if (ClrType.Map.TryGetValue (p.Value.ClrType, out var ct))
				{
					if (p.Value.IsDate)
					{
						TypeMap[p.Key] = "Instant?";
						TypeMap[p.Key + "[]"] = "Instant[]";
					}
					else if (p.Value.IsTimeSpan)
					{
						TypeMap[p.Key] = "LocalTime?";
						TypeMap[p.Key + "[]"] = "LocalTime[]";
					}
				}
			}

			TypeMap[PgCatalogPrefix + "timestamp"] = "LocalDateTime?";
			TypeMap[PgCatalogPrefix + "timestamp[]"] = "LocalDateTime[]";
			TypeMap[PgCatalogPrefix + "timestamp without time zone"] = "LocalDateTime?";
			TypeMap[PgCatalogPrefix + "timestamp without time zone[]"] = "LocalDateTime[]";

			TypeMap[PgCatalogPrefix + "date"] = "LocalDate?";
			TypeMap[PgCatalogPrefix + "date[]"] = "LocalDate[]";

			TypeMap[PgCatalogPrefix + "interval"] = "Period";
			TypeMap[PgCatalogPrefix + "interval[]"] = "Period[]";
		}

		public override void OnHaveWrapper (Wrapper Wrapper)
		{
			Wrapper.Usings.Add ("using NodaTime;");
		}
	}
}
