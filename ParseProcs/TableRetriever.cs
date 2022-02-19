using System;
using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITableRetriever
	{
		ITable GetTable (IRequestContext Context);
	}

	public class DbTableRetriever : ITableRetriever
	{
		public string[] Name;

		public DbTableRetriever (string[] Name)
		{
			this.Name = Name;
		}

		public ITable GetTable (IRequestContext Context)
		{
			throw new NotImplementedException ();
		}
	}

	public class UnnestTableRetriever : ITableRetriever
	{
		public class Table : BasicTable
		{
			public override IReadOnlyList<NamedTyped> Columns { get; }

			public Table (NamedTyped SingleColumn)
			{
				Columns = new[] { SingleColumn };
			}
		}

		public string FunctionName;
		public Func<IRequestContext, NamedTyped> Parameter;

		public UnnestTableRetriever (Func<IRequestContext, NamedTyped> Parameter, string FunctionName = null)
		{
			this.FunctionName = FunctionName ?? "unnest";
			this.Parameter = Parameter;
		}

		public ITable GetTable (IRequestContext Context)
		{
			return new Table (new NamedTyped (FunctionName, Parameter (Context).Type));
		}
	}
}
