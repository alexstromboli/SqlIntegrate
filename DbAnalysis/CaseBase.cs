using System.Collections.Generic;

using Sprache;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public ITextSpan<string> CaseH { get; }
		public IEnumerable<T> Branches { get; }
		public IOption<SPolynom> ElseC { get; }

		public CaseBase (ITextSpan<string> CaseH, IEnumerable<T> Branches, IOption<SPolynom> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}
}
