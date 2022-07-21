using System.Linq;
using System.Collections.Generic;

namespace DbAnalysis.Sources
{
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

		public static Sourced<T> SourcedCalculated<T> (this T Value, TextSpan Span)
		{
			return new Sourced<T> (Value, new CalculatedSource (Span));
		}

		public static Sourced<T> SourcedCalculated<T, C> (this T Value, Sourced<C> Source)
		{
			return new Sourced<T> (Value, new CalculatedSource (Source.TextSpan));
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
