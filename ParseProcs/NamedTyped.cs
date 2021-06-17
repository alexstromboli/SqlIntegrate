using System;

namespace ParseProcs
{
	public class NamedTyped
	{
		// can be null
		public virtual string Name { get; protected set; }
		public virtual PSqlType Type { get; protected set; }
		public override string ToString () => $"{Name ?? "???"} {Type.Display}";

		public NamedTyped (string Name, PSqlType Type)
		{
			this.Name = Name?.ToLower ();
			this.Type = Type;
		}

		public NamedTyped (PSqlType Type)
			: this (null, Type)
		{
		}
	}

	public class NamedLazyTyped : NamedTyped
	{
		protected Func<PSqlType> TypeGetter;

		protected PSqlType _Type = null;
		public override PSqlType Type
		{
			get
			{
				if (_Type == null)
				{
					_Type = TypeGetter ();
				}

				return _Type;
			}

			protected set
			{
				_Type = value;
			}
		}

		public NamedLazyTyped (string Name, Func<PSqlType> TypeGetter)
			: base (Name, PSqlType.Null)
		{
			this.TypeGetter = TypeGetter;
		}
	}
}
