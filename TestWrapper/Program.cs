using System.IO;
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

	class EncryptionCodeProcessor : AugCodeProcessor
	{
		public override void OnHaveWrapper (Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database)
		{
			// here: check if not added yet
			Database.Usings.Add ("using System.Text;");
			Database.Usings.Add ("using Newtonsoft.Json;");
		}

		public override void OnCodeGenerationStarted (Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database, IndentedTextBuilder Builder, List<DbProcProperty> DbProcProperties)
		{
			DbProcProperties.Add (new DbProcProperty { Type = "Func<byte[], byte[]>", Name = "Encryptor" });
			DbProcProperties.Add (new DbProcProperty { Type = "Func<byte[], byte[]>", Name = "Decryptor" });
		}

		public override void OnCodeGeneratingDbProc (Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database, IndentedTextBuilder sb)
		{
			sb.AppendLine ();
			using (sb.UseCurlyBraces ("public T ReadEncrypted<T> (object Input)"))
			{
				using (sb.UseCurlyBraces ("if (Input == null || Input == DBNull.Value)"))
				{
					sb.AppendLine ("return default (T);");
				}
				
				sb.AppendLine ()
					.AppendLine ("return JsonConvert.DeserializeObject<T> (Encoding.UTF8.GetString (Decryptor ((byte[])Input)));");
			}

			sb.AppendLine ();
			using (sb.UseCurlyBraces ("public byte[] WriteEncrypted<T> (T Input)"))
			{
				using (sb.UseCurlyBraces ("if (Input == null)"))
				{
					sb.AppendLine ("return null;");
				}
				
				sb.AppendLine ()
					.AppendLine ("return Encryptor (Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (Input)));");
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
				CodeGenerationUtils.EnsureFileContents (run.target, Code);
			}
		}
	}
}
