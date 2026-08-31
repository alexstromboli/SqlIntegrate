using System;
using System.Text;

using Sprache;

namespace DbAnalysis
{
	// Where a parse actually got to, as opposed to where Sprache says it failed.
	//
	// Sprache carries the furthest failure across the alternatives of a single Or
	// (DetermineBestError), but the two combinators this grammar leans on hardest
	// throw it away: Many () stops at the first failure and reports SUCCESS with
	// what it had, and Optional () does the same. A statement list is a Many over an
	// Or of statement parsers, so a syntax error deep inside one statement does not
	// propagate at all -- the list simply ends early, and the failure surfaces far
	// away as "the enclosing block does not end where it should".
	//
	// The observed shape of that, which is what this class exists to fix: an
	// unsupported operator in the SELECT tail of an OPEN ... FOR was reported as
	// "Unexpected open" at the line of the OPEN itself, five lines earlier -- a
	// token that is legal there, in a statement that is correct. The natural reading
	// is that OPEN ... FOR is unsupported, which sends you bisecting the wrong half
	// of the procedure.
	//
	// So record the high-water mark independently. Every token in the grammar is
	// built through SpracheUtils.SqlToken, so noting the remainder there after each
	// successful token gives the furthest offset the parser ever reached, whatever
	// backtracking happened afterwards.
	public static class ParseProgress
	{
		// Deliberately not on IInput.Memos: CustomInput mints a fresh dictionary per
		// instance and compares equal on (source, position), so memos are not shared
		// between the instances a parse creates. A ThreadStatic field is enough --
		// ParseProcs parses one procedure at a time -- and keeps the tracking out of
		// the parser's own types.
		[ThreadStatic]
		private static int FurthestPosition;

		public static void Reset ()
		{
			FurthestPosition = 0;
		}

		public static void Note (IInput Remainder)
		{
			if (Remainder != null && Remainder.Position > FurthestPosition)
			{
				FurthestPosition = Remainder.Position;
			}
		}

		// Renders the high-water mark against the source that was parsed: the line
		// and column, the line itself, and a caret under the offset.
		//
		// Line numbers are relative to the procedure BODY, because that is what gets
		// parsed -- pg_proc.prosrc, not the .sql file the procedure was deployed
		// from. The numbers are never file-relative, and a reader who assumes they are
		// will chase the wrong line.
		public static string Describe (string Source)
		{
			if (string.IsNullOrEmpty (Source))
			{
				return null;
			}

			int Position = Math.Min (FurthestPosition, Source.Length);

			int LineStart = Source.LastIndexOf ('\n', Math.Max (Position - 1, 0)) + 1;
			if (Position == 0)
			{
				LineStart = 0;
			}

			int LineEnd = Source.IndexOf ('\n', LineStart);
			if (LineEnd < 0)
			{
				LineEnd = Source.Length;
			}

			int Line = 1;
			for (int i = 0; i < LineStart; i++)
			{
				if (Source[i] == '\n')
				{
					Line++;
				}
			}

			int Column = Position - LineStart + 1;
			string Text = Source.Substring (LineStart, LineEnd - LineStart).TrimEnd ('\r');

			// Tabs would put the caret under the wrong character once the terminal
			// expands them, so render them as single spaces in both the text and the
			// padding.
			string Shown = Text.Replace ('\t', ' ');
			string Gutter = Line.ToString ();
			string Pad = new string (' ', Math.Max (Column - 1, 0));

			return new StringBuilder ()
					.Append ("  furthest progress: line ").Append (Line)
					.Append (", column ").Append (Column).Append ('\n')
					.Append ("    ").Append (Gutter).Append (" | ").Append (Shown).Append ('\n')
					.Append ("    ").Append (new string (' ', Gutter.Length)).Append (" | ").Append (Pad).Append ('^').Append ('\n')
					.Append ("  note: lines are relative to the procedure body (pg_proc.prosrc), not the .sql file")
					.ToString ()
				;
		}
	}
}
