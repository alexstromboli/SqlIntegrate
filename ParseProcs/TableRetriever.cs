using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITableRetriever
	{
		ITable GetTable (ModuleContext Context);
	}

	public class DbTableRetriever : ITableRetriever
	{
		public string[] Name;

		public DbTableRetriever (string[] Name)
		{
			this.Name = Name;
		}

		public ITable GetTable (ModuleContext Context)
		{
			throw new System.NotImplementedException ();
		}
	}
}
