using System.Linq;
using System.Collections.Generic;

namespace ParseProcs
{
	public interface ITable
	{
		public IReadOnlyList<NamedTyped> Columns { get; }
		public IReadOnlyDictionary<string, NamedTyped> ColumnsDict  { get; }
		NamedTyped[] GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null);
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

		public virtual NamedTyped[] GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
		{
			List<NamedTyped> Result = new List<NamedTyped> (Columns);

			if (Alias != null)
			{
				Result.AddRange (Columns.Select (c => new NamedTyped (Alias + "." + c.Name, c.Type)));
			}

			return Result.ToArray ();
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

		public NamedTyped[] GetAllColumnReferences (ModuleContext ModuleContext, string Alias = null)
		{
			if (Alias != null)
			{
				return ColumnsHolder.GetAllColumnReferences (ModuleContext, Alias);
			}

			List<NamedTyped> Result = new List<NamedTyped> (ColumnsHolder.GetAllColumnReferences (ModuleContext, Alias));

			bool CanMissSchema = !ModuleContext.SchemaOrder.TakeWhile (s => s != Schema).Any (s => ModuleContext.TablesDict.ContainsKey (s + "." + Name));

			if (CanMissSchema)
			{
				Result.AddRange (Columns.Select (c => new NamedTyped (Name + "." + c.Name, c.Type)));
			}

			Result.AddRange (Columns.Select (c => new NamedTyped (Schema + "." + Name + "." + c.Name, c.Type)));

			return Result.ToArray ();
		}

		public NamedTyped AddColumn (NamedTyped ColumnL)
		{
			return ColumnsHolder.AddColumn (ColumnL);
		}
	}
}
