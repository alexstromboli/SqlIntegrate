using System;
using System.Collections.Generic;

namespace DbAnalysis
{
	public class SPolynom
	{
		public class Operand
		{
			// ignore prefixes as irrelevant
			public RcFunc<NamedTyped> Atomic;
			public OperatorProcessor[] Postfixes;
		}

		public List<Operand> Operands;
		public List<OperatorProcessor> Operators;

		protected NamedTyped ResultNameType = null;

		public NamedTyped GetResult (RequestContext Context)
		{
			if (ResultNameType != null)
			{
				return ResultNameType;
			}

			Stack<RcFunc<NamedTyped>> OperandsStack = new Stack<RcFunc<NamedTyped>> ();
			Stack<OperatorProcessor> OperatorsStack = new Stack<OperatorProcessor> ();
			Action<int> Perform = n =>
			{
				while (OperatorsStack.Count > 0 && OperatorsStack.Peek ().Precedence >= n)
				{
					OperatorProcessor op = OperatorsStack.Pop ();
					var r = OperandsStack.Pop ();
					var l = OperandsStack.Pop ();
					var res = op.Processor (l, r);
					OperandsStack.Push (res);
				}
			};

			Action<Operand> Process = arg =>
			{
				OperandsStack.Push (arg.Atomic);

				foreach (var Postfix in arg.Postfixes)
				{
					Perform (Postfix.Precedence);
					var l = OperandsStack.Pop ();
					var res = Postfix.Processor (l, null);
					OperandsStack.Push (res);
				}
			};

			bool HasBetween = false;		// apparently, PSQL does not allow nested betweens on the same level, so only one level
			Process (Operands[0]);
			for (int i = 0; i < Operators.Count; ++i)
			{
				var op = Operators[i];
				Perform (HasBetween && op.IsAnd
					? (int)PSqlOperatorPriority.Between
					: op.Precedence);
				OperatorsStack.Push (op);

				if (op.IsBetween)
				{
					HasBetween = true;
				}
				else if (op.IsAnd)
				{
					HasBetween = false;
				}

				var v = Operands[i + 1];
				Process (v);
			}

			Perform (0);
			var ResultFunc = OperandsStack.Pop ();
			ResultNameType = ResultFunc (Context);
			return ResultNameType;
		}
	}
}
