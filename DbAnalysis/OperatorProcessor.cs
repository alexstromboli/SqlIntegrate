using System;

using Sprache;		// for utilities

using DbAnalysis.Sources;

namespace DbAnalysis
{
	public class OperatorProcessor
	{
		public int Precedence = 0;
		public bool IsBinary = false;
		public bool IsBetween = false;
		public bool IsAnd = false;

		public Func<
			Func<RequestContext, NamedTyped>,		// left
			Func<RequestContext, NamedTyped>,		// right (null for unary)
			Func<RequestContext, NamedTyped>		// result
		> Processor = null;

		public OperatorProcessor (PSqlOperatorPriority Precedence,
			bool IsBinary,
			Func<
				Func<RequestContext, NamedTyped>, // left
				Func<RequestContext, NamedTyped>, // right (null for unary)
				Func<RequestContext, NamedTyped> // result
			> Processor,
			bool IsBetween = false,
			bool IsAnd = false
		)
		{
			this.Precedence = (int)Precedence;
			this.IsBinary = IsBinary;
			this.Processor = Processor;
			this.IsBetween = IsBetween;
			this.IsAnd = IsAnd;
		}

		public static Func<
			Func<RequestContext, NamedTyped>, // left
			Func<RequestContext, NamedTyped>, // right (null for unary)
			Func<RequestContext, NamedTyped> // result
		> GetForBinaryOperator (SqlTypeMap Typemap, Sourced<string> Operator)
		{
			return (l, r) => rc =>
			{
				Sourced<PSqlType> Left = l (rc).Type;
				Sourced<PSqlType> Right = r (rc).Type;
				return new NamedTyped (
					PSqlUtils.GetBinaryOperationResultType (Typemap, Left.Value, Right.Value, Operator.Value)
						.SourcedCalculated (TextSpan.Range (Left.TextSpan, Right.TextSpan))
				);
			};
		}

		public static Func<
			Func<RequestContext, NamedTyped>, // left
			Func<RequestContext, NamedTyped>, // right (null for unary)
			Func<RequestContext, NamedTyped> // result
		> GetForBinaryOperator (SqlTypeMap Typemap, ITextSpan<string> Operator)
		{
			return GetForBinaryOperator (Typemap, Operator.ToSourced ());
		}

		public static Func<
			Func<RequestContext, NamedTyped>, // left
			Func<RequestContext, NamedTyped>, // right (null for unary)
			Func<RequestContext, NamedTyped> // result
		> ProduceType (Sourced<PSqlType> Type)
		{
			return (l, r) => rc => new NamedTyped (Type);
		}
	}
}
