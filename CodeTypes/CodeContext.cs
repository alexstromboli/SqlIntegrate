using System;
using System.Collections.Generic;

namespace CodeTypes;

public class CodeContext
{
	public static readonly CodeContext FullNames = new([], false);
	public static readonly CodeContext Simple = new([], true);

	protected readonly Dictionary<string, string> FullNamesToAliasesImpl;
	public IReadOnlyDictionary<string, string> FullNamesToAliases => FullNamesToAliasesImpl;

	protected readonly SortedSet<string> UsingsImpl;
	public IReadOnlySet<string> Usings => UsingsImpl;

	protected CodeContext (string[] Usings, bool AddCsAliases)
	{
		UsingsImpl = new SortedSet<string> (Usings);

		FullNamesToAliasesImpl = new Dictionary<string, string> ();
		if (AddCsAliases)
		{
			AddAlias (typeof(bool), "bool");
			AddAlias (typeof(byte), "byte");
			AddAlias (typeof(char), "char");
			AddAlias (typeof(decimal), "decimal");
			AddAlias (typeof(double), "double");
			AddAlias (typeof(float), "float");
			AddAlias (typeof(int), "int");
			AddAlias (typeof(long), "long");
			AddAlias (typeof(object), "object");
			AddAlias (typeof(sbyte), "sbyte");
			AddAlias (typeof(short), "short");
			AddAlias (typeof(string), "string");
			AddAlias (typeof(uint), "uint");
			AddAlias (typeof(ulong), "ulong");
			AddAlias (typeof(ushort), "ushort");
			AddAlias (typeof(void), "void");
		}
	}

	public CodeContext (params string[] Usings)
		: this (Usings, true)
	{
	}

	protected void AddAlias (Type Type, string Alias)
	{
		FullNamesToAliasesImpl.Add (Type.FullName!, Alias);
	}
}
