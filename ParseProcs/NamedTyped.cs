namespace ParseProcs
{
	public class NamedTyped
	{
		public string Name { get; protected set; }
		public PSqlType Type { get; protected set; }
		public override string ToString () => $"{Name} {Type.Display}";

		public NamedTyped (string Name, PSqlType Type)
		{
			this.Name = Name.ToLower ();
			this.Type = Type;
		}
	}
}
