namespace DbAnalysis
{
	public class OrdinarySelect
	{
		public NamedTyped[] List { get; }
		public TableJoin[] FromClause { get; }		// can be null

		public OrdinarySelect (NamedTyped[] List, TableJoin[] FromClause, RcFunc<int> ExpressionsToTest/*exp*/)
		{
			this.List = List;
			this.FromClause = FromClause;
		}
	}
}
