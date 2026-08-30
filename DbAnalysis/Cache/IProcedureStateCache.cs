namespace DbAnalysis.Cache
{
	public interface IProcedureStateCache
	{
		bool TryGet (string ProcKey, out CachedAnalysis Analysis);
		void Store (string ProcKey, CachedAnalysis Analysis);
	}
}
