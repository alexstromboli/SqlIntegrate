using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class FromTableExpression
	{
		public RcFunc<ITableRetriever> TableRetriever { get; }
		public Sourced<string> Alias { get; }

		public FromTableExpression (RcFunc<ITableRetriever> TableRetriever, Sourced<string> Alias)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
		}
	}
}
