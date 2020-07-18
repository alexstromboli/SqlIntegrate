using System;

namespace ParseProcs
{
	public class OperatorProcessor
	{
		public int Precedence = 0;
		public bool IsBinary = false;

		public Func<
			Func<RequestContext, PSqlType>,		// left
			Func<RequestContext, PSqlType>,		// right (null for unary)
			Func<RequestContext, PSqlType>		// result
		> Processor = null;

		public OperatorProcessor (PSqlOperatorPriority Precedence,
			bool IsBinary,
			Func<
				Func<RequestContext, PSqlType>, // left
				Func<RequestContext, PSqlType>, // right (null for unary)
				Func<RequestContext, PSqlType> // result
			> Processor
		)
		{
			this.Precedence = (int)Precedence;
			this.IsBinary = IsBinary;
			this.Processor = Processor;
		}

		public static Func<
			Func<RequestContext, PSqlType>, // left
			Func<RequestContext, PSqlType>, // right (null for unary)
			Func<RequestContext, PSqlType> // result
		> GetForBinaryOperator (string Operator)
		{
			return (l, r) => rc =>
			{
				PSqlType Left = l (rc);
				PSqlType Right = r (rc);
				return PSqlUtils.GetBinaryOperationResultType (Left, Right, Operator);
			};
		}
	}
}
