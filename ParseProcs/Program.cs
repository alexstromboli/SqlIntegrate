using System.IO;

using Newtonsoft.Json;

using DbAnalysis;
using DbAnalysis.Datasets;

namespace ParseProcs
{
	partial class Program
	{
		static void Main (string[] args)
		{
			string ConnectionString = args[0];
			string OutputFileName = args[1];

			//
			var DatabaseContext = ReadDatabase.LoadContext (ConnectionString);
			Analyzer Analyzer = new Analyzer (DatabaseContext);
			Module ModuleReport = Analyzer.Run ();

			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));
		}
	}
}
