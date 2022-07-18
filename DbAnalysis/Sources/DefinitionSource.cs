namespace DbAnalysis.Sources
{
	public class DefinitionSource : ISource
	{
		public static DefinitionSource Instance = new DefinitionSource ();

		protected DefinitionSource ()
		{
		}

		public override string ToString ()
		{
			return "by definition";
		}
	}
}
