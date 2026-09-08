using System;
using System.Collections.Generic;
using System.Linq;

using Utils;

namespace DbAnalysis
{
	public class DatabaseContext
	{
		public string DatabaseName;
		public SqlTypeMap TypeMap;
		public Dictionary<string, DbTable> TablesDict;
		public Dictionary<string, Procedure> ProceduresDict;

		// The overloads of each qualified name, in catalogue order. A name is keyed rather
		// than a signature because that is what a call site writes; which of the overloads
		// it means is decided from the arguments beside it.
		public Dictionary<string, List<DbFunction>> FunctionsDict;

		// Type pairs PostgreSQL coerces between without being asked, by the types' display
		// names. Resolution needs them because a call site rarely names a declared argument
		// type exactly, and an overload rejected over a coercion PostgreSQL performs
		// silently would leave the arguments deciding nothing.
		public HashSet<(string Source, string Target)> ImplicitCasts;

		public List<string> SchemaOrder;

		public PSqlType GetTypeForName (params string[] TypeName)
		{
			return TypeMap.GetTypeForName (TypeName);
		}

		// A bare name is resolved by walking the schema order, so the schema order is part
		// of the lookup and not merely of the dictionary it reads. The whole lookup lives
		// here because it has to be replayable outside a parse: a cached analysis is
		// trusted only after its recorded callees resolve the same way again.
		public T GetSchemaEntity<T> (IReadOnlyDictionary<string, T> Dict, string[] NameSegments)
		{
			string Key = NameSegments.PSqlQualifiedName ();

			T Result;
			if (!Dict.TryGetValue (Key, out Result))
			{
				if (NameSegments.Length == 1)
				{
					foreach (string sch in SchemaOrder)
					{
						string SchKey = sch.ToTrivialArray ().Concat (NameSegments).PSqlQualifiedName ();
						if (Dict.TryGetValue (SchKey, out Result))
						{
							break;
						}
					}
				}
			}

			return Result;
		}

		// Every function the name reaches, or null when it reaches none. Null is a state of
		// its own and has to stay distinguishable from a function whose return type is
		// unusable: one sends the reader to the search_path and the other to the type map.
		public List<DbFunction> GetFunctionOverloads (string[] NameSegments)
		{
			return GetSchemaEntity (FunctionsDict, NameSegments);
		}

		// Which overload of a name a call site means, given the arguments beside it. Null
		// when the name names no function at all.
		//
		// An argument's type may be null, and so may the whole list: an argument is an
		// expression, and one the surrounding context cannot type says nothing about which
		// overload was meant. Resolution therefore narrows rather than decides -- it never
		// refuses a name the database has, because a call whose value nothing reads must go
		// on analysing, and because the alternative to a resolved overload is not an error
		// but the answer catalogue order alone would have given.
		public DbFunction ResolveFunction (string[] NameSegments, IReadOnlyList<CallArgument> Arguments)
		{
			List<DbFunction> Overloads = GetFunctionOverloads (NameSegments);

			if (Overloads == null || Overloads.Count == 0)
			{
				return null;
			}

			// What answers when the arguments distinguish nothing: the last overload in
			// catalogue order that names a usable type, and only failing that, the first
			// that does not. A candidate carrying a type nothing can hold never displaces
			// one that does, or every lower() in a corpus types as anyelement on the
			// strength of the range overload sorting last.
			DbFunction Fallback = Overloads.LastOrDefault (f => f.ReturnType != null)
			                      ?? Overloads[0];

			if (Overloads.Count == 1 || Arguments == null)
			{
				return Fallback;
			}

			var Ranked = Overloads
					.Select ((f, Index) => new { Function = f, Index, Score = ScoreOverload (f, Arguments) })
					.Where (c => c.Score != null)
					.ToList ()
				;

			if (Ranked.Count == 0)
			{
				return Fallback;
			}

			// Most significant first: how many arguments the call names exactly, then how
			// many it names a type for at all, then a usable return type, and last the
			// catalogue position -- descending, so that where the arguments separate
			// nothing this collapses to the same overload Fallback names. A call the
			// arguments genuinely do not decide must not answer differently for having
			// been ranked.
			return Ranked
					.OrderByDescending (c => c.Score.Value.Exact)
					.ThenByDescending (c => c.Score.Value.Accepted)
					.ThenByDescending (c => c.Function.ReturnType != null)
					.ThenByDescending (c => c.Index)
					.First ()
					.Function
				;
		}

