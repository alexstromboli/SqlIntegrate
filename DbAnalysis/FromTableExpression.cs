using System;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class FromTableExpression
	{
		public Func<RequestContext, ITableRetriever> TableRetriever { get; }
		public Sourced<string> Alias { get; }

		public FromTableExpression (Func<RequestContext, ITableRetriever> TableRetriever, Sourced<string> Alias)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
		}
	}
}
