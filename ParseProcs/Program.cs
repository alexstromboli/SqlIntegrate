using System.IO;

using Newtonsoft.Json;

using ParseProcs.Datasets;

namespace ParseProcs
{
	partial class Program
	{
		static void Main (string[] args)
		{
			string ConnectionString = args[0];
			string OutputFileName = args[1];

			//
			var DatabaseContext = ReadDatabase (ConnectionString);
			Analyzer Analyzer = new Analyzer (DatabaseContext);
			Module ModuleReport = Analyzer.Run ();

			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));
		}
	}
}
