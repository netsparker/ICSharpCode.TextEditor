// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;

namespace ICSharpCode.TextEditor.Document
{
	public enum AnchorMovementType
	{
		/// <summary>
		///     Behaves like a start marker - when text is inserted at the anchor position, the anchor will stay
		///     before the inserted text.
		/// </summary>
		BeforeInsertion,

		/// <summary>
		///     Behave like an end marker - when text is insered at the anchor position, the anchor will move
		///     after the inserted text.
		/// </summary>
		AfterInsertion
	}

	/// <summary>
	///     An anchor that can be put into a document and moves around when the document is changed.
	/// </summary>
	public sealed class TextAnchor
	{
		private int _columnNumber;

		private LineSegment _lineSegment;

		internal TextAnchor(LineSegment lineSegment, int columnNumber)
		{
			_lineSegment = lineSegment;
			_columnNumber = columnNumber;
		}

		public LineSegment Line
		{
			get
			{
				if (_lineSegment == null) throw AnchorDeletedError();
				return _lineSegment;
			}
			internal set { _lineSegment = value; }
		}

		public bool IsDeleted => _lineSegment == null;

		public int LineNumber => Line.LineNumber;

		public int ColumnNumber
		{
			get
			{
				if (_lineSegment == null) throw AnchorDeletedError();
				return _columnNumber;
			}
			internal set { _columnNumber = value; }
		}

		public TextLocation Location => new TextLocation(ColumnNumber, LineNumber);

		public int Offset => Line.Offset + _columnNumber;

		/// <summary>
		///     Controls how the anchor moves.
		/// </summary>
		public AnchorMovementType MovementType { get; set; }

		private static Exception AnchorDeletedError()
		{
			return new InvalidOperationException("The text containing the anchor was deleted");
		}

		public event EventHandler Deleted;

		internal void Delete(ref DeferredEventList deferredEventList)
		{
			// we cannot fire an event here because this method is called while the LineManager adjusts the
			// lineCollection, so an event handler could see inconsistent state
			_lineSegment = null;
			deferredEventList.AddDeletedAnchor(this);
		}

		internal void RaiseDeleted()
		{
			if (Deleted != null)
				Deleted(this, EventArgs.Empty);
		}

		public override string ToString()
		{
			if (IsDeleted)
				return "[TextAnchor (deleted)]";
			return "[TextAnchor " + Location + "]";
		}
	}
}