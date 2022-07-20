using System.Collections.Generic;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class QualifiedName
	{
		protected Sourced<string>[] Fragments;

		public QualifiedName (Sourced<string>[] Fragments)
		{
			this.Fragments = Fragments;
		}

		public IReadOnlyList<Sourced<string>> Get (RequestContext Context, int NormalDepth = 3)
		{
			return Fragments;
		}
	}
}
