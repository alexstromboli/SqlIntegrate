namespace ParseProcs
{
	public class Argument : NamedTyped
	{
		public enum DirectionType
		{
			In,
			InOut
		}
		
		public DirectionType Direction { get; protected set; }
		
		public Argument (string Name, PSqlType Type, DirectionType Direction)
			: base (Name, Type)
		{
			this.Direction = Direction;
		}

		public override string ToString ()
		{
			return base.ToString () + (Direction == DirectionType.InOut ? " INOUT" : "");
		}
	}
}
