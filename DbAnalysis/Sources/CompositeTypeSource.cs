namespace DbAnalysis.Sources
{
	public class CompositeTypeSource : ISource
	{
		// here: replace with reference to PSqlType?
		public string SchemaName { get; } = null;
		public string TypeName { get; } = null;
		public string PropertyName { get; } = null;

		public CompositeTypeSource (string SchemaName, string TypeName, string PropertyName)
		{
			this.SchemaName = SchemaName;
			this.TypeName = TypeName;
			this.PropertyName = PropertyName;
		}

		public override string ToString ()
		{
			return $"type {SchemaName}.{TypeName}, property {PropertyName}";
		}
	}
}
