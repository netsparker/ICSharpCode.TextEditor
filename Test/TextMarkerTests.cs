// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.TextEditor.Document;
using NUnit.Framework;

namespace ICSharpCode.TextEditor.Tests
{
	[TestFixture]
	public class TextMarkerTests
	{
		[SetUp]
		public void SetUp()
		{
			_document = new DocumentFactory().CreateDocument();
			_document.TextContent = "0123456789";
			_marker = new TextMarker(3, 3, TextMarkerType.Underlined);
			_document.MarkerStrategy.AddMarker(_marker);
		}

		private IDocument _document;
		private TextMarker _marker;

		[Test]
		public void InsertTextAfterMarker()
		{
			_document.Insert(7, "ab");
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void InsertTextBeforeMarker()
		{
			_document.Insert(1, "ab");
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void InsertTextImmediatelyAfterMarker()
		{
			_document.Insert(6, "ab");
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void InsertTextImmediatelyBeforeMarker()
		{
			_document.Insert(3, "ab");
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void InsertTextInsideMarker()
		{
			_document.Insert(4, "ab");
			Assert.AreEqual("3ab45", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextAfterMarker()
		{
			_document.Remove(7, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextBeforeMarker()
		{
			_document.Remove(1, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextBeforeMarkerIntoMarker()
		{
			_document.Remove(2, 2);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("45", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextBeforeMarkerOverMarkerEnd()
		{
			_document.Remove(2, 5);
			Assert.AreEqual(0, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
		}

		[Test]
		public void RemoveTextBeforeMarkerUntilMarkerEnd()
		{
			_document.Remove(2, 4);
			Assert.AreEqual(0, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
		}

		[Test]
		public void RemoveTextFromMarkerStartIntoMarker()
		{
			_document.Remove(3, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("45", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextFromMarkerStartOverMarkerEnd()
		{
			_document.Remove(3, 4);
			Assert.AreEqual(0, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
		}

		[Test]
		public void RemoveTextFromMarkerStartUntilMarkerEnd()
		{
			_document.Remove(3, 3);
			Assert.AreEqual(0, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
		}

		[Test]
		public void RemoveTextImmediatelyAfterMarker()
		{
			_document.Remove(6, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextImmediatelyBeforeMarker()
		{
			_document.Remove(2, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("345", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextInsideMarker()
		{
			_document.Remove(4, 1);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("35", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextInsideMarkerOverMarkerEnd()
		{
			_document.Remove(4, 3);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("3", _document.GetText(_marker));
		}

		[Test]
		public void RemoveTextInsideMarkerUntilMarkerEnd()
		{
			_document.Remove(4, 2);
			Assert.AreEqual(1, _document.MarkerStrategy.GetMarkers(0, _document.TextLength).Count);
			Assert.AreEqual("3", _document.GetText(_marker));
		}
	}
}