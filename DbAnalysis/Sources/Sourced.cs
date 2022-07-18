using System;

namespace DbAnalysis.Sources
{
	public class Sourced<T>
	{
		public T Value { get; }
		public ISource Source { get; }
		public TextSpan TextSpan => (Source as TextSpanSource)?.Span;

		public Sourced (T Value, ISource Source)
		{
			this.Value = Value;
			this.Source = Source;
		}

		public override string ToString ()
		{
			return (Value?.ToString () ?? "null") + ", source: " + Source?.ToString ();
		}

		public Sourced<N> Select<N> (Func<T, N> Convert)
		{
			return new Sourced<N> (Convert (Value), Source);
		}
	}
}
