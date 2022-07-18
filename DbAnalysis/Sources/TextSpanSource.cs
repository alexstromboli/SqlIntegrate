namespace DbAnalysis.Sources
{
	public class TextSpanSource : ISource
	{
		public TextSpan Span { get; } = null;

		public TextSpanSource (TextSpan Span)
		{
			this.Span = Span;
		}

		public override string ToString ()
		{
			return Span?.ToString () ?? "???";
		}
	}
}
