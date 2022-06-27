using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace Utils.CodeGeneration
{
	public enum EndOfLine
	{
		LeaveAsIs,
		MakeLf,
		MakeCrLf
	}

	public static class CodeGenerationUtils
	{
		public static readonly string AutomaticWarning =
@"/*
   * This file has been generated automatically.
   * Do not edit, or you will lose your changes after the next run.
   */
";

		// only write file if it's missing or has mismatching text
		public static void EnsureFileContents (string FileName, string Contents,
			EndOfLine EndOfLine = EndOfLine.LeaveAsIs,
			Encoding enc = null
			)
		{
			enc = enc ?? Encoding.UTF8;

			switch (EndOfLine)
			{
				case EndOfLine.LeaveAsIs:
					break;

				case EndOfLine.MakeLf:
					Contents = Regex.Replace (Contents, @"\r?\n", "\n");
					break;

				case EndOfLine.MakeCrLf:
					Contents = Regex.Replace (Contents, @"\r?\n", "\r\n");
					break;
			}

			if (!File.Exists (FileName) || File.ReadAllText (FileName, enc) != Contents)
			{
				File.WriteAllText (FileName, Contents, enc);
			}
		}
	}

	public class IndentedTextBuilder
	{
		class Block : IDisposable
		{
			IndentedTextBuilder Builder;
			int Depth;
			string OpenBracket;
			string CloseBracket;

			public Block (IndentedTextBuilder Builder, int Depth = 1, string OpenBracket = null, string CloseBracket = null)
			{
				this.Builder = Builder;
				this.Depth = Depth;
				this.OpenBracket = OpenBracket;
				this.CloseBracket = CloseBracket;

				if (this.OpenBracket != null)
				{
					this.Builder.AppendLine (this.OpenBracket);
				}

				++this.Builder.Depth;
			}

			public void Dispose ()
			{
				--Builder.Depth;

				if (CloseBracket != null)
				{
					Builder.AppendLine (CloseBracket);
				}
			}
		}

		public int Depth;
		public string IndentText;
		public StringBuilder StringBuilder;

		public IndentedTextBuilder (string IndentText = "\t")
		{
			this.IndentText = IndentText;
			this.Depth = 0;
			this.StringBuilder = new StringBuilder ();
		}

		public override string ToString ()
		{
			return StringBuilder.ToString ();
		}

		public int GetLastLineWidth (int TabWidth = 4)
		{
			int TotalLength = StringBuilder.Length;

			int LastLineLength = 0;
			int Pos = TotalLength - 1;
			while (Pos >= 0)
			{
				char c = StringBuilder[Pos];
				if (c == '\n' || c == '\r')
				{
					break;
				}

				++LastLineLength;
				--Pos;
			}

			++Pos;
			int LastLineWidth = 0;
			while (Pos < TotalLength)
			{
				char c = StringBuilder[Pos];
				if (c == '\t')
				{
					LastLineWidth = ((LastLineWidth / TabWidth) + 1) * TabWidth;
				}
				else
				{
					++LastLineWidth;
				}

				++Pos;
			}

			return LastLineWidth;
		}

		public IndentedTextBuilder TypeIndent (int AddedDepth = 0)
		{
			int TotalDepth = Depth + AddedDepth;

			if (TotalDepth > 0)
			{
				StringBuilder.Append (string.Concat (Enumerable.Repeat (IndentText, TotalDepth)));
			}

			return this;
		}

		public IndentedTextBuilder TypeText (string Text)
		{
			if (!string.IsNullOrEmpty (Text))
			{
				StringBuilder.Append (Text);
			}

			return this;
		}

		// no check that Line is indeed just 1 line
		protected IndentedTextBuilder AppendSingleLine (string Line, int AddedDepth)
		{
			if (!string.IsNullOrWhiteSpace (Line))
			{
				if (StringBuilder.Length == 0 || StringBuilder[^1] == '\n')
				{
					TypeIndent (AddedDepth);
				}

				StringBuilder.AppendLine (Line);
			}
			else
			{
				StringBuilder.AppendLine ();
			}

			return this;
		}

		public IndentedTextBuilder AppendLine (string Text = null, int AddedDepth = 0)
		{
			if (Text == null)
			{
				return AppendSingleLine (null, AddedDepth);
			}

			foreach (string Line in Text.Split ('\n').Select (s => s.TrimEnd ('\r')))
			{
				AppendSingleLine (Line, AddedDepth);
			}

			return this;
		}

		public IndentedTextBuilder AppendFormat (string Format, params object[] Args)
		{
			return AppendLine (string.Format (Format, Args));
		}

		public IDisposable UseBlock (int AddedDepth = 1)
		{
			return new Block (this, AddedDepth);
		}

		public IDisposable UseCurlyBraces (string Caption = null, bool IncludeOpen = true, int AddedDepth = 1, bool Semicolon = false)
		{
			if (!string.IsNullOrWhiteSpace (Caption))
			{
				AppendLine (Caption);
			}

			return new Block (this, AddedDepth, IncludeOpen ? "{" : null, Semicolon ? "};" : "}");
		}

		public IDisposable UseParentheses (bool IncludeOpen = true, int AddedDepth = 1)
		{
			return new Block (this, AddedDepth, IncludeOpen ? "(" : null, ")");
		}

		public IDisposable UseSquareBrackets (bool IncludeOpen = true, int AddedDepth = 1)
		{
			return new Block (this, AddedDepth, IncludeOpen ? "[" : null, "]");
		}
	}
}
