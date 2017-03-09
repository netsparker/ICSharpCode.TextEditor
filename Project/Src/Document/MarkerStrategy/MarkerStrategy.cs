// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;

namespace ICSharpCode.TextEditor.Document
{
	/// <summary>
	///     Manages the list of markers and provides ways to retrieve markers for specific positions.
	/// </summary>
	public sealed class MarkerStrategy
	{
		private readonly Dictionary<int, List<TextMarker>> _markersTable = new Dictionary<int, List<TextMarker>>();
		private readonly List<TextMarker> _textMarker = new List<TextMarker>();

		public MarkerStrategy(IDocument document)
		{
			Document = document;
			document.DocumentChanged += DocumentChanged;
		}

		public IDocument Document { get; }

		public IEnumerable<TextMarker> TextMarker => _textMarker.AsReadOnly();

		public void AddMarker(TextMarker item)
		{
			_markersTable.Clear();
			_textMarker.Add(item);
		}

		public void InsertMarker(int index, TextMarker item)
		{
			_markersTable.Clear();
			_textMarker.Insert(index, item);
		}

		public void RemoveMarker(TextMarker item)
		{
			_markersTable.Clear();
			_textMarker.Remove(item);
		}

		public void RemoveAll(Predicate<TextMarker> match)
		{
			_markersTable.Clear();
			_textMarker.RemoveAll(match);
		}

		public List<TextMarker> GetMarkers(int offset)
		{
			if (!_markersTable.ContainsKey(offset))
			{
				var markers = new List<TextMarker>();
				for (var i = 0; i < _textMarker.Count; ++i)
				{
					var marker = _textMarker[i];
					if (marker.Offset <= offset && offset <= marker.EndOffset)
						markers.Add(marker);
				}
				_markersTable[offset] = markers;
			}
			return _markersTable[offset];
		}

		public List<TextMarker> GetMarkers(int offset, int length)
		{
			var endOffset = offset + length - 1;
			var markers = new List<TextMarker>();
			for (var i = 0; i < _textMarker.Count; ++i)
			{
				var marker = _textMarker[i];
				if ( // start in marker region
					marker.Offset <= offset && offset <= marker.EndOffset ||
					// end in marker region
					marker.Offset <= endOffset && endOffset <= marker.EndOffset ||
					// marker start in region
					offset <= marker.Offset && marker.Offset <= endOffset ||
					// marker end in region
					offset <= marker.EndOffset && marker.EndOffset <= endOffset
				)
					markers.Add(marker);
			}
			return markers;
		}

		public List<TextMarker> GetMarkers(TextLocation position)
		{
			if (position.Y >= Document.TotalNumberOfLines || position.Y < 0)
				return new List<TextMarker>();
			var segment = Document.GetLineSegment(position.Y);
			return GetMarkers(segment.Offset + position.X);
		}

		private void DocumentChanged(object sender, DocumentEventArgs e)
		{
			// reset markers table
			_markersTable.Clear();
			Document.UpdateSegmentListOnDocumentChange(_textMarker, e);
		}
	}
}