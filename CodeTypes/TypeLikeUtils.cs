using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeTypes;

public static class TypeLikeUtils
{
	public static bool IsNullableT (this Type Type)
	{
		return Nullable.GetUnderlyingType (Type) != null;
	}

	public static TypeLike[] ToTypeLikeArray (this IEnumerable<Type> Types)
	{
		return Types.Select (t => new TypeLike (t)).ToArray ();
	}

	public static TypeLike[] ToTypeLikeArray (this IEnumerable<NullabilityInfo> Types)
	{
		return Types.Select (t => new TypeLike (t)).ToArray ();
	}

	public static string ChopPureGenericFullName (string FullName)
	{
		return Regex.Match (FullName!, @"\A(?<name>[^`]+)").Groups["name"].Value;
	}

	//
	public static Type? GetCollectionItemType (Type T)
	{
		Type? V = null;

		// Case 1: If T is an array
		if (T.IsArray)
		{
			V = T.GetElementType ();
			return V;
		}

		// Case 2: If T implements IEnumerable<Z>
		var EnumerableImpl = T.GetInterfaces ()
			.FirstOrDefault (i => i.IsGenericType && i.GetGenericTypeDefinition () == typeof(IEnumerable<>));
		if (EnumerableImpl != null)
		{
			V = EnumerableImpl.GetGenericArguments ()[0];
			return V;
		}

		// Case 3: If T implements IEnumerable
		if (typeof(IEnumerable).IsAssignableFrom (T))
		{
			V = typeof(object);
			return V;
		}

		return null;
	}
}
