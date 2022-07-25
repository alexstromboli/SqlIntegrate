namespace DbAnalysis
{
	public class OrdinarySelect
	{
		public NamedTyped[] List { get; }
		public FromTableExpression[] FromClause { get; }		// can be null

		public OrdinarySelect (NamedTyped[] List, FromTableExpression[] FromClause)
		{
			this.List = List;
			this.FromClause = FromClause;
		}
	}
}
