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
		// AT TIME ZONE. PostgreSQL's gram.y declares '%left AT' between '^' and
		// UMINUS, so it binds tighter than exponentiation and looser than a unary
		// sign. Members are only ever compared relatively, so inserting one here
		// is safe.
		AtTimeZone,
		// COLLATE. PostgreSQL's gram.y declares '%left COLLATE' between '%left AT'
		// and '%right UMINUS', so it binds tighter than AT TIME ZONE and looser than
		// a unary sign.
		Collate,
		Unary,
		Array,
		Typecast,
		NameSeparator
	}
}
