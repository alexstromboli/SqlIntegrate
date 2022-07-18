using System;
using System.Linq;
using System.Collections.Generic;

using Sprache;

namespace DbAnalysis.Sources
{
	public interface ISource
	{
	}

	public class DefinitionSource : ISource
	{
		public static DefinitionSource Instance = new DefinitionSource ();

		protected DefinitionSource ()
		{
		}
	}

	public class TextSpanSource : ISource
	{
		public TextSpan Span { get; } = null;

		public TextSpanSource (TextSpan Span)
		{
			this.Span = Span;
		}
	}

	public class CalculatedSource : TextSpanSource
	{
		public CalculatedSource (TextSpan Span)
			: base (Span)
		{
		}
	}

	public class FunctionSource : TextSpanSource
	{
		public FunctionSource (TextSpan Span)
			: base (Span)
		{
		}
	}

	public class TableSource : ISource
	{
		// here: replace with reference to Table?
		public string SchemaName { get; } = null;
		public string TableName { get; } = null;
		public string TableColumnName { get; } = null;

		public TableSource (string SchemaName, string TableName, string TableColumnName)
		{
			this.SchemaName = SchemaName;
			this.TableName = TableName;
			this.TableColumnName = TableColumnName;
		}
	}

	public class CompositeTypeSource : ISource
	{
		// here: replace with reference to PSqlType?
		public string SchemaName { get; } = null;
		public string TypeName { get; } = null;
		public string PropertyName { get; } = null;

		public CompositeTypeSource (string SchemaName, string TypeName, string PropertyName)
		{
			this.SchemaName = SchemaName;
			this.TypeName = TypeName;
			this.PropertyName = PropertyName;
		}
	}

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

		public Sourced<N> Select<N> (Func<T, N> Convert)
		{
			return new Sourced<N> (Convert (Value), Source);
		}
	}

	public static class SourcedUtils
	{
		public static T[] Values<T> (this IEnumerable<Sourced<T>> Items)
		{
			return Items.Select (f => f.Value).ToArray ();
		}

		public static TextSpan Range<T> (this IEnumerable<Sourced<T>> Items)
		{
			return Items.Select (f => f.TextSpan).Range ();
		}

		public static Sourced<string> ToLower (this Sourced<string> Inner)
		{
			return Inner.Select (s => s.ToLower ());
		}

		public static Sourced<T> SourcedUnknown<T> (this T Value)
		{
			return new Sourced<T> (Value, null);
		}

		public static Sourced<T> SourcedDefinition<T> (this T Value)
		{
			return new Sourced<T> (Value, DefinitionSource.Instance);
		}

		public static Sourced<T> SourcedTextSpan<T> (this T Value, TextSpan Span)
		{
			return new Sourced<T> (Value, new TextSpanSource (Span));
		}

		public static Sourced<T> SourcedTextSpan<T, N> (this T Value, ITextSpan<N> Span)
		{
			return new Sourced<T> (Value, new TextSpanSource (Span.ToTextSpan ()));
		}

		public static Sourced<T> ToSourced<T> (this ITextSpan<T> Span)
		{
			return new Sourced<T> (Span.Value, new TextSpanSource (Span.ToTextSpan ()));
		}

		public static Sourced<T>[] ToSourced<T> (this IEnumerable<ITextSpan<T>> Spans)
		{
			return Spans.Select (s => s.ToSourced ()).ToArray ();
		}

		public static Sourced<T> SourcedCalculated<T> (this T Value, TextSpan Span)
		{
			return new Sourced<T> (Value, new CalculatedSource (Span));
		}

		public static Sourced<T> SourcedCalculated<T, N> (this T Value, ITextSpan<N> Span)
		{
			return new Sourced<T> (Value, new CalculatedSource (Span.ToTextSpan ()));
		}

		public static Sourced<T> SourcedFunction<T> (this T Value, TextSpan Span)
		{
			return new Sourced<T> (Value, new FunctionSource (Span));
		}

		public static Sourced<T> SourcedTable<T> (this T Value,
			string SchemaName, string TableName, string TableColumnName)
		{
			return new Sourced<T> (Value, new TableSource (SchemaName, TableName, TableColumnName));
		}

		public static Sourced<T> SourcedCompositeType<T> (this T Value,
			string SchemaName, string TypeName, string PropertyName)
		{
			return new Sourced<T> (Value, new CompositeTypeSource (SchemaName, TypeName, PropertyName));
		}
	}
}
