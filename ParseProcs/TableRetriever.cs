using System;
using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITableRetriever
	{
		ITable GetTable (IRequestContext Context);
	}

	public class NamedTableRetriever : ITableRetriever
	{
		public string[] NameL;

		public NamedTableRetriever (string[] NameL)
		{
			this.NameL = NameL;
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

			public override NamedTyped[] GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
			{
				string Name = Alias ?? Columns[0].Name;
				PSqlType Type = Columns[0].Type;
				return new [] { new NamedTyped (Name, Type), new NamedTyped (Name + "." + Name, Type) };
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
