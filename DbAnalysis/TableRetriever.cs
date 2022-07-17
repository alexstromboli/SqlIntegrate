using System;
using System.Linq;
using System.Collections.Generic;

using Utils;

namespace DbAnalysis
{
	public interface ITableRetriever
	{
		ITable GetTable (RequestContext Context, bool OnlyNamed = true);
	}

	public class NamedTableRetriever : ITableRetriever
	{
		public string[] NameL;
		public string FullName;

		public NamedTableRetriever (string[] NameL)
		{
			this.NameL = NameL;
			FullName = this.NameL.JoinDot ();
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			try
			{
				return Context
						.TableRefChain
						.SelectMany (c => c)
						.First (c => c.Key == FullName)
						.Value
					;
			}
			catch (InvalidOperationException)
			{
				throw new InvalidOperationException ("Not found " + FullName);
			}
		}
	}

	public class UnnestTableRetriever : ITableRetriever
	{
		public class Table : BasicTable
		{
			public override IReadOnlyList<NamedTyped> Columns { get; }

			public Table (NamedTyped SingleColumn)
			{
				Columns = SingleColumn.ToTrivialArray ();
			}

			public override ITable.ColumnReferences GetAllColumnReferences (ModuleContext ModuleContext, Sourced<string> Alias = null)
			{
				Sourced<string> Name = Alias ?? Columns[0].Name;
				Sourced<PSqlType> Type = Columns[0].Type;
				NamedTyped Column = new NamedTyped (Name, Type);
				var ColumnsArray = Column.ToTrivialArray ();

				return new ITable.ColumnReferences
				{
					Columns = new Dictionary<string, NamedTyped>
					{
						[Name.Value] = Column,
						[Name.Value + "." + Name.Value] = Column
					},
					Asterisks = new Dictionary<string, NamedTyped[]>
					{
						["*"] = ColumnsArray,
						[Name.Value + ".*"] = ColumnsArray
					}
				};
			}
		}

		public Sourced<string> FunctionName;
		public Func<RequestContext, NamedTyped> Parameter;

		public UnnestTableRetriever (Func<RequestContext, NamedTyped> Parameter, Sourced<string> FunctionName = null)
		{
			this.FunctionName = FunctionName ?? new Sourced<string> ("unnest");
			this.Parameter = Parameter;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			return new Table (Parameter (Context).WithName (FunctionName));
		}
	}
}
