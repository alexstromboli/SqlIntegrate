using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Newtonsoft.Json;

using Wrapper;
using DbAnalysis;
using DbAnalysis.Datasets;
using Utils.CodeGeneration;

namespace TestWrapper
{
	class AugType : SqlType
	{
		public string Tag;
	}

	class AugModule : GModule<AugType, Procedure, Column, Argument, ResultSet>
	{
	}

	class AugCodeProcessor : GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>
	{
	}

	class ChangeNameCodeProcessor : AugCodeProcessor
	{
		public override void OnHaveWrapper (Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database)
		{
			base.OnHaveWrapper (Database);
			Database.CsNamespace = "FirstSolution";
			Database.CsClassName = "Proxy";
		}
	}

	// The tracked wrapper is compiled beside the untracked one, so it needs a namespace
	// of its own. The class name stays DbProc -- the schema classes name it literally.
	class TrackedNamespaceCodeProcessor : AugCodeProcessor
	{
		public override void OnHaveWrapper (Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database)
		{
			base.OnHaveWrapper (Database);
			Database.CsNamespace = "GeneratedTracked";
		}
	}

	class TaggerCodeProcessor : AugCodeProcessor
	{
		public override void OnHaveTypeMap (SqlTypeMap DbTypeMap, Dictionary<string, TypeMapping<AugType, Column>> TypeMap)
		{
			foreach (var t in TypeMap)
			{
				if (t.Value.ReportedType?.Tag != null)
				{
					var Prev = t.Value.GetValue;
					t.Value.GetValue = v => $"{Prev (v)} /* {t.Value.ReportedType.Tag} */";
				}
			}
		}
	}

	class Program
	{
		const string LegacyNpgsqlFlag = "--legacy-npgsql";
		const string TrackerFlag = "--tracker=";

		static int Main (string[] args)
		{
			bool LegacyNpgsql = args.Any (a => a == LegacyNpgsqlFlag);
			string TrackerStateType = args
				.Where (a => a.StartsWith (TrackerFlag))
				.Select (a => a.Substring (TrackerFlag.Length))
				.LastOrDefault ();
			string[] PositionalArgs = args
				.Where (a => a != LegacyNpgsqlFlag && !a.StartsWith (TrackerFlag))
				.ToArray ();

			string ModuleInputPath = Path.GetFullPath (PositionalArgs[0]);
			string ModuleJson = File.ReadAllText (ModuleInputPath);
			AugModule Module = JsonConvert.DeserializeObject<AugModule> (ModuleJson);

			GeneratorOptions Options = new GeneratorOptions { LegacyNpgsql = LegacyNpgsql };

			var Runs = new List<(string Target, GeneratorOptions Options, GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[] Processors)>
			{
				("dbproc.cs", Options, new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[] { new ChangeNameCodeProcessor () }),
				("dbproc_sch_noda.cs", Options, new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[]
				{
					new GNodaTimeCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule> (),
					new TaggerCodeProcessor (),
					new EncryptionCodeProcessor ()
				})
			};

			// Only on request: a tracked wrapper has to name the state type its tracker hands
			// itself, and without the flag there is nothing to name it with. The chain is the
			// one dbproc_sch_noda.cs uses, so tracking is exercised alongside the value
			// rewriting the processors do rather than on a bare wrapper.
			if (TrackerStateType != null)
			{
				Runs.Add (("dbproc_tracked.cs",
					new GeneratorOptions { LegacyNpgsql = LegacyNpgsql, TrackerStateType = TrackerStateType },
					new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[]
					{
						new GNodaTimeCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule> (),
						new TaggerCodeProcessor (),
						new EncryptionCodeProcessor (),
						new TrackedNamespaceCodeProcessor ()
					}));
			}

			//
			// A type the generator cannot describe is something to act on, not a crash to
			// read a stack trace out of. The message names the site and the type; a stack
			// trace through the LINQ that walked there names neither.
			try
			{
				foreach (var run in Runs)
				{
					string Code = Generator.GGenerateCode (Module, run.Options, run.Processors);
					CodeGenerationUtils.EnsureFileContents (run.Target, Code, EndOfLine.MakeLf, Encoding.UTF8);
				}
			}
			catch (UnmappedTypeException ex)
			{
				Console.Error.WriteLine ("Cannot generate code: " + ex.Message);
				return 1;
			}

			return 0;
		}
	}
}
