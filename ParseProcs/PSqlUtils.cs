namespace ParseProcs
{
	public static class PSqlUtils
	{
		public static PSqlType GetBinaryOperationResultType (PSqlType Left, PSqlType Right, string Operator)
		{
			if (Left.IsNumber && Right.IsNumber)
			{
				return Left.NumericLevel > Right.NumericLevel
					? Left
					: Right;
			}

			if (Left.IsNumber && Right.IsText
			    || Left.IsText && Right.IsNumber)
			{
				return Operator == "||"
					? PSqlType.Text
					: PSqlType.Int;
			}

			if (Left.IsDate && Right.IsTimeSpan)
			{
				return Left;
			}

			if (Left.IsTimeSpan && Right.IsDate)
			{
				return Right;
			}

			if (Left.IsTimeSpan && Right.IsTimeSpan)
			{
				return PSqlType.Interval;
			}

			if (Left.IsDate && Right.IsDate && Operator == "-")
			{
				return PSqlType.Interval;
			}

			return PSqlType.Null;
		}
	}
}
