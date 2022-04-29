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
			/*
			keys:
			- column name
			- (table name or alias).(column name)
			- (schema name).(table name).(column name)
			*/
			public Dictionary<string, NamedTyped> Columns;

			/*
			keys:
			- *
			- (table name or alias).*
			- (schema name).(table name).*
			*/
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
			Dictionary<string, NamedTyped> AvailableColumns = new Dictionary<string, NamedTyped> (ColumnsDict);
			Dictionary<string, NamedTyped[]> Asterisks = new Dictionary<string, NamedTyped[]> ();

			var ColumnsArray = Columns.ToArray ();
			Asterisks["*"] = ColumnsArray;

			if (Alias != null)
			{
				foreach (var c in Columns)
				{
					AvailableColumns[Alias + "." + c.Name] = c;
				}
				Asterisks[Alias + ".*"] = ColumnsArray;
			}

			return new ITable.ColumnReferences
			{
				Columns = AvailableColumns,
				Asterisks = Asterisks
			};
		}
	}

	public class Table : BasicTable
	{
		protected List<NamedTyped> _Columns;
		public override IReadOnlyList<NamedTyped> Columns => _Columns;
		public string Name { get; }

		public Table (string Name = null)
		{
			_Columns = new List<NamedTyped> ();
			_ColumnsDict = new Dictionary<string, NamedTyped> ();
			this.Name = Name;
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

		public override ITable.ColumnReferences GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
		{
			return base.GetAllColumnReferences (ModuleContext, Alias ?? Name);
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

			var AvailableColumns = BaseResult.Columns;
			Dictionary<string, NamedTyped[]> Asterisks = BaseResult.Asterisks;

			bool CanMissSchema = !ModuleContext.SchemaOrder.TakeWhile (s => s != Schema).Any (s => ModuleContext.TablesDict.ContainsKey (s + "." + Name));

			if (CanMissSchema)
			{
				foreach (var c in Columns)
				{
					AvailableColumns[Name + "." + c.Name] = c;
				}
				Asterisks[Name + ".*"] = Asterisks["*"];
			}

			foreach (var c in Columns)
			{
				AvailableColumns[Schema + "." + Name + "." + c.Name] = c;
				AvailableColumns[ModuleContext.DatabaseContext.DatabaseName + "." + Schema + "." + Name + "." + c.Name] = c;
			}
			Asterisks[Schema + "." + Name + ".*"] = Asterisks["*"];
			Asterisks[ModuleContext.DatabaseContext.DatabaseName + "." + Schema + "." + Name + ".*"] = Asterisks["*"];

			return new ITable.ColumnReferences
			{
				Columns = AvailableColumns,
				Asterisks = Asterisks
			};
		}

		public NamedTyped AddColumn (NamedTyped ColumnL)
		{
			return ColumnsHolder.AddColumn (ColumnL);
		}
	}
}
