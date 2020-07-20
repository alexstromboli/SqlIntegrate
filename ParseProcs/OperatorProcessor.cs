using System;

namespace ParseProcs
{
	public class OperatorProcessor
	{
		public int Precedence = 0;
		public bool IsBinary = false;

		public Func<
			Func<IRequestContext, NamedTyped>,		// left
			Func<IRequestContext, NamedTyped>,		// right (null for unary)
			Func<IRequestContext, NamedTyped>		// result
		> Processor = null;

		public OperatorProcessor (PSqlOperatorPriority Precedence,
			bool IsBinary,
			Func<
				Func<IRequestContext, NamedTyped>, // left
				Func<IRequestContext, NamedTyped>, // right (null for unary)
				Func<IRequestContext, NamedTyped> // result
			> Processor
		)
		{
			this.Precedence = (int)Precedence;
			this.IsBinary = IsBinary;
			this.Processor = Processor;
		}

		public static Func<
			Func<IRequestContext, NamedTyped>, // left
			Func<IRequestContext, NamedTyped>, // right (null for unary)
			Func<IRequestContext, NamedTyped> // result
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
