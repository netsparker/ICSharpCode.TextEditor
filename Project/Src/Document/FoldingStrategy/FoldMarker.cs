// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;

namespace ICSharpCode.TextEditor.Document
{
	public enum FoldType
	{
		Unspecified,
		MemberBody,
		Region,
		TypeBody
	}

	public class FoldMarker : AbstractSegment, IComparable
	{
		private readonly IDocument _document;
		private int _startLine = -1, _startColumn, _endLine = -1, _endColumn;

		public FoldMarker(IDocument document, int offset, int length, string foldText, bool isFolded)
		{
			_document = document;
			this.offset = offset;
			this.length = length;
			FoldText = foldText;
			IsFolded = isFolded;
		}

		public FoldMarker(IDocument document, int startLine, int startColumn, int endLine, int endColumn)
			: this(document, startLine, startColumn, endLine, endColumn, FoldType.Unspecified)
		{
		}

		public FoldMarker(IDocument document, int startLine, int startColumn, int endLine, int endColumn, FoldType foldType)
			: this(document, startLine, startColumn, endLine, endColumn, foldType, "...")
		{
		}

		public FoldMarker(IDocument document, int startLine, int startColumn, int endLine, int endColumn, FoldType foldType,
			string foldText) : this(document, startLine, startColumn, endLine, endColumn, foldType, foldText, false)
		{
		}

		public FoldMarker(IDocument document, int startLine, int startColumn, int endLine, int endColumn, FoldType foldType,
			string foldText, bool isFolded)
		{
			_document = document;

			startLine = Math.Min(document.TotalNumberOfLines - 1, Math.Max(startLine, 0));
			ISegment startLineSegment = document.GetLineSegment(startLine);

			endLine = Math.Min(document.TotalNumberOfLines - 1, Math.Max(endLine, 0));
			ISegment endLineSegment = document.GetLineSegment(endLine);

			// Prevent the region from completely disappearing
			if (string.IsNullOrEmpty(foldText))
				foldText = "...";

			FoldType = foldType;
			FoldText = foldText;
			offset = startLineSegment.Offset + Math.Min(startColumn, startLineSegment.Length);
			length = endLineSegment.Offset + Math.Min(endColumn, endLineSegment.Length) - offset;
			IsFolded = isFolded;
		}

		public FoldType FoldType { get; set; } = FoldType.Unspecified;

		public int StartLine
		{
			get
			{
				if (_startLine < 0)
					GetPointForOffset(_document, offset, out _startLine, out _startColumn);
				return _startLine;
			}
		}

		public int StartColumn
		{
			get
			{
				if (_startLine < 0)
					GetPointForOffset(_document, offset, out _startLine, out _startColumn);
				return _startColumn;
			}
		}

		public int EndLine
		{
			get
			{
				if (_endLine < 0)
					GetPointForOffset(_document, offset + length, out _endLine, out _endColumn);
				return _endLine;
			}
		}

		public int EndColumn
		{
			get
			{
				if (_endLine < 0)
					GetPointForOffset(_document, offset + length, out _endLine, out _endColumn);
				return _endColumn;
			}
		}

		public override int Offset
		{
			get { return base.Offset; }
			set
			{
				base.Offset = value;
				_startLine = -1;
				_endLine = -1;
			}
		}

		public override int Length
		{
			get { return base.Length; }
			set
			{
				base.Length = value;
				_endLine = -1;
			}
		}

		public bool IsFolded { get; set; }

		public string FoldText { get; } = "...";

		public string InnerText => _document.GetText(offset, length);

		public int CompareTo(object o)
		{
			if (!(o is FoldMarker))
				throw new ArgumentException();
			var f = (FoldMarker) o;
			if (offset != f.offset)
				return offset.CompareTo(f.offset);

			return length.CompareTo(f.length);
		}

		private static void GetPointForOffset(IDocument document, int offset, out int line, out int column)
		{
			if (offset > document.TextLength)
			{
				line = document.TotalNumberOfLines + 1;
				column = 1;
			}
			else if (offset < 0)
			{
				line = -1;
				column = -1;
			}
			else
			{
				line = document.GetLineNumberForOffset(offset);
				column = offset - document.GetLineSegment(line).Offset;
			}
		}
	}
}