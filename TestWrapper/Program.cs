using System.IO;
using Newtonsoft.Json;

using Wrapper;
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

	class GChangeNameCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> : GCodeProcessor<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule>
		where TColumn : Column, new()
		where TArgument : Argument, new()
		where TResultSet : GResultSet<TColumn>, new()
		where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
		where TSqlType : GSqlType<TColumn>, new()
		where TModule : GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
	{
		public override void OnHaveWrapper (Database<TSqlType, TProcedure, TColumn, TArgument, TResultSet, TModule> Database)
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
			AugModule Module = JsonConvert.DeserializeObject<AugModule> (ModuleJson);

			//
			foreach (var run in new[]
			         {
				         new { target = "dbproc.cs", processors = new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[] { new GChangeNameCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule> () } },
				         new { target = "dbproc_sch_noda.cs", processors = new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[] { new GNodaTimeCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule> () } }
			         }
			        )
			{
				string Code = Generator.GGenerateCode (Module, run.processors);
				CodeGenerationUtils.EnsureFileContents (run.target, Code);
			}
		}
	}
}
