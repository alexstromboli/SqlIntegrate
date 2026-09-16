namespace Wrapper
{
	public class GeneratorOptions
	{
		public bool LegacyNpgsql = false;

		// Fully qualified C# name of the state type a tracker hands itself per call,
		// the T of IDbProcTracker<T>, e.g. "MyApp.CallMetrics". Null leaves tracking
		// out entirely and the generated text unchanged.
		public string TrackerStateType = null;
	}
}
