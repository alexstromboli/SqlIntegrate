using System.Linq;
using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITable
	{
		public IReadOnlyList<NamedTyped> Columns { get; }
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict  { get; }

		class ColumnReferences
		{
			// column name
			// (table name or alias).(column name)
			// (schema name).(table name).(column name)
			public NamedTyped[] Columns;

			// column name
			// (table name or alias).*
			// (schema name).(table name).*
			public Dictionary<string, NamedTyped[]> Asterisks;
		}

		ColumnReferences GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null);
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

		public virtual ITable.ColumnReferences GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
		{
			List<NamedTyped> Result = new List<NamedTyped> (Columns);
			Dictionary<string, NamedTyped[]> Asterisks = new Dictionary<string, NamedTyped[]> ();

			var ColumnsArray = Columns.ToArray ();
			Asterisks["*"] = ColumnsArray;

			if (Alias != null)
			{
				Result.AddRange (Columns.Select (c => new NamedTyped (Alias + "." + c.Name, c.Type)));
				Asterisks[Alias + ".*"] = ColumnsArray;
			}

			return new ITable.ColumnReferences
			{
				Columns = Result.ToArray (),
				Asterisks = Asterisks
			};
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
			if (ColumnL.Name != null && _ColumnsDict.TryGetValue (ColumnL.Name, out NamedTyped Existing))
			{
				return Existing;
			}

			_Columns.Add (ColumnL);

			if (ColumnL.Name != null)
			{
				_ColumnsDict[ColumnL.Name] = ColumnL;
			}

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

		public ITable.ColumnReferences GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
		{
			var BaseResult = ColumnsHolder.GetAllColumnReferences (ModuleContext, Alias);

			if (Alias != null)
			{
				return BaseResult;
			}

			List<NamedTyped> AllColumns = new List<NamedTyped> (BaseResult.Columns);
			Dictionary<string, NamedTyped[]> Asterisks = BaseResult.Asterisks;

			bool CanMissSchema = !ModuleContext.SchemaOrder.TakeWhile (s => s != Schema).Any (s => ModuleContext.TablesDict.ContainsKey (s + "." + Name));

			if (CanMissSchema)
			{
				AllColumns.AddRange (Columns.Select (c => new NamedTyped (Name + "." + c.Name, c.Type)));
				Asterisks[Name + ".*"] = Asterisks["*"];
			}

			AllColumns.AddRange (Columns.Select (c => new NamedTyped (Schema + "." + Name + "." + c.Name, c.Type)));
			Asterisks[Schema + "." + Name + ".*"] = Asterisks["*"];

			return new ITable.ColumnReferences
			{
				Columns = AllColumns.ToArray (),
				Asterisks = Asterisks
			};
		}

		public NamedTyped AddColumn (NamedTyped ColumnL)
		{
			return ColumnsHolder.AddColumn (ColumnL);
		}
	}
}
