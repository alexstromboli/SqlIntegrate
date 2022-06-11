using System.Collections.Generic;

namespace ParseProcs
{
	public class DatabaseContext
	{
		public string DatabaseName;
		public SqlTypeMap TypeMap;
		public Dictionary<string, DbTable> TablesDict;
		public Dictionary<string, Procedure> ProceduresDict;
		public Dictionary<string, PSqlType> FunctionsDict;
		public List<string> SchemaOrder;

		public PSqlType GetTypeForName (params string[] TypeName)
		{
			return TypeMap.GetTypeForName (TypeName);
		}
	}
}
