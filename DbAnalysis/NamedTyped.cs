using Sprache;	// for utility

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class NamedTyped
	{
		// can be null
		public virtual Sourced<string> Name { get; protected set; }
		public virtual Sourced<PSqlType> Type { get; protected set; }
		public override string ToString () => $"{Name?.Value ?? "???"} {Type?.Value?.Display ?? "???"}";

		public NamedTyped (Sourced<string> Name, Sourced<PSqlType> Type)
		{
			this.Name = Name?.Select (s => s.ToLower ());
			this.Type = Type;
		}

		public NamedTyped (Sourced<PSqlType> Type)
			: this (null, Type)
		{
		}

		public NamedTyped WithName (Sourced<string> NewName)
		{
			return new NamedTyped (NewName, Type);
		}

		public NamedTyped WithName (ITextSpan<string> NewName)
		{
			return WithName (NewName.ToSourced ());
		}

		public NamedTyped WithType (Sourced<PSqlType> NewType)
		{
			return new NamedTyped (Name, NewType);
		}

		public NamedTyped ToArray ()	// here: trace back the source
		{
			return new NamedTyped (Name, Type.Select (t => t.ArrayType));
		}
	}
}
