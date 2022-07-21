using System.Collections.Generic;

using Sprache;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public Sourced<string> CaseH { get; }
		public Sourced<T>[] Branches { get; }
		public Sourced<SPolynom> ElseC { get; }		// can be null

		public CaseBase (Sourced<string> CaseH, Sourced<T>[] Branches, IOption<Sourced<SPolynom>> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC.GetOrDefault ();
		}
	}
}
