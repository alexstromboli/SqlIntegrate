using System;
using System.Collections.Generic;

using Sprache;

namespace DbAnalysis
{
	public class CustomInput : IInput, IEquatable<IInput>
	{
		private readonly string _source;
		private readonly int _position;
		private readonly int _line;
		private readonly int _column;

		/// <summary>
		/// Gets the list of memos assigned to the <see cref="T:Sprache.Input" /> instance.
		/// </summary>
		public IDictionary<object, object> Memos { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Sprache.Input" /> class.
		/// </summary>
		/// <param name="source">The source.</param>
		public CustomInput (string source)
			: this (source, 0)
		{
		}

		public CustomInput (string source, int position, int line = 1, int column = 1)
		{
			this._source = source;
			this._position = position;
			this._line = line;
			this._column = column;
			this.Memos = (IDictionary<object, object>)new Dictionary<object, object> ();
		}

		/// <summary>Advances the input.</summary>
		/// <returns>A new <see cref="T:Sprache.IInput" /> that is advanced.</returns>
		/// <exception cref="T:System.InvalidOperationException">The input is already at the end of the source.</exception>
		public IInput Advance ()
		{
			if (this.AtEnd)
				throw new InvalidOperationException ("The input is already at the end of the source.");
			return (IInput)new CustomInput (this._source, this._position + 1,
				this.Current == '\n' ? this._line + 1 : this._line, this.Current == '\n' ? 1 : this._column + 1);
		}

		/// <summary>Gets the whole source.</summary>
		public string Source => this._source;

		/// <summary>
		/// Gets the current <see cref="T:System.Char" />.
		/// </summary>
		public char Current => this._source[this._position];

		/// <summary>
		/// Gets a value indicating whether the end of the source is reached.
		/// </summary>
		public bool AtEnd => this._position == this._source.Length;

		/// <summary>Gets the current positon.</summary>
		public int Position => this._position;

		/// <summary>Gets the current line number.</summary>
		public int Line => this._line;

		/// <summary>Gets the current column.</summary>
		public int Column => this._column;

		/// <summary>Returns a string that represents the current object.</summary>
		/// <returns>A string that represents the current object.</returns>
		public override string ToString () =>
			string.Format ("Line {0}, Column {1}", (object)this._line, (object)this._column);

		/// <summary>Serves as a hash function for a particular type.</summary>
		/// <returns>
		/// A hash code for the current <see cref="T:Sprache.Input" />.
		/// </returns>
		public override int GetHashCode () =>
			(this._source != null ? this._source.GetHashCode () : 0) * 397 ^ this._position;

		/// <summary>
		/// Determines whether the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:Sprache.Input" />.
		/// </summary>
		/// <returns>
		/// true if the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:Sprache.Input" />; otherwise, false.
		/// </returns>
		/// <param name="obj">The object to compare with the current object. </param>
		public override bool Equals (object obj) => this.Equals (obj as IInput);

		/// <summary>
		/// Indicates whether the current <see cref="T:Sprache.Input" /> is equal to another object of the same type.
		/// </summary>
		/// <returns>
		/// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
		/// </returns>
		/// <param name="other">An object to compare with this object.</param>
		public bool Equals (IInput other)
		{
			if (other == null)
				return false;
			if ((object)this == (object)other)
				return true;
			return string.Equals (this._source, other.Source) && this._position == other.Position;
		}

		/// <summary>
		/// Indicates whether the left <see cref="T:Sprache.Input" /> is equal to the right <see cref="T:Sprache.Input" />.
		/// </summary>
		/// <param name="left">The left <see cref="T:Sprache.Input" />.</param>
		/// <param name="right">The right <see cref="T:Sprache.Input" />.</param>
		/// <returns>true if both objects are equal.</returns>
		public static bool operator == (CustomInput left, CustomInput right) => object.Equals ((object)left, (object)right);

		/// <summary>
		/// Indicates whether the left <see cref="T:Sprache.Input" /> is not equal to the right <see cref="T:Sprache.Input" />.
		/// </summary>
		/// <param name="left">The left <see cref="T:Sprache.Input" />.</param>
		/// <param name="right">The right <see cref="T:Sprache.Input" />.</param>
		/// <returns>true if the objects are not equal.</returns>
		public static bool operator != (CustomInput left, CustomInput right) => !object.Equals ((object)left, (object)right);
	}
}
