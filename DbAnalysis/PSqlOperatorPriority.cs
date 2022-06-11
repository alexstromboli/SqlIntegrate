namespace ParseProcs
{
	public enum PSqlOperatorPriority
	{
		None,
		Or,
		And,
		Not,
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
