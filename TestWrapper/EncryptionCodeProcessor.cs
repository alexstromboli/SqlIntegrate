using System.Collections.Generic;
using System.Text.RegularExpressions;

using Wrapper;
using DbAnalysis.Datasets;
using Utils.CodeGeneration;

namespace TestWrapper
{
	class EncryptionCodeProcessor : AugCodeProcessor
	{
		public const string TargetCsTypeName = "TryWrapper.Payer";
		
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
			sb.AppendLine (@"
public T ReadEncrypted<T> (object Input)
{
	if (Input == null || Input == DBNull.Value)
	{
		return default (T);
	}

	return JsonConvert.DeserializeObject<T> (Encoding.UTF8.GetString (Decryptor ((byte[])Input)));
}

public byte[] WriteEncrypted<T> (T Input)
{
	if (Input == null)
	{
		return null;
	}

	return Encryptor (Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (Input)));
}");
		}

		protected static bool NameMatches (string ArgumentName)
		{
			return Regex.IsMatch (ArgumentName, @"^(p_)?enc_pi_");
		}

		public override void OnEncodingParameter (
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema Schema,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure Procedure,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentCsType)
		{
			if (NameMatches (Argument.NativeName))
			{
				ArgumentCsType = TargetCsTypeName;
			}
		}

		public override void OnPassingParameter (
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema Schema,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure Procedure,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentValue)
		{
			if (NameMatches (Argument.NativeName))
			{
				ArgumentValue = $"DbProc.WriteEncrypted ({ArgumentValue})";
			}
		}

		public override void OnReadingParameter (
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema Schema,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure Procedure,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Argument Argument,
			ref string ArgumentValue)
		{
			if (NameMatches (Argument.NativeName))
			{
				ArgumentValue = $"DbProc.ReadEncrypted<{TargetCsTypeName}> ({ArgumentValue})";
			}
		}

		public override void OnEncodingResultSetColumn (
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema Schema,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure Procedure,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Set ResultSet,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Set.Property Property,
			ref string ColumnCsType)
		{
			if (NameMatches (Property.NativeName))
			{
				ColumnCsType = TargetCsTypeName;
			}
		}

		public override void OnReadingResultSetColumn (
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule> Database,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema Schema,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure Procedure,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Set ResultSet,
			Database<AugType, Procedure, Column, Argument, ResultSet, AugModule>.Schema.Procedure.Set.Property Property,
			ref string ColumnValue)
		{
			if (NameMatches (Property.NativeName))
			{
				ColumnValue = $"DbProc.ReadEncrypted<{TargetCsTypeName}> ({ColumnValue})";
			}
		}
	}
}
