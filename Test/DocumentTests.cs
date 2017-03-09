// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.TextEditor.Document;
using NUnit.Framework;

namespace ICSharpCode.TextEditor.Tests
{
	[TestFixture]
	public class DocumentAggregatorTests
	{
		[Test]
		public void TestDocumentBug1Test()
		{
			var document = new DocumentFactory().CreateDocument();

			var top = "1234567890";
			document.TextContent = top;

			Assert.AreEqual(document.GetLineSegment(0).Length, document.TextLength);

			document.Remove(0, document.TextLength);

			var line = document.GetLineSegment(0);
			Assert.AreEqual(0, line.Offset);
			Assert.AreEqual(0, line.Length);
			Assert.AreEqual(0, document.TextLength);
			Assert.AreEqual(1, document.TotalNumberOfLines);
		}

		[Test]
		public void TestDocumentBug2Test()
		{
			var document = new DocumentFactory().CreateDocument();

			var top = "123\n456\n789\n0";
			var testText = "Hello World!";

			document.TextContent = top;

			document.Insert(top.Length, testText);

			var line = document.GetLineSegment(document.TotalNumberOfLines - 1);

			Assert.AreEqual(top.Length - 1, line.Offset);
			Assert.AreEqual(testText.Length + 1, line.Length);
		}

		[Test]
		public void TestDocumentGenerationTest()
		{
			var document = new DocumentFactory().CreateDocument();
		}

		[Test]
		public void TestDocumentInsertTest()
		{
			var document = new DocumentFactory().CreateDocument();

			var top = "1234567890\n";
			var testText =
				"12345678\n" +
				"1234567\n" +
				"123456\n" +
				"12345\n" +
				"1234\n" +
				"123\n" +
				"12\n" +
				"1\n" +
				"\n";

			document.TextContent = top;
			document.Insert(top.Length, testText);
			Assert.AreEqual(top + testText, document.TextContent);
		}

		[Test]
		public void TestDocumentRemoveStoreTest()
		{
			var document = new DocumentFactory().CreateDocument();

			var top = "1234567890\n";
			var testText =
				"12345678\n" +
				"1234567\n" +
				"123456\n" +
				"12345\n" +
				"1234\n" +
				"123\n" +
				"12\n" +
				"1\n" +
				"\n";
			document.TextContent = top + testText;
			document.Remove(0, top.Length);
			Assert.AreEqual(document.TextContent, testText);

			document.Remove(0, document.TextLength);
			var line = document.GetLineSegment(0);
			Assert.AreEqual(0, line.Offset);
			Assert.AreEqual(0, line.Length);
			Assert.AreEqual(0, document.TextLength);
			Assert.AreEqual(1, document.TotalNumberOfLines);
		}

		[Test]
		public void TestDocumentStoreTest()
		{
			var document = new DocumentFactory().CreateDocument();

			var testText = "1234567890\n" +
			               "12345678\n" +
			               "1234567\n" +
			               "123456\n" +
			               "12345\n" +
			               "1234\n" +
			               "123\n" +
			               "12\n" +
			               "1\n" +
			               "\n";
			document.TextContent = testText;

			Assert.AreEqual(testText, document.TextContent);
			Assert.AreEqual(11, document.TotalNumberOfLines);
			Assert.AreEqual(testText.Length, document.TextLength);
		}

//			string testText = "123\r\n";
//			
//			IDocument document = new DocumentFactory().CreateDocument();
//		{
//		public void TestDocumentBug3Test()

//		[Test]
//			
//			for (int i = 0; i < 5; ++i) {
//				document.Insert(document.TextLength, testText);
//			}
//			
//			document.Caret.Offset = testText.Length * 2 + 1;
//			document.Remove(testText.Length * 2, 2);
//			
//			Assert.AreEqual(testText.Length * 2, document.Caret.Offset);
//		}

//		[Test]
//		public void TestDocumentBug4Test()
//		{
//			IDocument document = new DocumentFactory().CreateDocument();
//			
//			string testText = "123\r\n";
//			
//			for (int i = 0; i < 5; ++i) {
//				document.Insert(document.TextLength, testText);
//			}
//			
//			document.Caret.Offset = testText.Length * 2 + 1;
//			document.Replace(testText.Length * 2, 2, "");
//			
//			Assert.AreEqual(testText.Length * 2, document.Caret.Offset);
//		}
//		
//		[Test]
//		public void TestDocumentBug5Test()
//		{
//			IDocument document = new DocumentFactory().CreateDocument();
//			
//			for (int i = 3; i <= 5; ++i) {
//				document.TextContent  = "abcdefgh";
//				document.Caret.Offset = i;
//				
//				document.Replace(2, 3, "Hello");
//				
//				Assert.AreEqual(i, document.Caret.Offset);
//			}
//		}
	}
}