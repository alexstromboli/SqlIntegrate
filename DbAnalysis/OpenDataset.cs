namespace ParseProcs
{
	public class OpenDataset
	{
		public string Name { get; }
		public string[] Comments { get; }

		public OpenDataset (string Name, string[] Comments)
		{
			this.Name = Name;
			this.Comments = Comments;
		}
	}
}
