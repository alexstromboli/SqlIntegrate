namespace DbAnalysis.Cache
{
	public interface IProcedureStateCache
	{
		bool TryGet (string ProcKey, out Datasets.Procedure ProcedureReport);
		void Store (string ProcKey, Datasets.Procedure ProcedureReport);
	}
}
