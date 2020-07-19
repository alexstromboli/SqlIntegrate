using System;

namespace ParseProcs
{
	public class OperatorProcessor
	{
		public int Precedence = 0;
		public bool IsBinary = false;

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
			> Processor
		)
		{
			this.Precedence = (int)Precedence;
			this.IsBinary = IsBinary;
			this.Processor = Processor;
		}

		public static Func<
			Func<RequestContext, NamedTyped>, // left
			Func<RequestContext, NamedTyped>, // right (null for unary)
			Func<RequestContext, NamedTyped> // result
		> GetForBinaryOperator (string Operator)
		{
			return (l, r) => rc =>
			{
				PSqlType Left = l (rc).Type;
				PSqlType Right = r (rc).Type;
				return new NamedTyped (PSqlUtils.GetBinaryOperationResultType (Left, Right, Operator));
			};
		}
	}
}