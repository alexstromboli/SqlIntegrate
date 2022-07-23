using System.Collections.Generic;

using Sprache;

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class CaseBase<T>
	{
		public Sourced<string> CaseH { get; }
		public Sourced<NamedTyped> Sample { get; }
		public Sourced<NamedTyped>[] Conditions { get; }
		public Sourced<T>[] Branches { get; }
		public Sourced<NamedTyped> ElseC { get; }		// can be null

		public CaseBase (
			Sourced<string> CaseH,
			Sourced<NamedTyped> Sample,
			Sourced<NamedTyped>[] Conditions,
			Sourced<T>[] Branches,
			Sourced<NamedTyped> ElseC
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
