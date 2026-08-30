using System.Collections.Generic;

namespace DbAnalysis.Cache
{
	// One function call site's dependency: the name as written, and what it resolved to.
	// The segments are kept rather than the resolved key, because resolution walks the
	// schema order -- replaying the lookup is what catches a function that has appeared
	// earlier on the path, as well as one whose return type changed.
	public class CalleeSignature
	{
		public List<string> NameSegments;

		// Null when the name resolves to no function. That is a state of its own: a
		// function created later changes the inference, so it must not read as a match
		// against any return type.
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
				if (Callee.NameSegments == null)
				{
					return false;
				}

				if (DatabaseContext.GetFunctionType (Callee.NameSegments.ToArray ())?.Display
				    != Callee.ReturnType)
				{
					return false;
				}
			}

			return true;
		}
	}
}
