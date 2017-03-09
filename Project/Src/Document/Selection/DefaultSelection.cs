// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System.Diagnostics;

namespace ICSharpCode.TextEditor.Document
{
	/// <summary>
	///     Default implementation of the <see cref="ICSharpCode.TextEditor.Document.ISelection" /> interface.
	/// </summary>
	public class DefaultSelection : ISelection
	{
		private readonly IDocument _document;
		private TextLocation _endPosition;
		private TextLocation _startPosition;

		/// <summary>
		///     Creates a new instance of <see cref="DefaultSelection" />
		/// </summary>
		public DefaultSelection(IDocument document, TextLocation startPosition, TextLocation endPosition)
		{
			DefaultDocument.ValidatePosition(document, startPosition);
			DefaultDocument.ValidatePosition(document, endPosition);
			Debug.Assert(startPosition <= endPosition);
			_document = document;
			_startPosition = startPosition;
			_endPosition = endPosition;
		}

		public TextLocation StartPosition
		{
			get { return _startPosition; }
			set
			{
				DefaultDocument.ValidatePosition(_document, value);
				_startPosition = value;
			}
		}

		public TextLocation EndPosition
		{
			get { return _endPosition; }
			set
			{
				DefaultDocument.ValidatePosition(_document, value);
				_endPosition = value;
			}
		}

		public int Offset => _document.PositionToOffset(_startPosition);

		public int EndOffset => _document.PositionToOffset(_endPosition);

		public int Length => EndOffset - Offset;

		/// <value>
		///     Returns true, if the selection is empty
		/// </value>
		public bool IsEmpty => _startPosition == _endPosition;

		/// <value>
		///     Returns true, if the selection is rectangular
		/// </value>
		// TODO : make this unused property used.
		public bool IsRectangularSelection { get; set; }

		/// <value>
		///     The text which is selected by this selection.
		/// </value>
		public string SelectedText
		{
			get
			{
				if (_document != null)
				{
					if (Length < 0)
						return null;
					return _document.GetText(Offset, Length);
				}
				return null;
			}
		}

		public bool ContainsPosition(TextLocation position)
		{
			if (IsEmpty)
				return false;
			return _startPosition.Y < position.Y && position.Y < _endPosition.Y ||
			       _startPosition.Y == position.Y && _startPosition.X <= position.X &&
			       (_startPosition.Y != _endPosition.Y || position.X <= _endPosition.X) ||
			       _endPosition.Y == position.Y && _startPosition.Y != _endPosition.Y && position.X <= _endPosition.X;
		}

		public bool ContainsOffset(int offset)
		{
			return Offset <= offset && offset <= EndOffset;
		}

		/// <summary>
		///     Converts a <see cref="DefaultSelection" /> instance to string (for debug purposes)
		/// </summary>
		public override string ToString()
		{
			return string.Format("[DefaultSelection : StartPosition={0}, EndPosition={1}]", _startPosition, _endPosition);
		}
	}
}