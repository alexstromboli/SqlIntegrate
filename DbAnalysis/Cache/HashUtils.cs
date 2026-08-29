using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace DbAnalysis.Cache
{
	public static class HashUtils
	{
		public static string ComputeSha1 (string input)
		{
			using (SHA1 sha1 = SHA1.Create ())
			{
				byte[] bytes = Encoding.UTF8.GetBytes (input);
				byte[] hash = sha1.ComputeHash (bytes);
				return string.Concat (hash.Select (b => b.ToString ("x2")));
			}
		}

		public static string ComputeDatabaseDataLayoutHash (DatabaseContext DatabaseContext)
		{
			StringBuilder sb = new StringBuilder ();

			// Types: Schema, Name, Properties, EnumValues
			foreach (var type in DatabaseContext.TypeMap.Map.Values
				         .Where (t => t.IsCustom)
				         .OrderBy (t => t.Schema)
				         .ThenBy (t => t.OwnName))
			{
				sb.Append ("T:");
				sb.Append (type.Schema);
				sb.Append (".");
				sb.Append (type.OwnName);
				if (type.EnumValues != null)
				{
					sb.Append (":E:");
					sb.Append (string.Join (",", type.EnumValues));
				}
				if (type.Properties != null)
				{
					sb.Append (":P:");
					sb.Append (string.Join (",", type.Properties.Select (p => p.Name + ":" + p.Type.Display)));
				}
				sb.Append (";");
			}

			// Tables and their columns
			foreach (var table in DatabaseContext.TablesDict.Values.OrderBy (t => t.Display))
			{
				sb.Append ("TBL:");
				sb.Append (table.Display);
				sb.Append (":");
				sb.Append (string.Join (",", table.Columns.Select (c => c.Name.Value + ":" + c.Type.Value.Display)));
				sb.Append (";");
			}

			return ComputeSha1 (sb.ToString ());
		}

		public static string ComputeProcedureHash (Procedure proc)
		{
			StringBuilder sb = new StringBuilder ();

			// Include procedure name
			sb.Append (proc.Schema);
			sb.Append (".");
			sb.Append (proc.Name);
			sb.Append (";");

			// Include arguments with names and types
			foreach (var arg in proc.Arguments)
			{
				sb.Append ("A:");
				sb.Append (arg.Name.Value);
				sb.Append (":");
				sb.Append (arg.Type.Value.Display);
				sb.Append (";");
			}

			// Include source code
			sb.Append ("SRC:");
			sb.Append (proc.SourceCode);

			return ComputeSha1 (sb.ToString ());
		}

		// The grammar is an input to the analysis, so it belongs in the cache key:
		// an entry produced by a different analyzer describes a procedure that may now
		// infer different types, and serving it back would be a silently wrong wrapper.
		// The module version id is content-derived under the SDK's deterministic build,
		// so unchanged sources keep their entries while any edit to the analyzer
		// discards them. An assembly version would not: it is a hardcoded constant, and
		// even a git-derived informational version is frozen while a change is being
		// iterated on but not yet committed -- exactly when the cache must not be trusted.
		public static readonly string AnalyzerHash =
			typeof (HashUtils).Assembly.ManifestModule.ModuleVersionId.ToString ("N");

		public static string ComputeProcKey (string AnalyzerHash, string DatabaseDataLayoutHash,
			string ProcedureHash)
		{
			return AnalyzerHash + "_" + DatabaseDataLayoutHash + "_" + ProcedureHash;
		}
	}
}
