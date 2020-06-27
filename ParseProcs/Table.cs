using System.Collections.Generic;

namespace ParseProcs
{
	public class Table : SchemaEntity
	{
		protected List<Column> _Columns;
		public IReadOnlyList<Column> Columns => _Columns;

		protected Dictionary<string, Column> _ColumnsDict;
		public IReadOnlyDictionary<string, Column> ColumnsDict => _ColumnsDict;

		public Table (string Schema, string Name)
			: base (Schema, Name)
		{
			_Columns = new List<Column> ();
			_ColumnsDict = new Dictionary<string, Column> ();
		}

		public Column AddColumn (Column Column)
		{
			if (_ColumnsDict.TryGetValue (Column.Name, out Column Existing))
			{
				return Existing;
			}
			
			_Columns.Add (Column);
			_ColumnsDict[Column.Name] = Column;

			return Column;
		}
	}
}