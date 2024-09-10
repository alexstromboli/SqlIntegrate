using System;
using System.Collections.Generic;

using CodeTypes;

namespace Wrapper
{
	// this map is closed
	// only for native .NET types
	// third parties' types (like NodaTime) must be handled separately
	// as their assemblies have no real need to be linked to this utility
	// names are sufficient for purposes of code generator
	public class ClrType
	{
		protected static Dictionary<Type, TypeLike> _Map;
		public static IReadOnlyDictionary<Type, TypeLike> Map => _Map;

		static ClrType ()
		{
			_Map = new Dictionary<Type, TypeLike> ();

			Add (typeof (object));
			Add (typeof (bool));
			Add (typeof (int));
			Add (typeof (uint));
			Add (typeof (Int16));
			Add (typeof (Int64));
			Add (typeof (decimal));
			Add (typeof (float));
			Add (typeof (double));
			Add (typeof (string));
			Add (typeof (Guid));
			Add (typeof (DateTime));
			Add (typeof (TimeSpan));
		}

		protected static void Add (Type Type)
		{
			_Map[Type] = new TypeLike (Type);
		}
	}
}
