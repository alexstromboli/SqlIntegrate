using System.Linq;
using System.Collections.Generic;

using Utils;
using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class ModuleContext
	{
		public DatabaseContext DatabaseContext { get; }
		public string ModuleName { get; }

		public IReadOnlyDictionary<string, DbTable> TablesDict => DatabaseContext.TablesDict;

		protected Dictionary<string, NamedTyped> _VariablesDict;
		public IReadOnlyDictionary<string, NamedTyped> VariablesDict => _VariablesDict;

		public IReadOnlyDictionary<string, PSqlType> FunctionsDict => DatabaseContext.FunctionsDict;

		public IReadOnlyList<string> SchemaOrder => DatabaseContext.SchemaOrder;

		public ModuleContext (
			string ModuleName,
			DatabaseContext DatabaseContext,
			IReadOnlyDictionary<string, NamedTyped> VariablesDict
		)
		{
			this.ModuleName = ModuleName.ToLower ();
			this.DatabaseContext = DatabaseContext;
			_VariablesDict = new Dictionary<string, NamedTyped> (VariablesDict);
		}

		protected T GetSchemaEntity<T> (IReadOnlyDictionary<string, T> Dict, string[] NameSegments)
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

		public NamedTyped GetFunction (Sourced<string>[] NameSegments)
		{
			Sourced<string> Name = NameSegments[^1].ToLower ();
			var Span = NameSegments.Range ();
			Sourced<PSqlType> Type = GetSchemaEntity (FunctionsDict, NameSegments.Values ()).SourcedFunction (Span)
			                ?? DatabaseContext.TypeMap.Null.SourcedTextSpan (Span);

			return new NamedTyped (Name, Type);
		}

		public DbTable GetTable (string[] NameSegments)
		{
			return GetSchemaEntity (TablesDict, NameSegments);
		}
	}
}
