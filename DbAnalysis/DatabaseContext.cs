using System.Collections.Generic;
using System.Linq;

using Utils;

namespace DbAnalysis
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

		// A bare name is resolved by walking the schema order, so the schema order is part
		// of the lookup and not merely of the dictionary it reads. The whole lookup lives
		// here because it has to be replayable outside a parse: a cached analysis is
		// trusted only after its recorded callees resolve the same way again.
		public T GetSchemaEntity<T> (IReadOnlyDictionary<string, T> Dict, string[] NameSegments)
		{
			string Key = NameSegments.PSqlQualifiedName ();

			T Result;
			if (!Dict.TryGetValue (Key, out Result))
			{
				if (NameSegments.Length == 1)
				{
					foreach (string sch in SchemaOrder)
					{
						string SchKey = sch.ToTrivialArray ().Concat (NameSegments).PSqlQualifiedName ();
						if (Dict.TryGetValue (SchKey, out Result))
						{
							break;
						}
					}
				}
			}

			return Result;
		}

		// Null when the name resolves to no function at all, which is a distinct state
		// from any return type and has to stay distinguishable from one.
		public PSqlType GetFunctionType (string[] NameSegments)
		{
			return GetSchemaEntity (FunctionsDict, NameSegments);
		}
	}
}
