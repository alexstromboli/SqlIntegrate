using System;
using System.Collections.Generic;

using Sprache;

namespace DbAnalysis
{
	public class OrdinarySelect
	{
		public Func<RequestContext, IReadOnlyList<NamedTyped>> List { get; }
		public IOption<FromTableExpression[]> FromClause { get; }

		public OrdinarySelect (Func<RequestContext, IReadOnlyList<NamedTyped>> List, IOption<FromTableExpression[]> FromClause)
		{
			this.List = List;
			this.FromClause = FromClause;
		}
	}
}
