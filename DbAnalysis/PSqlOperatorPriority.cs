namespace DbAnalysis
{
	public enum PSqlOperatorPriority
	{
		None,
		Or,
		And,
		Not,
		ArrayOverlap,
		Is,
		Comparison,
		Like,
		Between,
		In,
		General,
		AddSub,
		MulDiv,
		Exp,
		Unary,
		Array,
		Typecast,
		NameSeparator
	}
}
