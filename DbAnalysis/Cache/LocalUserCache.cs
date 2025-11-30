using System;
using System.IO;

using Newtonsoft.Json;

namespace DbAnalysis.Cache
{
	public class LocalUserCache : IProcedureStateCache
	{
		protected string CacheDirectory;

		public LocalUserCache ()
		{
			string HomeDir = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
			CacheDirectory = Path.Combine (HomeDir, ".sqlintegrate", "cache");

			if (!Directory.Exists (CacheDirectory))
			{
				Directory.CreateDirectory (CacheDirectory);
			}

			CleanOldFiles ();
		}

		protected void CleanOldFiles ()
		{
			DateTime Cutoff = DateTime.UtcNow.AddDays (-30);

			foreach (string file in Directory.GetFiles (CacheDirectory))
			{
				try
				{
					FileInfo fi = new FileInfo (file);
					if (fi.LastWriteTimeUtc < Cutoff)
					{
						fi.Delete ();
					}
				}
				catch
				{
					// Ignore file access errors during cleanup
				}
			}
		}

		protected string GetFilePath (string ProcKey)
		{
			return Path.Combine (CacheDirectory, ProcKey + ".json");
		}

		public bool TryGet (string ProcKey, out Datasets.Procedure ProcedureReport)
		{
			string FilePath = GetFilePath (ProcKey);

			if (!File.Exists (FilePath))
			{
				ProcedureReport = null;
				return false;
			}

			try
			{
				string json = File.ReadAllText (FilePath);
				ProcedureReport = JsonConvert.DeserializeObject<Datasets.Procedure> (json);

				// Update file modification time to track usage
				File.SetLastWriteTimeUtc (FilePath, DateTime.UtcNow);

				return true;
			}
			catch
			{
				ProcedureReport = null;
				return false;
			}
		}

		public void Store (string ProcKey, Datasets.Procedure ProcedureReport)
		{
			string FilePath = GetFilePath (ProcKey);

			try
			{
				string json = JsonConvert.SerializeObject (ProcedureReport, Formatting.None);
				File.WriteAllText (FilePath, json);
			}
			catch
			{
				// Ignore write errors
			}
		}
	}
}
