using System;
using System.Linq;
using System.Collections.Generic;

namespace ParseProcs
{
	public class SelectStatement : ITableRetriever
	{
		public Func<RequestContext, IReadOnlyList<NamedTyped>> List { get; }
		// can be null
		public FromTableExpression[] Froms { get; }
		public string Name { get; }

		public SelectStatement (Func<RequestContext, IReadOnlyList<NamedTyped>> List, FromTableExpression[] Froms,
			string Name = null)
		{
			this.List = List;
			this.Froms = Froms;
			this.Name = Name;
		}

		public SelectStatement (SelectStatement Core, string Name)
			: this (Core.List, Core.Froms, Name)
		{
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			// name (simple or qualified) to NamedTyped
			List<Tuple<string, NamedTyped>> AllColumns = new List<Tuple<string, NamedTyped>> ();

			Dictionary<string, IReadOnlyList<NamedTyped>> Asterisks =
				new Dictionary<string, IReadOnlyList<NamedTyped>> ();
			List<NamedTyped> AllAsteriskedEntries = new List<NamedTyped> ();

			if (Froms != null && Froms.Length > 0)
			{
				foreach (var f in Froms)
				{
					ITable Table = f.TableRetriever.GetTable (Context);
					var Refs = Table.GetAllColumnReferences (Context.ModuleContext, f.Alias);
					AllColumns.AddRange (Refs.Columns.Select (p => new Tuple<string, NamedTyped> (p.Key, p.Value)));

					foreach (var ast in Refs.Asterisks)
					{
						if (ast.Key == "*")
						{
							AllAsteriskedEntries.AddRange (ast.Value);
						}

						if (ast.Key != "*")
						{
							Asterisks[ast.Key] = ast.Value;
						}
					}
				}
			}

			// found immediate columns
			// + variables
			var AllNamedDict = AllColumns
					.Concat (Context.ModuleContext.VariablesDict.Select (p => new Tuple<string, NamedTyped> (p.Key, p.Value)))
					.ToLookup (c => c.Item1)
					.Where (g => g.Count () == 1)
					.ToDictionary (g => g.Key, g => g.First ().Item2)
				;

			Asterisks["*"] = AllAsteriskedEntries
					.ToLookup (c => c.Name)
					.Where (g => g.Count () == 1)
					.Select (g => g.First ())
					.ToArray ()
				;

			RequestContext NewContext = new RequestContext (Context, null, AllNamedDict, Asterisks);

			SortedSet<string> FoundNames = new SortedSet<string> ();
			Table Result = new Table (Name);
			foreach (var nt in List (NewContext))
			{
				if (nt.Name != null && !FoundNames.Contains (nt.Name))
				{
					FoundNames.Add (nt.Name);
					Result.AddColumn (nt);
				}
				else if (!OnlyNamed)
				{
					Result.AddColumn (nt);
				}
			}

			return Result;
		}
	}
}
