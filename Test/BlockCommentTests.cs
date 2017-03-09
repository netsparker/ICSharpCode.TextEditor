// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.TextEditor.Actions;
using ICSharpCode.TextEditor.Document;
using NUnit.Framework;

namespace ICSharpCode.TextEditor.Tests
{
	[TestFixture]
	public class BlockCommentTests
	{
		[SetUp]
		public void Init()
		{
			_document = new DocumentFactory().CreateDocument();
			_document.HighlightingStrategy = HighlightingManager.Manager.FindHighlighter("XML");
		}

		private IDocument _document;
		private readonly string _commentStart = "<!--";
		private readonly string _commentEnd = "-->";

		[Test]
		public void CaretInsideCommentButNoSelectedText()
		{
			_document.TextContent = "<!---->";
			var selectionStartOffset = 4;
			var selectionEndOffset = 4;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 4);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void CursorJustOutsideCommentEnd()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 8;
			var selectionEndOffset = 8;

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.IsNull(commentRegion);
		}

		[Test]
		public void CursorJustOutsideCommentStart()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 0;
			var selectionEndOffset = 0;

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.IsNull(commentRegion);
		}

		[Test]
		public void EntireCommentAndExtraTextSelected()
		{
			_document.TextContent = "a<!-- -->";
			var selectionStartOffset = 0;
			var selectionEndOffset = 9;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 1, 6);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void EntireCommentSelected()
		{
			_document.TextContent = "<!---->";
			var selectionStartOffset = 0;
			var selectionEndOffset = 7;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 4);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void FirstCharacterOfCommentStartSelected()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 0;
			var selectionEndOffset = 1;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 5);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void LastCharacterOfCommentEndSelected()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 7;
			var selectionEndOffset = 8;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 5);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void NoTextSelected()
		{
			_document.TextContent = string.Empty;
			var selectionStartOffset = 0;
			var selectionEndOffset = 0;

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.IsNull(commentRegion, "Should not be a comment region for an empty document");
		}

		[Test]
		public void OnlyCommentEndSelected()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 5;
			var selectionEndOffset = 8;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 5);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void OnlyCommentStartSelected()
		{
			_document.TextContent = "<!-- -->";
			var selectionStartOffset = 0;
			var selectionEndOffset = 4;
			var expectedCommentRegion = new BlockCommentRegion(_commentStart, _commentEnd, 0, 5);

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.AreEqual(expectedCommentRegion, commentRegion);
		}

		[Test]
		public void TwoExistingBlockComments()
		{
			_document.TextContent = "<a>\r\n" +
			                        "<!--<b></b>-->\r\n" +
			                        "\t<c></c>\r\n" +
			                        "<!--<d></d>-->\r\n" +
			                        "</a>";

			var selectedText = "<c></c>";
			var selectionStartOffset = _document.TextContent.IndexOf(selectedText);
			var selectionEndOffset = selectionStartOffset + selectedText.Length;

			var commentRegion = ToggleBlockComment.FindSelectedCommentRegion(_document, _commentStart, _commentEnd,
				selectionStartOffset, selectionEndOffset);
			Assert.IsNull(commentRegion);
		}
	}
}