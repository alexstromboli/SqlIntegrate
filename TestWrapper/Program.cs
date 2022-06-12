using System.IO;
using Newtonsoft.Json;

using Wrapper;
using DbAnalysis.Datasets;
using Utils.CodeGeneration;

namespace TestWrapper
{
	class ChangeNameCodeProcessor : CodeProcessor
	{
		public override void OnHaveWrapper (Database Database)
		{
			base.OnHaveWrapper (Database);
			Database.CsNamespace = "FirstSolution";
			Database.CsClassName = "Proxy";
		}
	}

	class Program
	{
		static void Main (string[] args)
		{
			string ModuleInputPath = Path.GetFullPath (args[0]);
			string ModuleJson = File.ReadAllText (ModuleInputPath);
			Module Module = JsonConvert.DeserializeObject<Module> (ModuleJson);

			//
			foreach (var run in new[]
			         {
				         new { target = "dbproc.cs", processors = new[] { (CodeProcessor)new ChangeNameCodeProcessor () } },
				         new { target = "dbproc_sch_noda.cs", processors = new[] { (CodeProcessor)new NodaTimeCodeProcessor () } }
			         }
			        )
			{
				string Code = Generator.GenerateCode (Module, run.processors);
				CodeGenerationUtils.EnsureFileContents (run.target, Code);
			}
		}
	}
}