		// Null when the overload cannot accept the call at all. Otherwise how well it
		// accepts it: Exact counts the arguments whose declared type the call names
		// outright, and Accepted counts those it names a type for that reaches the declared
		// one. Neither counts an argument there is nothing to compare on -- which is why a
		// call site that types none of its arguments scores every overload alike, and so
		// decides nothing rather than deciding arbitrarily.
		//
		// Every argument is bound to a declared parameter before anything is compared,
		// because which parameter an argument faces is not always its own position: a named
		// argument faces the parameter it names, and the arguments of an expanded variadic
		// call face the array's element type rather than a parameter each. Binding first is
		// also what makes arity a consequence rather than a separate rule -- a parameter
		// the call left unbound has to carry a default, and one bound twice rejects the
		// overload the way PostgreSQL rejects it.
		protected (int Exact, int Accepted)? ScoreOverload (DbFunction Function,
			IReadOnlyList<CallArgument> Arguments)
		{
			int Declared = Function.Arguments.Length;

			// Where the variadic parameter sits, if any. VARIADIC may be written only on
			// the last argument, so one flag describes the whole call: it passes the array
			// whole instead of naming its elements.
			int VariadicAt = Function.IsVariadic ? Declared - 1 : -1;
			bool PassesArray = Arguments.Count > 0 && Arguments[^1].IsVariadicArray;

			// A call passing the array whole names exactly as many arguments as the
			// function declares, where an expanded one may name more. Against a function
			// with no variadic parameter it names no candidate at all.
			if (PassesArray && (VariadicAt < 0 || Arguments.Count != Declared))
			{
				return null;
			}

			// Positional arguments precede every named one, which is the only order the
			// grammar accepts, so the positional prefix is what its length says it is.
			int PositionalCount = 0;
			while (PositionalCount < Arguments.Count && Arguments[PositionalCount].Name == null)
			{
				++PositionalCount;
			}

			// What each declared parameter is matched against, or null where the call leaves
			// it to its default. Arguments past a variadic position are not parameters of
			// their own, so they are collected beside it.
			CallArgument[] Bound = new CallArgument[Declared];
			List<CallArgument> VariadicElements = new List<CallArgument> ();

			for (int i = 0; i < PositionalCount; ++i)
			{
				if (VariadicAt >= 0 && i >= VariadicAt && !PassesArray)
				{
					VariadicElements.Add (Arguments[i]);
					continue;
				}

				// More positional arguments than the function declares, and no variadic
				// parameter to absorb the rest.
				if (i >= Declared)
				{
					return null;
				}

				Bound[i] = Arguments[i];
			}

			for (int i = PositionalCount; i < Arguments.Count; ++i)
			{
				// A name no parameter carries rejects the overload, exactly as PostgreSQL
				// rejects it, and so does a name landing on a parameter already bound. A
				// name reaching the variadic parameter binds the array whole, that being
				// the only thing binding one value to one parameter can mean.
				int At = FindArgumentByName (Function, Arguments[i].Name);

				if (At < 0 || Bound[At] != null)
				{
					return null;
				}

				Bound[At] = Arguments[i];
			}

			// A parameter the call bound to nothing has to carry a default, or be the
			// variadic one, which a call may leave out entirely.
			for (int i = 0; i < Declared; ++i)
			{
				if (Bound[i] == null && i < Declared - Function.DefaultCount && i != VariadicAt)
				{
					return null;
				}
			}

			int Exact = 0;
			int Accepted = 0;

			for (int i = 0; i < Declared; ++i)
			{
				if (Bound[i] != null
				    && !Accepts (Bound[i].Type, Function.Arguments[i].Type,
					    Function.Arguments[i].IsPseudo, ref Exact, ref Accepted)
				    )
				{
					return null;
				}
			}

			// An expanded variadic call names elements of the array, so each one is matched
			// against the element type. Left unscored they would say nothing -- including
			// where they reject the candidate outright -- and the call would be decided by
			// its fixed arguments alone.
			foreach (CallArgument Element in VariadicElements)
			{
				if (!Accepts (Element.Type, Function.VariadicElementType,
					    Function.VariadicElementIsPseudo, ref Exact, ref Accepted))
				{
					return null;
				}
			}

			return (Exact, Accepted);
		}

		// Whether the overload survives one argument, counting how well it takes it.
		//
		// Three kinds of nothing-to-compare, and none of them may reject: an argument the
		// call site could not type, a declared type the type map has no answer for, and a
		// declared pseudo-type, which accepts a value of any type. The pseudo-type case
		// scores nothing either, so an overload naming the argument's own type stays ahead
		// of it.
		protected bool Accepts (PSqlType Actual, PSqlType Formal, bool FormalIsPseudo,
			ref int Exact, ref int Accepted)
		{
			if (Actual == null
			    || Actual.Display == TypeMap.Null.Display
			    || Formal == null
			    || FormalIsPseudo
			    )
			{
				return true;
			}

			if (Actual.Display == Formal.Display)
			{
				++Exact;
				++Accepted;
				return true;
			}

			if (ImplicitCasts.Contains ((Actual.Display, Formal.Display)))
			{
				++Accepted;
				return true;
			}

			return false;
		}

		// Which declared parameter carries a name, or -1 where none does. Compared without
		// regard to case: an unquoted parameter name reaches the catalogue folded to lower
		// case, and a call site's is folded the same way, so the two meet already folded
		// and only a quoted declaration could differ.
		protected static int FindArgumentByName (DbFunction Function, string Name)
		{
			for (int i = 0; i < Function.Arguments.Length; ++i)
			{
				if (Function.Arguments[i].Name != null
				    && string.Equals (Function.Arguments[i].Name, Name,
					    StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}

			return -1;
		}
	}
}
