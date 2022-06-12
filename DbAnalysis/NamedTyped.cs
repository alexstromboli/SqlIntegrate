using System;

namespace DbAnalysis
{
	public class NamedTyped
	{
		// can be null
		public virtual string Name { get; protected set; }
		public virtual PSqlType Type { get; protected set; }
		public override string ToString () => $"{Name ?? "???"} {Type?.Display ?? "???"}";

		public NamedTyped (string Name, PSqlType Type)
		{
			this.Name = Name?.ToLower ();
			this.Type = Type;
		}

		public NamedTyped (PSqlType Type)
			: this (null, Type)
		{
		}

		public NamedTyped WithName (string NewName)
		{
			return new NamedTyped (NewName, Type);
		}

		public NamedTyped WithType (PSqlType NewType)
		{
			return new NamedTyped (Name, NewType);
		}

		public NamedTyped ToArray ()
		{
			return new NamedTyped (Name, Type.ArrayType);
		}
	}
}
