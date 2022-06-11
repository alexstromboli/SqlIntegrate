namespace ParseProcs
{
	public abstract class SchemaEntity
	{
		public string Schema { get; protected set; }
		public string Name { get; protected set; }

		public static string GetDisplay (string Schema, string Name)
		{
			return PSqlUtils.PSqlQualifiedName (Schema, Name);
		}

		public string Display { get; protected set; }
		public override string ToString () => Display;

		public SchemaEntity (string Schema, string Name)
		{
			this.Schema = Schema.ToLower ();
			this.Name = Name.ToLower ();
			Display = GetDisplay (Schema, Name);
		}
	}
}
