namespace DbAnalysis.Cache
{
	public class VoidCache : IProcedureStateCache
	{
		public bool TryGet (string ProcKey, out CachedAnalysis Analysis)
		{
			Analysis = null;
			return false;
		}

		public void Store (string ProcKey, CachedAnalysis Analysis)
		{
			// Do nothing
		}
	}
}
