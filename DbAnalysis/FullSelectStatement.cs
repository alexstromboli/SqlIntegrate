using System.Collections.Generic;

using Sprache;

namespace DbAnalysis
{
	public class FullSelectStatement : ITableRetriever
	{
		// can be null
		public IOption<IEnumerable<SelectStatement>> Cte { get; }
		public SelectStatement SelectBody { get; }

		public FullSelectStatement (IOption<IEnumerable<SelectStatement>> Cte, SelectStatement SelectBody)
		{
			this.Cte = Cte;
			this.SelectBody = SelectBody;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			RequestContext CurrentContext = Context;

			if (Cte != null && Cte.IsDefined)
			{
				var Levels = Cte.Get ();
				if (Levels != null)
				{
					foreach (var l in Levels)
					{
						ITable t = l.GetTable (CurrentContext, true);
						CurrentContext = new RequestContext (CurrentContext, new Dictionary<string, ITable> { [l.Name.Value] = t });
					}
				}
			}

			var Result = SelectBody.GetTable (CurrentContext, OnlyNamed);
			return Result;
		}
	}
}
