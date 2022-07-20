using System.Collections.Generic;

using Sprache;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public Sourced<string> CaseH { get; }
		public IEnumerable<Sourced<T>> Branches { get; }
		public Sourced<SPolynom> ElseC { get; }		// can be null

		public CaseBase (ITextSpan<string> CaseH, IEnumerable<ITextSpan<T>> Branches, IOption<Sourced<SPolynom>> ElseC)
		{
			this.CaseH = CaseH.ToSourced ();
			this.Branches = Branches.ToSourced ();
			this.ElseC = ElseC.GetOrDefault ();
		}
	}
}
