/*
 * Created by SharpDevelop.
 * User: Daniel Grunwald
 * Date: 10/28/2006
 * Time: 8:42 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System.Collections.Generic;
using System.Text;
using ICSharpCode.TextEditor.Document;
using NUnit.Framework;

namespace ICSharpCode.TextEditor.Tests
{
	[TestFixture]
	public class FoldingManagerTests
	{
		[SetUp]
		public void SetUp()
		{
			var doc = new DocumentFactory().CreateDocument();
			var b = new StringBuilder();
			for (var i = 0; i < 50; i++)
				b.AppendLine(new string('a', 50));
			doc.TextContent = b.ToString();
			_list = new List<FoldMarker>();
			_list.Add(new FoldMarker(doc, 1, 6, 5, 2));
			_list.Add(new FoldMarker(doc, 2, 1, 2, 3));
			_list.Add(new FoldMarker(doc, 3, 7, 4, 1));
			_list.Add(new FoldMarker(doc, 10, 1, 14, 1));
			_list.Add(new FoldMarker(doc, 10, 3, 10, 3));
			_list.Add(new FoldMarker(doc, 11, 1, 15, 1));
			_list.Add(new FoldMarker(doc, 12, 1, 16, 1));
			foreach (var fm in _list)
				fm.IsFolded = true;
			doc.FoldingManager.UpdateFoldings(new List<FoldMarker>(_list));
			_manager = doc.FoldingManager;
		}

		private FoldingManager _manager;
		private List<FoldMarker> _list;

		private void AssertPosition(int line, int column, params int[] markers)
		{
			AssertList(_manager.GetFoldingsFromPosition(line, column), markers);
		}

		private void AssertList(List<FoldMarker> l, params int[] markers)
		{
			Assert.AreEqual(markers.Length, l.Count);
			foreach (var m in markers)
				Assert.Contains(_list[m], l);
		}

		[Test]
		public void GetFoldingsContainsLineNumber()
		{
			AssertList(_manager.GetFoldingsContainsLineNumber(1));
			AssertList(_manager.GetFoldingsContainsLineNumber(2), 0);
			AssertList(_manager.GetFoldingsContainsLineNumber(3), 0);
			AssertList(_manager.GetFoldingsContainsLineNumber(4), 0);
			AssertList(_manager.GetFoldingsContainsLineNumber(5));
			AssertList(_manager.GetFoldingsContainsLineNumber(10));
			AssertList(_manager.GetFoldingsContainsLineNumber(11), 3);
			AssertList(_manager.GetFoldingsContainsLineNumber(12), 3, 5);
			AssertList(_manager.GetFoldingsContainsLineNumber(13), 3, 5, 6);
			AssertList(_manager.GetFoldingsContainsLineNumber(14), 5, 6);
			AssertList(_manager.GetFoldingsContainsLineNumber(15), 6);
			AssertList(_manager.GetFoldingsContainsLineNumber(16));
		}

		[Test]
		public void GetFoldingsWithStart()
		{
			AssertList(_manager.GetFoldingsWithStart(1), 0);
			AssertList(_manager.GetFoldingsWithStart(2), 1);
			AssertList(_manager.GetFoldingsWithStart(3), 2);
			AssertList(_manager.GetFoldingsWithStart(4));
			AssertList(_manager.GetFoldingsWithStart(10), 3, 4);
			AssertList(_manager.GetFoldingsWithStart(11), 5);
			AssertList(_manager.GetFoldingsWithStart(12), 6);
			AssertList(_manager.GetFoldingsWithStart(13));
			AssertList(_manager.GetFoldingsWithStart(14));
			AssertList(_manager.GetFoldedFoldingsWithStartAfterColumn(10, 0), 3, 4);
			AssertList(_manager.GetFoldedFoldingsWithStartAfterColumn(10, 1), 4);
			AssertList(_manager.GetFoldedFoldingsWithStartAfterColumn(10, 2), 4);
			AssertList(_manager.GetFoldedFoldingsWithStartAfterColumn(10, 3));
			AssertList(_manager.GetFoldedFoldingsWithStartAfterColumn(10, 4));
		}

		[Test]
		public void GetFromPositionOverlapping()
		{
			AssertPosition(10, 1);
			AssertPosition(10, 2, 3);
			AssertPosition(10, 3, 3);
			AssertPosition(10, 4, 3);
			AssertPosition(11, 1, 3);
			AssertPosition(11, 2, 3, 5);
			AssertPosition(12, 1, 3, 5);
			AssertPosition(12, 2, 3, 5, 6);
			AssertPosition(14, 0, 3, 5, 6);
			AssertPosition(14, 1, 5, 6);
			AssertPosition(15, 0, 5, 6);
			AssertPosition(15, 1, 6);
			AssertPosition(16, 0, 6);
			AssertPosition(16, 1);
		}

		[Test]
		public void GetFromPositionTest()
		{
			AssertPosition(1, 5);
			//AssertPosition(1, 6,  0);
			AssertPosition(1, 7, 0);
			AssertPosition(5, 0, 0);
			AssertPosition(5, 1, 0);
			AssertPosition(5, 2);
			AssertPosition(5, 3);
			AssertPosition(3, 8, 0, 2);
			AssertPosition(3, 30, 0, 2);
			AssertPosition(4, 0, 0, 2);
			AssertPosition(4, 1, 0);
			AssertPosition(2, 1, 0);
			AssertPosition(2, 2, 0, 1);
			AssertPosition(2, 3, 0);
		}

		[Test]
		public void GetTopLevelFoldedFoldings()
		{
			AssertList(_manager.GetTopLevelFoldedFoldings(), 0, 3);
		}
	}
}