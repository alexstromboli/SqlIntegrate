using System.Collections.Generic;
using System.Linq;

namespace DbAnalysis.Cache
{
	// One function call site's dependency: the name as written, the argument types it was
	// resolved against, and what it resolved to. The segments are kept rather than the
	// resolved key, because resolution walks the schema order -- replaying the lookup is
	// what catches a function that has appeared earlier on the path, as well as one whose
	// return type changed.
	public class CalleeSignature
	{
		public List<string> NameSegments;

		// The call site's argument types, by display name, in order; an entry is null where
		// the argument could not be typed. They are part of the dependency and not merely
		// of the key: which overload of a name the call means is decided from them, so an
		// overload added or removed since changes the inference exactly as a changed return
		// type does.
		public List<string> ArgumentTypes;

		// The declared parameter each argument named, parallel to the types and null where
		// it bound by position. Recorded for the same reason the types are: a named
		// argument faces the parameter it names rather than the one at its position, so a
		// replay that lost the names would resolve the call positionally and could answer
		// with another overload -- silently, the entry having verified.
		public List<string> ArgumentNames;

		// Whether the call passed a variadic array whole rather than naming its elements.
		// VARIADIC may be written only on the last argument, so one flag describes the
		// call. It decides whether that argument is matched against the array type or
		// against the element type, which is another way one name's overloads are told
		// apart.
		public bool PassesVariadicArray;

		// Null when the name resolves to no function, or to one whose return type nothing
		// can carry a value of. That is a state of its own: a function created later
		// changes the inference, so it must not read as a match against any return type.
		public string ReturnType;
	}

	// What one cache entry holds: the analysis, and the closure of callee signatures it was
	// inferred against.
	public class CachedAnalysis
	{
		public Datasets.Procedure Procedure;
		public List<CalleeSignature> CalleeSignatures;

		// The callee closure cannot be part of the key: it is only known after the parse
		// the key selects. Hashing signatures into the shared data-layout segment instead
		// would discard every procedure in the database on any signature change anywhere,
		// so the closure is verified here, per entry, at the point of use.
		public bool MatchesCallees (DatabaseContext DatabaseContext)
		{
			// An entry with no recorded closure says nothing about its callees, and cannot
			// be told apart from one whose callees all still match. Refuse it.
			if (Procedure == null || CalleeSignatures == null)
			{
				return false;
			}

			foreach (var Callee in CalleeSignatures)
			{
				// An entry that records no arguments cannot be replayed: resolution consults
				// them, and a zero-argument call is an empty list rather than an absent one,
				// so nothing distinguishes "no arguments" from "not recorded". The names are
				// held to the same rule, and to being aligned with the types -- one list
				// shorter than the other would bind an argument to a parameter the call
				// never named.
				if (Callee.NameSegments == null || Callee.ArgumentTypes == null
				    || Callee.ArgumentNames == null
				    || Callee.ArgumentNames.Count != Callee.ArgumentTypes.Count)
				{
					return false;
				}

				var Arguments = Callee.ArgumentTypes
						.Select ((n, Index) => new CallArgument
						{
							Type = n == null ? null : DatabaseContext.GetTypeForName (n),
							Name = Callee.ArgumentNames[Index],
							IsVariadicArray = Callee.PassesVariadicArray
							                  && Index == Callee.ArgumentTypes.Count - 1
						})
						.ToList ()
					;

				if (DatabaseContext.ResolveFunction (Callee.NameSegments.ToArray (), Arguments)
					    ?.ReturnType?.Display
				    != Callee.ReturnType)
				{
					return false;
				}
			}

			return true;
		}
	}
}
