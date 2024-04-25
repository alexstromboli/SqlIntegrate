namespace DbAnalysis
{
	public class DataReturnStatement
	{
		public static readonly DataReturnStatement Void = null;
		public static readonly SStatement VoidStatement = null;

		public OpenDataset Open { get; }
		public FullSelectStatement FullSelect { get; }

		public DataReturnStatement (OpenDataset Open, FullSelectStatement FullSelect)
		{
			this.Open = Open;
			this.FullSelect = FullSelect;
		}

		public NamedDataReturn GetResult (RequestContext rc)
		{
			var Result = new NamedDataReturn
			{
				Name = Open.Name,
				Comments = Open.Comments,
				Table = FullSelect.GetTable (rc)
			};

			return Result;
		}

		public SStatement ToStatement ()
		{
			return new SStatement (this, rc => rc);
		}
	}
}
