using System.Linq;
using System.Collections.Generic;

using Utils;

namespace ParseProcs
{
	public class RequestContext
	{
		public ModuleContext ModuleContext { get; }
		public IReadOnlyDictionary<string, ITable>[] TableRefChain { get; }
		public IReadOnlyDictionary<string, NamedTyped> NamedDict { get; }
		public IReadOnlyDictionary<string, IReadOnlyList<NamedTyped>> Asterisks { get; }

		public RequestContext (ModuleContext ModuleContext)
		{
			this.ModuleContext = ModuleContext;

			// table, as accessible in ModuleContext, considering schema order
			this.TableRefChain = ModuleContext
					.TablesDict
						// schema + name
					.Select (t => new { name = t.Key, table = t.Value })
						// name
					.Concat (ModuleContext.TablesDict.Values.Where (t =>
							!ModuleContext.SchemaOrder.TakeWhile (s => s != t.Schema).Any (s => ModuleContext.TablesDict.ContainsKey (s + "." + t.Name)))
						.Select (t => new { name = t.Name, table = t })
					)
						// database + schema + name
					.Concat (ModuleContext.TablesDict
						.Select (t => new { name = ModuleContext.DatabaseContext.DatabaseName + "." + t.Key, table = t.Value })
					)
					.ToDictionary (t => t.name, t => (ITable)t.table)
				.ToTrivialArray ()
				;

			this.NamedDict = ModuleContext.VariablesDict;
			// asterisks only appear in in-select contexts
			this.Asterisks = new Dictionary<string, IReadOnlyList<NamedTyped>> ();
		}

		public RequestContext (RequestContext ParentContext,
			IReadOnlyDictionary<string, ITable> TableRefsToPrepend = null,
			IReadOnlyDictionary<string, NamedTyped> NamedDictToOverride = null,
			IReadOnlyDictionary<string, IReadOnlyList<NamedTyped>> Asterisks = null
		)
		{
			this.ModuleContext = ParentContext.ModuleContext;

			this.TableRefChain = TableRefsToPrepend == null
					? ParentContext.TableRefChain
					: TableRefsToPrepend.ToTrivialArray ()
						.Concat (ParentContext.TableRefChain)
						.ToArray ()
				;

			this.NamedDict = NamedDictToOverride ?? ModuleContext.VariablesDict;
			this.Asterisks = Asterisks ?? ParentContext.Asterisks;
		}

		public IReadOnlyList<NamedTyped> GetAsterisk (string AsteriskEntry)
		{
			return Asterisks[AsteriskEntry];
		}
	}
}
