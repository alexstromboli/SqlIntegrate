using System;

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
		> GetForBinaryOperator (SqlTypeMap Typemap, string Operator)
		{
			return (l, r) => rc =>
			{
				PSqlType Left = l (rc).Type;
				PSqlType Right = r (rc).Type;
				return new NamedTyped (PSqlUtils.GetBinaryOperationResultType (Typemap, Left, Right, Operator));
			};
		}

		public static Func<
			Func<RequestContext, NamedTyped>, // left
			Func<RequestContext, NamedTyped>, // right (null for unary)
			Func<RequestContext, NamedTyped> // result
		> ProduceType (PSqlType Type)
		{
			return (l, r) => rc => new NamedTyped (Type);
		}
	}
}
