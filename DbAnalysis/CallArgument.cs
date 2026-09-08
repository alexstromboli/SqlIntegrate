using DbAnalysis.Sources;

namespace DbAnalysis
{
	// One argument as a call site writes it. A call site names an input by its position, by
	// the declared parameter's name, or -- in the last position of a variadic call -- as the
	// whole array rather than as one of its elements. All three decide which overload of a
	// name the call means, so all three reach resolution rather than the type alone: by
	// position, a named call resolves against whichever parameters happen to sit where its
	// arguments were written, and a variadic array resolves as an element of itself.
	public class CallArgument
	{
		// The type the expression carries, or null where the surrounding context cannot type
		// it. An argument that cannot be typed constrains nothing rather than failing the
		// call.
		public PSqlType Type;

		// The declared parameter this argument binds to, or null where it binds by position.
		// A name no parameter carries rejects the overload, which is what PostgreSQL does
		// with it.
		public string Name;

		// Whether the call site wrote VARIADIC on this argument. The variadic array is then
		// passed whole, so the value is matched against the array type and not against the
		// element type an expanded call names.
		public bool IsVariadicArray;
	}

	// The same argument as the grammar sees it, before anything has been typed. The
	// expression is kept whole rather than evaluated, because typing it needs a request
	// context the parse does not have; the name and the VARIADIC spelling are already
	// final, being written at the call site rather than inferred from it.
	public class ParsedCallArgument
	{
		public Sourced<string> Name;
		public bool IsVariadicArray;
		public SPolynom Expression;
	}
}
