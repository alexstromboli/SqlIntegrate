using System.Linq;
using System.Collections.Generic;

using Utils;
using DbAnalysis.Cache;
using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class ModuleContext
	{
		public DatabaseContext DatabaseContext { get; }
		public string ModuleName { get; }

		public IReadOnlyDictionary<string, DbTable> TablesDict => DatabaseContext.TablesDict;

		protected Dictionary<string, NamedTyped> _VariablesDict;
		public IReadOnlyDictionary<string, NamedTyped> VariablesDict => _VariablesDict;

		public IReadOnlyDictionary<string, PSqlType> FunctionsDict => DatabaseContext.FunctionsDict;

		public IReadOnlyList<string> SchemaOrder => DatabaseContext.SchemaOrder;

		// What the procedure's inferred types depend on beyond its own source and the data
		// layout: the return type of every function it calls. The set is only known once the
		// body has been parsed, so it is collected here as resolution happens and stored
		// beside the analysis, to be replayed before a cached entry is trusted.
		protected Dictionary<string, CalleeSignature> _CalleeSignatures =
			new Dictionary<string, CalleeSignature> ();

		public IEnumerable<CalleeSignature> CalleeSignatures =>
			_CalleeSignatures.OrderBy (p => p.Key).Select (p => p.Value);

		// Functions this procedure calls that the database HAS, but whose return type the
		// analyzer has no mapping for, against that type's name. A separate collection from
		// the callee signatures because it answers a different question: not "what did this
		// resolve to" but "why did it not resolve", and only one of the two answers sends
		// the reader to the type map.
		protected Dictionary<string, string> _UnmappedReturnTypes =
			new Dictionary<string, string> ();

		// The names this procedure calls that resolve to no function at all. A search_path
		// that does not reach a function's schema resolves it the same way as one that does
		// not exist, and the reader can act on either only if the name is named. A function
		// that exists with an unmappable return type is excluded: it is not missing, and
		// sending its reader to check a search_path points away from the fix.
		public IReadOnlyList<string> UnresolvedFunctions =>
			_CalleeSignatures
				.Where (p => p.Value.ReturnType == null && !_UnmappedReturnTypes.ContainsKey (p.Key))
				.Select (p => p.Key)
				.OrderBy (s => s)
				.ToList ();

		// The calls that failed for the other reason, each named with the type that has no
		// mapping -- the type being the half a maintainer can act on.
		public IReadOnlyList<string> FunctionsWithUnmappedReturnType =>
			_UnmappedReturnTypes
				.OrderBy (p => p.Key)
				.Select (p => $"{p.Key} returns {p.Value}")
				.ToList ();

		public ModuleContext (
			string ModuleName,
			DatabaseContext DatabaseContext,
			IReadOnlyDictionary<string, NamedTyped> VariablesDict
		)
		{
			this.ModuleName = ModuleName.ToLower ();
			this.DatabaseContext = DatabaseContext;
			_VariablesDict = new Dictionary<string, NamedTyped> (VariablesDict);
		}

		protected T GetSchemaEntity<T> (IReadOnlyDictionary<string, T> Dict, string[] NameSegments)
		{
			return DatabaseContext.GetSchemaEntity (Dict, NameSegments);
		}

		public NamedTyped GetFunction (Sourced<string>[] NameSegments)
		{
			Sourced<string> Name = NameSegments[^1].ToLower ();
			var Span = NameSegments.Range ();
			string[] Segments = NameSegments.Values ();

			PSqlType Resolved = DatabaseContext.GetFunctionType (Segments);

			// Recorded under the resolution key so repeated calls to the same function
			// collapse into one entry, while two spellings of it stay separate: each is a
			// lookup in its own right and each has to be replayed as written.
			_CalleeSignatures[Segments.PSqlQualifiedName ()] = new CalleeSignature
			{
				NameSegments = Segments.ToList (),
				ReturnType = Resolved?.Display
			};

			// A null resolution has two causes, and they are told apart here, where the
			// database's own answer is still available. Left uncollected they reach the
			// diagnostic as one state, and it then has to guess which advice to give.
			if (Resolved == null)
			{
				string UnmappedType = DatabaseContext.GetUnmappedFunctionReturnType (Segments);
				if (UnmappedType != null)
				{
					_UnmappedReturnTypes[Segments.PSqlQualifiedName ()] = UnmappedType;
				}
			}

			// A name that resolves to nothing carries no type, and travels on as one.
			// Resolution failing is not by itself a reason to drop the procedure: most calls
			// sit where nothing ever asks what they return -- a predicate, a discarded
			// argument -- and those procedures are analysable in full. It is where the
			// unknown type would become a result column that it has to be refused, so the
			// wrapper never carries a guessed type, and the names above are what that
			// refusal names.
			return new NamedTyped (Name, Resolved.SourcedFunction (Span));
		}

		public DbTable GetTable (string[] NameSegments)
		{
			return GetSchemaEntity (TablesDict, NameSegments);
		}
	}
}
