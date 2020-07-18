using Sprache;

namespace ParseProcs
{
	public class Ref<T>
	{
		public Parser<T> Parser = null;

		public Parser<T> Get { get; }

		public Ref ()
		{
			Get = Parse.Ref (() => Parser);
		}
	}
}
