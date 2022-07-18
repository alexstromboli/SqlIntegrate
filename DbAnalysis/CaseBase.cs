using System.Collections.Generic;

using Sprache;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public ITextSpan<string> CaseH { get; }
		public IEnumerable<ITextSpan<T>> Branches { get; }
		public IOption<ITextSpan<SPolynom>> ElseC { get; }

		public CaseBase (ITextSpan<string> CaseH, IEnumerable<ITextSpan<T>> Branches, IOption<ITextSpan<SPolynom>> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}
}
