using System.IO;
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
		static void Main (string[] args)
		{
			string ModuleInputPath = Path.GetFullPath (args[0]);
			string ModuleJson = File.ReadAllText (ModuleInputPath);
			AugModule Module = JsonConvert.DeserializeObject<AugModule> (ModuleJson);

			//
			foreach (var run in new[]
			         {
				         new { target = "dbproc.cs", processors = new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[] { new ChangeNameCodeProcessor () } },
				         new { target = "dbproc_sch_noda.cs", processors = new GCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule>[]
				         {
					         new GNodaTimeCodeProcessor<AugType, Procedure, Column, Argument, ResultSet, AugModule> (),
					         new TaggerCodeProcessor (),
					         new EncryptionCodeProcessor ()
				         } }
			         }
			        )
			{
				string Code = Generator.GGenerateCode (Module, run.processors);
				CodeGenerationUtils.EnsureFileContents (run.target, Code, EndOfLine.MakeLf, Encoding.UTF8);
			}
		}
	}
}
