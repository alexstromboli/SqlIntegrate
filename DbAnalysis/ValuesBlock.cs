using Utils;
using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class ValuesBlock : ITableRetriever
	{
		public RcFunc<NamedTyped[]> Values;
		public Sourced<string> TableName;
		public Sourced<string>[] ColumnNames;

		public ValuesBlock (RcFunc<NamedTyped[]> Values, Sourced<string> TableName, Sourced<string>[] ColumnNames)
		{
			this.Values = Values;
			this.TableName = TableName;
			this.ColumnNames = ColumnNames;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			Table t = new Table (TableName);

			foreach (var ValueExp in Values (Context).Indexed ())
			{
				int Pos = ValueExp.Index;
				Sourced<string> ColName = Pos < ColumnNames.Length ? ColumnNames[Pos] : null;

				if (ColName != null || !OnlyNamed)
				{
					t.AddColumn (ValueExp.Value.WithName (ColName));
				}
			}

			return t;
		}
	}
}
