using System.IO;
using System.Linq;

using Newtonsoft.Json;

using DbAnalysis;
using DbAnalysis.Cache;
using DbAnalysis.Datasets;

namespace ParseProcs
{
	public class Program
	{
		static void Main (string[] args)
		{
			bool NoCache = args.Any (a => a == "--no-cache");
			string[] PositionalArgs = args.Where (a => !a.StartsWith ("--")).ToArray ();

			string ConnectionString = PositionalArgs[0];
			string OutputFileName = PositionalArgs[1];

			//
			var DatabaseContext = ReadDatabase.LoadContext (ConnectionString);

			// Calculate database data layout hash and create cache
			string DatabaseDataLayoutHash = HashUtils.ComputeDatabaseDataLayoutHash (DatabaseContext);
			IProcedureStateCache Cache = NoCache
				? new VoidCache ()
				: new LocalUserCache ();

			Analyzer Analyzer = new Analyzer (DatabaseContext);
			Module ModuleReport = Analyzer.Run (Cache, DatabaseDataLayoutHash);

			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));
		}
	}
}
