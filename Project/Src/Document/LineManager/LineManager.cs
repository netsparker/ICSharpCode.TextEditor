// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ICSharpCode.TextEditor.Document
{
	internal sealed class LineManager
	{
		// use always the same DelimiterSegment object for the NextDelimiter
		private readonly DelimiterSegment _delimiterSegment = new DelimiterSegment();

		private readonly IDocument _document;
		private IHighlightingStrategy _highlightingStrategy;
		private readonly LineSegmentTree _lineCollection = new LineSegmentTree();

		public LineManager(IDocument document, IHighlightingStrategy highlightingStrategy)
		{
			_document = document;
			_highlightingStrategy = highlightingStrategy;
		}

		public IList<LineSegment> LineSegmentCollection => _lineCollection;

		public int TotalNumberOfLines => _lineCollection.Count;

		public IHighlightingStrategy HighlightingStrategy
		{
			get { return _highlightingStrategy; }
			set
			{
				if (_highlightingStrategy != value)
				{
					_highlightingStrategy = value;
					if (_highlightingStrategy != null)
						_highlightingStrategy.MarkTokens(_document);
				}
			}
		}

		public int GetLineNumberForOffset(int offset)
		{
			return GetLineSegmentForOffset(offset).LineNumber;
		}

		public LineSegment GetLineSegmentForOffset(int offset)
		{
			return _lineCollection.GetByOffset(offset);
		}

		public LineSegment GetLineSegment(int lineNr)
		{
			return _lineCollection[lineNr];
		}

		public void Insert(int offset, string text)
		{
			Replace(offset, 0, text);
		}

		public void Remove(int offset, int length)
		{
			Replace(offset, length, string.Empty);
		}

		public void Replace(int offset, int length, string text)
		{
			Debug.WriteLine("Replace offset=" + offset + " length=" + length + " text.Length=" + text.Length);
			var lineStart = GetLineNumberForOffset(offset);
			var oldNumberOfLines = TotalNumberOfLines;
			var deferredEventList = new DeferredEventList();
			RemoveInternal(ref deferredEventList, offset, length);
			var numberOfLinesAfterRemoving = TotalNumberOfLines;
			if (!string.IsNullOrEmpty(text))
				InsertInternal(offset, text);
//			#if DEBUG
//			Console.WriteLine("New line collection:");
//			Console.WriteLine(lineCollection.GetTreeAsString());
//			Console.WriteLine("New text:");
//			Console.WriteLine("'" + document.TextContent + "'");
//			#endif
			// Only fire events after RemoveInternal+InsertInternal finished completely:
			// Otherwise we would expose inconsistent state to the event handlers.
			RunHighlighter(lineStart, 1 + Math.Max(0, TotalNumberOfLines - numberOfLinesAfterRemoving));

			if (deferredEventList.RemovedLines != null)
				foreach (var ls in deferredEventList.RemovedLines)
					OnLineDeleted(new LineEventArgs(_document, ls));
			deferredEventList.RaiseEvents();
			if (TotalNumberOfLines != oldNumberOfLines)
				OnLineCountChanged(new LineCountChangeEventArgs(_document, lineStart, TotalNumberOfLines - oldNumberOfLines));
		}

		private void RemoveInternal(ref DeferredEventList deferredEventList, int offset, int length)
		{
			Debug.Assert(length >= 0);
			if (length == 0) return;
			var it = _lineCollection.GetEnumeratorForOffset(offset);
			var startSegment = it.Current;
			var startSegmentOffset = startSegment.Offset;
			if (offset + length < startSegmentOffset + startSegment.TotalLength)
			{
				// just removing a part of this line segment
				startSegment.RemovedLinePart(ref deferredEventList, offset - startSegmentOffset, length);
				SetSegmentLength(startSegment, startSegment.TotalLength - length);
				return;
			}
			// merge startSegment with another line segment because startSegment's delimiter was deleted
			// possibly remove lines in between if multiple delimiters were deleted
			var charactersRemovedInStartLine = startSegmentOffset + startSegment.TotalLength - offset;
			Debug.Assert(charactersRemovedInStartLine > 0);
			startSegment.RemovedLinePart(ref deferredEventList, offset - startSegmentOffset, charactersRemovedInStartLine);


			var endSegment = _lineCollection.GetByOffset(offset + length);
			if (endSegment == startSegment)
			{
				// special case: we are removing a part of the last line up to the
				// end of the document
				SetSegmentLength(startSegment, startSegment.TotalLength - length);
				return;
			}
			var endSegmentOffset = endSegment.Offset;
			var charactersLeftInEndLine = endSegmentOffset + endSegment.TotalLength - (offset + length);
			endSegment.RemovedLinePart(ref deferredEventList, 0, endSegment.TotalLength - charactersLeftInEndLine);
			startSegment.MergedWith(endSegment, offset - startSegmentOffset);
			SetSegmentLength(startSegment, startSegment.TotalLength - charactersRemovedInStartLine + charactersLeftInEndLine);
			startSegment.DelimiterLength = endSegment.DelimiterLength;
			// remove all segments between startSegment (excl.) and endSegment (incl.)
			it.MoveNext();
			LineSegment segmentToRemove;
			do
			{
				segmentToRemove = it.Current;
				it.MoveNext();
				_lineCollection.RemoveSegment(segmentToRemove);
				segmentToRemove.Deleted(ref deferredEventList);
			} while (segmentToRemove != endSegment);
		}

		private void InsertInternal(int offset, string text)
		{
			var segment = _lineCollection.GetByOffset(offset);
			var ds = NextDelimiter(text, 0);
			if (ds == null)
			{
				// no newline is being inserted, all text is inserted in a single line
				segment.InsertedLinePart(offset - segment.Offset, text.Length);
				SetSegmentLength(segment, segment.TotalLength + text.Length);
				return;
			}
			var firstLine = segment;
			firstLine.InsertedLinePart(offset - firstLine.Offset, ds.Offset);
			var lastDelimiterEnd = 0;
			while (ds != null)
			{
				// split line segment at line delimiter
				var lineBreakOffset = offset + ds.Offset + ds.Length;
				var segmentOffset = segment.Offset;
				var lengthAfterInsertionPos = segmentOffset + segment.TotalLength - (offset + lastDelimiterEnd);
				_lineCollection.SetSegmentLength(segment, lineBreakOffset - segmentOffset);
				var newSegment = _lineCollection.InsertSegmentAfter(segment, lengthAfterInsertionPos);
				segment.DelimiterLength = ds.Length;

				segment = newSegment;
				lastDelimiterEnd = ds.Offset + ds.Length;

				ds = NextDelimiter(text, lastDelimiterEnd);
			}
			firstLine.SplitTo(segment);
			// insert rest after last delimiter
			if (lastDelimiterEnd != text.Length)
			{
				segment.InsertedLinePart(0, text.Length - lastDelimiterEnd);
				SetSegmentLength(segment, segment.TotalLength + text.Length - lastDelimiterEnd);
			}
		}

		private void SetSegmentLength(LineSegment segment, int newTotalLength)
		{
			var delta = newTotalLength - segment.TotalLength;
			if (delta != 0)
			{
				_lineCollection.SetSegmentLength(segment, newTotalLength);
				OnLineLengthChanged(new LineLengthChangeEventArgs(_document, segment, delta));
			}
		}

		private void RunHighlighter(int firstLine, int lineCount)
		{
			if (_highlightingStrategy != null)
			{
				var markLines = new List<LineSegment>();
				var it = _lineCollection.GetEnumeratorForIndex(firstLine);
				for (var i = 0; i < lineCount && it.IsValid; i++)
				{
					markLines.Add(it.Current);
					it.MoveNext();
				}
				_highlightingStrategy.MarkTokens(_document, markLines);
			}
		}

		public void SetContent(string text)
		{
			_lineCollection.Clear();
			if (text != null)
				Replace(0, 0, text);
		}

		public int GetVisibleLine(int logicalLineNumber)
		{
			if (!_document.TextEditorProperties.EnableFolding)
				return logicalLineNumber;

			var visibleLine = 0;
			var foldEnd = 0;
			var foldings = _document.FoldingManager.GetTopLevelFoldedFoldings();
			foreach (var fm in foldings)
			{
				if (fm.StartLine >= logicalLineNumber)
					break;
				if (fm.StartLine >= foldEnd)
				{
					visibleLine += fm.StartLine - foldEnd;
					if (fm.EndLine > logicalLineNumber)
						return visibleLine;
					foldEnd = fm.EndLine;
				}
			}
//			Debug.Assert(logicalLineNumber >= foldEnd);
			visibleLine += logicalLineNumber - foldEnd;
			return visibleLine;
		}

		public int GetFirstLogicalLine(int visibleLineNumber)
		{
			if (!_document.TextEditorProperties.EnableFolding)
				return visibleLineNumber;
			var v = 0;
			var foldEnd = 0;
			var foldings = _document.FoldingManager.GetTopLevelFoldedFoldings();
			foreach (var fm in foldings)
				if (fm.StartLine >= foldEnd)
				{
					if (v + fm.StartLine - foldEnd >= visibleLineNumber)
						break;
					v += fm.StartLine - foldEnd;
					foldEnd = fm.EndLine;
				}
			// help GC
			foldings.Clear();
			foldings = null;
			return foldEnd + visibleLineNumber - v;
		}

		public int GetLastLogicalLine(int visibleLineNumber)
		{
			if (!_document.TextEditorProperties.EnableFolding)
				return visibleLineNumber;
			return GetFirstLogicalLine(visibleLineNumber + 1) - 1;
		}

		// TODO : speedup the next/prev visible line search
		// HOW? : save the foldings in a sorted list and lookup the
		//        line numbers in this list
		public int GetNextVisibleLineAbove(int lineNumber, int lineCount)
		{
			var curLineNumber = lineNumber;
			if (_document.TextEditorProperties.EnableFolding)
				for (var i = 0; i < lineCount && curLineNumber < TotalNumberOfLines; ++i)
				{
					++curLineNumber;
					while (curLineNumber < TotalNumberOfLines &&
					       (curLineNumber >= _lineCollection.Count || !_document.FoldingManager.IsLineVisible(curLineNumber)))
						++curLineNumber;
				}
			else
				curLineNumber += lineCount;
			return Math.Min(TotalNumberOfLines - 1, curLineNumber);
		}

		public int GetNextVisibleLineBelow(int lineNumber, int lineCount)
		{
			var curLineNumber = lineNumber;
			if (_document.TextEditorProperties.EnableFolding)
				for (var i = 0; i < lineCount; ++i)
				{
					--curLineNumber;
					while (curLineNumber >= 0 && !_document.FoldingManager.IsLineVisible(curLineNumber))
						--curLineNumber;
				}
			else
				curLineNumber -= lineCount;
			return Math.Max(0, curLineNumber);
		}

		private DelimiterSegment NextDelimiter(string text, int offset)
		{
			for (var i = offset; i < text.Length; i++)
				switch (text[i])
				{
					case '\r':
						if (i + 1 < text.Length)
							if (text[i + 1] == '\n')
							{
								_delimiterSegment.Offset = i;
								_delimiterSegment.Length = 2;
								return _delimiterSegment;
							}
#if DATACONSISTENCYTEST
						Debug.Assert(false, "Found lone \\r, data consistency problems?");
#endif
						goto case '\n';
					case '\n':
						_delimiterSegment.Offset = i;
						_delimiterSegment.Length = 1;
						return _delimiterSegment;
				}
			return null;
		}

		private void OnLineCountChanged(LineCountChangeEventArgs e)
		{
			if (LineCountChanged != null)
				LineCountChanged(this, e);
		}

		private void OnLineLengthChanged(LineLengthChangeEventArgs e)
		{
			if (LineLengthChanged != null)
				LineLengthChanged(this, e);
		}

		private void OnLineDeleted(LineEventArgs e)
		{
			if (LineDeleted != null)
				LineDeleted(this, e);
		}

		public event EventHandler<LineLengthChangeEventArgs> LineLengthChanged;
		public event EventHandler<LineCountChangeEventArgs> LineCountChanged;
		public event EventHandler<LineEventArgs> LineDeleted;

		private sealed class DelimiterSegment
		{
			internal int Length;
			internal int Offset;
		}
	}
}