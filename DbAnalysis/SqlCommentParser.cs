using Sprache;

namespace DbAnalysis
{
	public class SqlCommentParser : CommentParser
	{
		protected SqlCommentParser ()
		{
			this.Single = "--";
		}

		public static readonly SqlCommentParser Instance = new SqlCommentParser ();
	}
}
