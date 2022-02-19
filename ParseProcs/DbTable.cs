using System.Linq;
using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITable
	{
		public IReadOnlyList<NamedTyped> Columns { get; }
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict  { get; }
	}

	public abstract class BasicTable : ITable
	{
		public abstract IReadOnlyList<NamedTyped> Columns { get; }

		protected Dictionary<string, NamedTyped> _ColumnsDict;
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict
		{
			get
			{
				if (_ColumnsDict == null)
				{
					_ColumnsDict = Columns.ToDictionary (c => c.Name);
				}

				return _ColumnsDict;
			}
		}
	}

	public class Table : BasicTable
	{
		protected List<NamedTyped> _Columns;
		public override IReadOnlyList<NamedTyped> Columns => _Columns;

		public Table ()
		{
			_Columns = new List<NamedTyped> ();
			_ColumnsDict = new Dictionary<string, NamedTyped> ();
		}

		public NamedTyped AddColumn (NamedTyped ColumnL)
		{
			if (_ColumnsDict.TryGetValue (ColumnL.Name, out NamedTyped Existing))
			{
				return Existing;
			}

			_Columns.Add (ColumnL);
			_ColumnsDict[ColumnL.Name] = ColumnL;

			return ColumnL;
		}
	}

	public class DbTable : SchemaEntity, ITable
	{
		protected Table ColumnsHolder;

		public DbTable (string Schema, string Name)
			: base (Schema, Name)
		{
			ColumnsHolder = new Table ();
		}

		public IReadOnlyList<NamedTyped> Columns => ((ITable)ColumnsHolder).Columns;
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict => ((ITable)ColumnsHolder).ColumnsDict;

		public NamedTyped AddColumn (NamedTyped ColumnL)
		{
			return ColumnsHolder.AddColumn (ColumnL);
		}
	}
}
