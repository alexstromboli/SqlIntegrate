using System;

// involvement of Sprache can be removed through decomposition of Sprache types in Sourced
using Sprache;

namespace DbAnalysis
{
	public enum SourceType
	{
		Unknown,
		TextSpan,
		Argument,
		TableColumn,
		EnumValue,
		Calculated
	}

	public class Sourced<T>
	{
		public SourceType SourceType { get; } = SourceType.Unknown;
		// text span
		public TextSpan TextSpan { get; } = null;
		// table
		public string SchemaName { get; } = null;
		public string TableName { get; } = null;
		public string TableColumnName { get; } = null;

		public T Value { get; }

		protected Sourced (T Value, SourceType SourceType, TextSpan TextSpan, string SchemaName, string TableName, string TableColumnName)
		{
			this.Value = Value;
			this.SourceType = SourceType;
			this.TextSpan = TextSpan;
			this.SchemaName = SchemaName;
			this.TableName = TableName;
			this.TableColumnName = TableColumnName;
		}

		public Sourced (ITextSpan<T> Span)
		{
			this.Value = Span.Value;
			this.SourceType = SourceType.TextSpan;
			this.TextSpan = (Span as TextSpan) ?? new TextSpan { Start = Span.Start, End = Span.End, Length = Span.Length };
		}

		public Sourced (T Value)
		{
			this.Value = Value;
			this.SourceType = SourceType.Unknown;
		}

		public static Sourced<T> Calculated (T Value, TextSpan Span)
		{
			if (Span == null)
			{
				return new Sourced<T> (Value, SourceType.Unknown, null, null, null, null);
			}

			return new Sourced<T> (Value, SourceType.Calculated, Span, null, null, null);
		}

		public static Sourced<T> FromTable<T> (T Value, string SchemaName, string TableName, string TableColumnName)
		{
			return new Sourced<T> (Value, SourceType.TableColumn, SchemaName, TableName, TableColumnName);
		}

		public Sourced<N> Select<N> (Func<T, N> Convert)
		{
			return new Sourced<N> (Convert (Value), SourceType, TextSpan, SchemaName, TableName, TableColumnName);
		}
	}

	public class Sourced
	{
		public static Sourced<T> Calculated<T> (T Value, TextSpan Span)
		{
			if (Span == null)
			{
				return new Sourced<T> (Value, SourceType.Unknown, null, null, null, null);
			}

			return new Sourced<T> (Value, SourceType.Calculated, Span, null, null, null);
		}

		public static Sourced<T> FromTable<T> (T Value, string SchemaName, string TableName, string TableColumnName)
		{
			return new Sourced<T> (Value, SourceType.TableColumn, SchemaName, TableName, TableColumnName);
		}
	}

	public class NamedTyped
	{
		// can be null
		public virtual Sourced<string> Name { get; protected set; }
		public virtual Sourced<PSqlType> Type { get; protected set; }
		public override string ToString () => $"{Name?.Value ?? "???"} {Type?.Value?.Display ?? "???"}";

		public NamedTyped (Sourced<string> Name, Sourced<PSqlType> Type)
		{
			this.Name = Name?.Select (s => s.ToLower ());
			this.Type = Type;
		}

		public NamedTyped (Sourced<PSqlType> Type)
			: this (null, Type)
		{
		}

		public NamedTyped WithName (Sourced<string> NewName)
		{
			return new NamedTyped (NewName, Type);
		}

		public NamedTyped WithType (Sourced<PSqlType> NewType)
		{
			return new NamedTyped (Name, NewType);
		}

		public NamedTyped ToArray ()	// here: trace back the source
		{
			return new NamedTyped (Name, Type.Select (t => t.ArrayType));
		}
	}
}
