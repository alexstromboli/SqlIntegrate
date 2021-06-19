using System;
using System.Collections.Generic;

using Sprache;

namespace ParseProcs
{
	public class SPolynom
	{
		public class Operand
		{
			// ignore prefixes as irrelevant
			public Func<IRequestContext, NamedTyped> Atomic;
			public IOption<IEnumerable<OperatorProcessor>> Postfixes;
		}

		public List<Operand> Operands;
		public List<OperatorProcessor> Operators;

		protected NamedTyped ResultNameType = null;

		public NamedTyped GetResult (IRequestContext Context)
		{
			if (ResultNameType != null)
			{
				return ResultNameType;
			}

			Stack<Func<IRequestContext, NamedTyped>> OperandsStack = new Stack<Func<IRequestContext, NamedTyped>> ();
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

				if (arg.Postfixes.IsDefined)
				{
					foreach (var Postfix in arg.Postfixes.Get ())
					{
						Perform (Postfix.Precedence);
						var l = OperandsStack.Pop ();
						var res = Postfix.Processor (l, null);
						OperandsStack.Push (res);
					}
				}
			};

			Process (Operands[0]);
			for (int i = 0; i < Operators.Count; ++i)
			{
				var op = Operators[i];
				Perform (op.Precedence);
				OperatorsStack.Push (op);

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
