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

		public IReadOnlyList<Sourced<string>> Get (RequestContext Context, int NormalDepth)
		{
			if (Fragments.Length > NormalDepth)
			{
				Context.ReportWarning ("Used database reference at " + (Fragments[..^NormalDepth].Range ().ToString () ?? "???"));
			}

			return Fragments;
		}
	}
}
