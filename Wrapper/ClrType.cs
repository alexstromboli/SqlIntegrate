using System.Collections.Generic;

using CodeTypes;
using DbAnalysis;

namespace Wrapper
{
	// this map is closed
	// only for native .NET types
	// third parties' types (like NodaTime) must be handled separately
	// as their assemblies have no real need to be linked to this utility
	// names are sufficient for purposes of code generator
	public class ClrType
	{
		protected static Dictionary<string, TypeLike> _Map;
		public static IReadOnlyDictionary<string, TypeLike> Map => _Map;

		static ClrType ()
		{
			_Map = new Dictionary<string, TypeLike> ();

			Add (TypeLikeStatic.Object);
			Add (TypeLikeStatic.Bool);
			Add (TypeLikeStatic.Int);
			Add (TypeLikeStatic.UInt);
			Add (TypeLikeStatic.Short);
			Add (TypeLikeStatic.Long);
			Add (TypeLikeStatic.Decimal);
			Add (TypeLikeStatic.Float);
			Add (TypeLikeStatic.Double);
			Add (TypeLikeStatic.String);
			Add (TypeLikeStatic.Guid);
			Add (TypeLikeStatic.DateTime);
			Add (TypeLikeStatic.TimeSpan);
		}

		protected static void Add (TypeLike TypeLike)
		{
			_Map[TypeLike.UniqueName] = TypeLike;
		}
	}
}
