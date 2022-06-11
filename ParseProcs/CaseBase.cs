using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
	public class CaseBase<T>
	{
		public string CaseH { get; }
		public IEnumerable<T> Branches { get; }
		public IOption<SPolynom> ElseC { get; }

		public CaseBase (string CaseH, IEnumerable<T> Branches, IOption<SPolynom> ElseC)
		{
			this.CaseH = CaseH;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}
}
