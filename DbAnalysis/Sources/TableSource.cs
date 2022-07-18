namespace DbAnalysis.Sources
{
	public class TableSource : ISource
	{
		// here: replace with reference to Table?
		public string SchemaName { get; } = null;
		public string TableName { get; } = null;
		public string TableColumnName { get; } = null;

		public TableSource (string SchemaName, string TableName, string TableColumnName)
		{
			this.SchemaName = SchemaName;
			this.TableName = TableName;
			this.TableColumnName = TableColumnName;
		}

		public override string ToString ()
		{
			return $"table {SchemaName}.{TableName}, column {TableColumnName}";
		}
	}
}
