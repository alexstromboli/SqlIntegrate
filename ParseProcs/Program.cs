using System;
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
		const string Usage =
			"Usage: ParseProcs [--no-cache] [--tolerate-failures] <connection string> <output json>";

		static int Main (string[] args)
		{
			bool NoCache = false;
			bool TolerateFailures = false;

			// An unrecognised flag is refused rather than ignored: a mistyped --no-cache
			// that is silently dropped hands back a cached analysis while reading as a
			// request for a fresh one.
			foreach (string arg in args.Where (a => a.StartsWith ("--")))
			{
				switch (arg)
				{
					case "--no-cache":
						NoCache = true;
						break;

					case "--tolerate-failures":
						TolerateFailures = true;
						break;

					default:
						Console.Error.WriteLine ($"Unknown option \"{arg}\".");
						Console.Error.WriteLine (Usage);
						return 2;
				}
			}

			string[] PositionalArgs = args.Where (a => !a.StartsWith ("--")).ToArray ();

			if (PositionalArgs.Length != 2)
			{
				Console.Error.WriteLine (
					$"Expected 2 arguments, got {PositionalArgs.Length}.");
				Console.Error.WriteLine (Usage);
				return 2;
			}

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

			// Written even when procedures were left out, so a partial report stays
			// available for inspection; the exit code is what says it is partial.
			File.WriteAllText (OutputFileName, JsonConvert.SerializeObject (ModuleReport, Formatting.Indented));

			if (Analyzer.Failures.Count == 0)
			{
				return 0;
			}

			// The per-procedure diagnostics are printed as they happen and scroll away in a
			// long run, so the tally is repeated here, at the end and on stderr, where a
			// `| tail` survives it. A procedure missing from the report generates no wrapper
			// method, which is a build that quietly lacks what the database offers.
			Console.Error.WriteLine (
				$"{Analyzer.Failures.Count} procedure(s) could not be analysed:");

			foreach (var Failure in Analyzer.Failures)
			{
				Console.Error.WriteLine ($"  {Failure.ProcDisplayName}: {Failure.Kind}");
			}

			if (TolerateFailures)
			{
				Console.Error.WriteLine (
					"Continuing anyway: --tolerate-failures was given.");
				return 0;
			}

			return 1;
		}
	}
}
