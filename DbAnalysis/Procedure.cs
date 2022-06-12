using System.Collections.Generic;

namespace DbAnalysis
{
	public class Procedure : SchemaEntity
	{
		public uint Oid { get; }
		public string SourceCode { get; }

		protected List<Argument> _Arguments;
		public IReadOnlyList<Argument> Arguments => _Arguments;

		protected Dictionary<string, Argument> _ArgumentsDict;
		public IReadOnlyDictionary<string, Argument> ArgumentsDict => _ArgumentsDict;

		public Procedure (string Schema, string Name, uint Oid, string SourceCode)
			: base (Schema, Name)
		{
			this.Oid = Oid;
			this.SourceCode = SourceCode;
			_Arguments = new List<Argument> ();
			_ArgumentsDict = new Dictionary<string, Argument> ();
		}

		public Argument AddArgument (Argument Argument)
		{
			if (_ArgumentsDict.TryGetValue (Argument.Name, out Argument Existing))
			{
				return Existing;
			}

			_Arguments.Add (Argument);
			_ArgumentsDict[Argument.Name] = Argument;

			return Argument;
		}
	}
}
