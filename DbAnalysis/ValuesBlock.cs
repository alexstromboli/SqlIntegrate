using Utils;

namespace ParseProcs
{
	public class ValuesBlock : ITableRetriever
	{
		public SPolynom[] Values;
		public string TableName;
		public string[] ColumnNames;

		public ValuesBlock (SPolynom[] Values, string TableName, string[] ColumnNames)
		{
			this.Values = Values;
			this.TableName = TableName;
			this.ColumnNames = ColumnNames;
		}

		public ITable GetTable (RequestContext Context, bool OnlyNamed = true)
		{
			Table t = new Table (TableName);

			foreach (var ValueExp in Values.Indexed ())
			{
				int Pos = ValueExp.Index;
				string ColName = Pos < ColumnNames.Length ? ColumnNames[Pos] : null;

				if (ColName != null || !OnlyNamed)
				{
					t.AddColumn (ValueExp.Value.GetResult (Context).WithName (ColName));
				}
			}

			return t;
		}
	}
}
