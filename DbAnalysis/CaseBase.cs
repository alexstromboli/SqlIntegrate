using System.Collections.Generic;

using Sprache;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public Sourced<string> CaseH { get; }
		public IEnumerable<Sourced<T>> Branches { get; }
		public IOption<Sourced<SPolynom>> ElseC { get; }

		public CaseBase (Sourced<string> CaseH, IEnumerable<Sourced<T>> Branches, IOption<Sourced<SPolynom>> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}
}
