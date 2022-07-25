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
			RcFunc<NamedTyped>,		// left
			RcFunc<NamedTyped>,		// right (null for unary)
			RcFunc<NamedTyped>		// result
		> Processor = null;

		public OperatorProcessor (PSqlOperatorPriority Precedence,
			bool IsBinary,
			Func<
				RcFunc<NamedTyped>, // left
				RcFunc<NamedTyped>, // right (null for unary)
				RcFunc<NamedTyped> // result
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
			RcFunc<NamedTyped>, // left
			RcFunc<NamedTyped>, // right (null for unary)
			RcFunc<NamedTyped> // result
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
			RcFunc<NamedTyped>, // left
			RcFunc<NamedTyped>, // right (null for unary)
			RcFunc<NamedTyped> // result
		> ProduceType<T> (Sourced<T> Span, PSqlType Type)
		{
			return (l, r) => rc => new NamedTyped (Type.SourcedCalculated (Span));
		}
	}
}
