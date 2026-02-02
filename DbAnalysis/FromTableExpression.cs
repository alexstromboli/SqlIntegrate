using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class FromTableExpression
	{
		public ITableRetriever TableRetriever { get; }
		public Sourced<string> Alias { get; }
		public Sourced<string>[] ColumnNames { get; }

		public FromTableExpression (ITableRetriever TableRetriever, Sourced<string> Alias,
			Sourced<string>[] ColumnNames = null)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
			this.ColumnNames = ColumnNames;
		}
	}
}
