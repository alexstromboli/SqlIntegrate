namespace DbAnalysis
{
	// One declared argument of a function, as the catalogue states it.
	public class DbFunctionArgument
	{
		// Null when the type map has no answer for the declared type. Such an argument
		// constrains nothing: there is no type to compare a call site's against, and
		// rejecting the overload over it would hide a function the database has.
		public PSqlType Type;

		// The declared parameter name, or null where the function declares none. A named
		// argument at a call site binds to it, wherever it sits, which is why a name is
		// part of the catalogue's view of an argument and not merely of its declaration.
		public string Name;

		// A pseudo-type argument -- anyelement, anyarray, "any" -- accepts a value of any
		// type, so it never rejects a call site, and it never outranks an overload that
		// names the argument's own type. That is what keeps lower(text) ahead of
		// lower(anyrange) for a text argument.
		public bool IsPseudo;
	}

	// One function the database has. A name is not a function: overloads of one name are
	// separate entries, because what they return is decided by the arguments written at
	// the call site and nothing else can tell them apart.
	public class DbFunction
	{
		public string QualifiedName;

		// The return type, or null when nothing can carry a value of it -- either the type
		// map has no mapping for it, or it is a pseudo-type, which no mapping could
		// describe. The two are one state for a caller asking what the call returns and two
		// for a caller asking why it got nothing, so the type's own name is kept beside it:
		// that name is the half a maintainer can act on.
		public PSqlType ReturnType;
		public string UnmappedReturnTypeName;

		// The declared input arguments, in order. OUT arguments are not among them: a call
		// site names inputs.
		public DbFunctionArgument[] Arguments;

		// How many trailing arguments carry defaults, and whether the last one is variadic.
		// Both widen the argument counts the overload accepts, and an overload rejected on
		// arity alone would make a perfectly ordinary call resolve to something else.
		public int DefaultCount;
		public bool IsVariadic;

		// What the variadic array holds, where the last argument is variadic. An expanded
		// variadic call names elements, so this is what its trailing arguments are matched
		// against; the array type declared beside it is what a call passing the array whole
		// is matched against. Null and pseudo mean here what they mean on a declared
		// argument: nothing to compare, so nothing rejected.
		public PSqlType VariadicElementType;
		public bool VariadicElementIsPseudo;
	}
}
