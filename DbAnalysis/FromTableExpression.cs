using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class FromTableExpression
	{
		public ITableRetriever TableRetriever { get; }
		public Sourced<string> Alias { get; }

		public FromTableExpression (ITableRetriever TableRetriever, Sourced<string> Alias)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
		}
	}
}
