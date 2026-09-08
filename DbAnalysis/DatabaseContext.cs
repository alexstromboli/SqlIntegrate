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

		// Which overload of a name a call site means, given the types its arguments carry.
		// Null when the name names no function at all.
		//
		// An argument type may be null, and so may the whole list: an argument is an
		// expression, and one the surrounding context cannot type says nothing about which
		// overload was meant. Resolution therefore narrows rather than decides -- it never
		// refuses a name the database has, because a call whose value nothing reads must go
		// on analysing, and because the alternative to a resolved overload is not an error
		// but the answer catalogue order alone would have given.
		public DbFunction ResolveFunction (string[] NameSegments, IReadOnlyList<PSqlType> ArgumentTypes)
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

			if (Overloads.Count == 1 || ArgumentTypes == null)
			{
				return Fallback;
			}

			var Ranked = Overloads
					.Select ((f, Index) => new { Function = f, Index, Score = ScoreOverload (f, ArgumentTypes) })
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
		protected (int Exact, int Accepted)? ScoreOverload (DbFunction Function,
			IReadOnlyList<PSqlType> ArgumentTypes)
		{
			int Declared = Function.Arguments.Length;

			// Defaults let a call name fewer arguments than are declared; a variadic last
			// argument lets it name more, or none in that position at all.
			int Min = Declared - Function.DefaultCount - (Function.IsVariadic ? 1 : 0);
			int Max = Function.IsVariadic ? int.MaxValue : Declared;

			if (ArgumentTypes.Count < Min || ArgumentTypes.Count > Max)
			{
				return null;
			}

			int Exact = 0;
			int Accepted = 0;

			for (int i = 0; i < ArgumentTypes.Count; ++i)
			{
				// At or past a variadic position the value is matched against the array's
				// element type, which this does not model, so such a position constrains
				// nothing and the plainly declared ones decide the call.
				if (i >= Declared || Function.IsVariadic && i >= Declared - 1)
				{
					continue;
				}

				PSqlType Actual = ArgumentTypes[i];
				DbFunctionArgument Formal = Function.Arguments[i];

				// Three kinds of nothing-to-compare, and none of them may reject: an
				// argument the call site could not be typed, a declared type the type map
				// has no answer for, and a declared pseudo-type, which accepts a value of
				// any type. The pseudo-type case scores nothing either, so an overload
				// naming the argument's own type stays ahead of it.
				if (Actual == null
				    || Actual.Display == TypeMap.Null.Display
				    || Formal.Type == null
				    || Formal.IsPseudo
				    )
				{
					continue;
				}

				if (Actual.Display == Formal.Type.Display)
				{
					++Exact;
					++Accepted;
					continue;
				}

				if (ImplicitCasts.Contains ((Actual.Display, Formal.Type.Display)))
				{
					++Accepted;
					continue;
				}

				return null;
			}

			return (Exact, Accepted);
		}
	}
}
