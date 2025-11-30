namespace DbAnalysis.Cache
{
	public class VoidCache : IProcedureStateCache
	{
		public bool TryGet (string ProcKey, out Datasets.Procedure ProcedureReport)
		{
			ProcedureReport = null;
			return false;
		}

		public void Store (string ProcKey, Datasets.Procedure ProcedureReport)
		{
			// Do nothing
		}
	}
}
