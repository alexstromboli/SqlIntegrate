using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Utils
{
	public static class EnumerableUtils
	{
		public class Item<T>
		{
			public T Value;
			public bool IsFirst;
			public int Index;
		}

		public class CollItem<T> : Item<T>
		{
			public bool IsLast;
		}

		public static IEnumerable<Item<T>> Indexed<T> (this IEnumerable<T> coll)
		{
			int i = -1;
			return coll.Select (e =>
			{
				++i;
				return new Item<T> { Value = e, Index = i, IsFirst = i == 0 };
			});
		}

		public static IEnumerable<CollItem<T>> Indexed<T> (this ICollection<T> coll)
		{
			int i = -1;
			return coll.Select (e =>
			{
				++i;
				return new CollItem<T> { Value = e, Index = i, IsFirst = i == 0, IsLast = i == coll.Count - 1 };
			});
		}
	}

	public static class CsUtils
	{
		public static SortedSet<string> CsKeywords = new SortedSet<string> (new[] { "abstract", "event", "new", "struct", "as", "explicit", "null", "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw", "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float", "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected", "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe", "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal", "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate", "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock", "stackalloc", "else", "long", "static", "enum", "namespace", "string" });

		public static string ValidCsName (this string Name)
		{
			Name = Name.ValidCsNamePart ();

			if (CsKeywords.Contains (Name))
			{
				return "_" + Name;
			}

			return Name;
		}

		public static string ToDoubleQuotes (this string Input)
		{
			return new StringBuilder ("\"")
					.Append (Input.Replace ("\"", "\\\""))
					.Append ('\"')
					.ToString ()
				;
		}

		public static string ValidCsNamePart (this string Name)
		{
			if (Name.Contains (' '))
			{
				return Name.Replace (' ', '_');
			}

			return Name;
		}
	}
}
