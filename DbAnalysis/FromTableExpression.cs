namespace DbAnalysis
{
	public class FromTableExpression
	{
		public ITableRetriever TableRetriever { get; }
		public string Alias { get; }

		public FromTableExpression (ITableRetriever TableRetriever, string Alias)
		{
			this.TableRetriever = TableRetriever;
			this.Alias = Alias;
		}
	}
}
