using System.Collections.Generic;

using Sprache;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public Sourced<string> CaseH { get; }
		public NamedTyped Sample { get; }
		public NamedTyped[] Conditions { get; }
		public Sourced<T>[] Branches { get; }
		public NamedTyped ElseC { get; }		// can be null

		public CaseBase (
			Sourced<string> CaseH,
			NamedTyped Sample,
			NamedTyped[] Conditions,
			Sourced<T>[] Branches,
			NamedTyped ElseC
			)
		{
			this.CaseH = CaseH;
			this.Sample = Sample;
			this.Conditions = Conditions;
			this.Branches = Branches;
			this.ElseC = ElseC;
		}
	}
}
