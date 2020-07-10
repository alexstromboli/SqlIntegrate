using System.Collections.Generic;

namespace ParseProcs
{
	public class Table : SchemaEntity
	{
		protected List<NamedTyped> _Columns;
		public IReadOnlyList<NamedTyped> Columns => _Columns;

		protected Dictionary<string, NamedTyped> _ColumnsDict;
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict => _ColumnsDict;

		public Table (string Schema, string Name)
			: base (Schema, Name)
		{
			_Columns = new List<NamedTyped> ();
			_ColumnsDict = new Dictionary<string, NamedTyped> ();
		}

		public NamedTyped AddColumn (NamedTyped Column)
		{
			if (_ColumnsDict.TryGetValue (Column.Name, out NamedTyped Existing))
			{
				return Existing;
			}
			
			_Columns.Add (Column);
			_ColumnsDict[Column.Name] = Column;

			return Column;
		}
	}
}
