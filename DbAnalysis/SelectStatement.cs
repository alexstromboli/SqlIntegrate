using System;
using System.Linq;
using System.Collections.Generic;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class SelectStatement : ITableRetriever
	{
		public RcFunc<OrdinarySelect> RcSelect { get; }
		//public RcFunc<NamedTyped[]> List { get; }
		// can be null
		//public FromTableExpression[] Froms { get; }
		public Sourced<string> Name { get; }
		protected RcFunc<int> ExpressionsToTest;

		public SelectStatement (//RcFunc<NamedTyped[]> List, FromTableExpression[] Froms,
			RcFunc<OrdinarySelect> RcSelect,
			Sourced<string> Name,
			RcFunc<int> ExpressionsToTest/*exp*/)
		{
			this.RcSelect = RcSelect;
			//this.List = List;
			//this.Froms = Froms;
			this.Name = Name;
			this.ExpressionsToTest = ExpressionsToTest;
		}

		public SelectStatement (SelectStatement Core, Sourced<string> Name, RcFunc<int> ExpressionsToTest/*exp*/)
			: this (Core.RcSelect, Name, ExpressionsToTest)
		{
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			ExpressionsToTest (Context);

			// name (simple or qualified) to NamedTyped
			List<Tuple<string, NamedTyped>> AllColumns = new List<Tuple<string, NamedTyped>> ();

			Dictionary<string, IReadOnlyList<NamedTyped>> Asterisks =
				new Dictionary<string, IReadOnlyList<NamedTyped>> ();
			List<NamedTyped> AllAsteriskedEntries = new List<NamedTyped> ();

			OrdinarySelect OrdSelect = RcSelect (Context);
			RequestContext CurrentContext = Context;
			if (OrdSelect.FromClause != null && OrdSelect.FromClause.Length > 0)
			{
				foreach (var f in OrdSelect.FromClause)
				{
					ITable Table = f.TableExpression.TableRetriever (Context).GetTable (CurrentContext);
					var Refs = Table.GetAllColumnReferences (Context.ModuleContext, f.TableExpression.Alias);
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

					CurrentContext = new RequestContext (CurrentContext);	// here: populate other parameters

					// test 'ON' expression
					f.Condition?.Invoke (CurrentContext);
				}
			}

			// found immediate columns
			// + variables
			var AllNamedDict = AllColumns
					.Concat (CurrentContext.ModuleContext.VariablesDict.Select (p => new Tuple<string, NamedTyped> (p.Key, p.Value)))
					.ToLookup (c => c.Item1)
					.Where (g => g.Count () == 1)
					.ToDictionary (g => g.Key, g => g.First ().Item2)
				;

			Asterisks["*"] = AllAsteriskedEntries
					.ToLookup (c => c.Name.Value)
					.Where (g => g.Count () == 1)
					.Select (g => g.First ())
					.ToArray ()
				;

			RequestContext NewContext = new RequestContext (CurrentContext, null, AllNamedDict, Asterisks);

			SortedSet<string> FoundNames = new SortedSet<string> ();
			Table Result = new Table (Name);
			foreach (var nt in OrdSelect.List)
			{
				if (nt.Name != null && !FoundNames.Contains (nt.Name.Value))
				{
					FoundNames.Add (nt.Name.Value);
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
