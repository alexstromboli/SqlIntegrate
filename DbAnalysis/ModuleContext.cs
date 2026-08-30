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

			Sourced<PSqlType> Type = Resolved.SourcedFunction (Span)
			                ?? DatabaseContext.TypeMap.Null.SourcedTextSpan (Span);

			return new NamedTyped (Name, Type);
		}

		public DbTable GetTable (string[] NameSegments)
		{
			return GetSchemaEntity (TablesDict, NameSegments);
		}
	}
}
