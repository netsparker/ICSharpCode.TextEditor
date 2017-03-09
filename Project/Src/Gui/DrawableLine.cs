// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     A class that is able to draw a line on any control (outside the text editor)
	/// </summary>
	public class DrawableLine
	{
		private static readonly StringFormat _sf = (StringFormat) StringFormat.GenericTypographic.Clone();
		private readonly Font _boldMonospacedFont;
		private readonly Font _monospacedFont;
		private SizeF _spaceSize;

		private readonly List<SimpleTextWord> _words = new List<SimpleTextWord>();

		public DrawableLine(IDocument document, LineSegment line, Font monospacedFont, Font boldMonospacedFont)
		{
			_monospacedFont = monospacedFont;
			_boldMonospacedFont = boldMonospacedFont;
			if (line.Words != null)
				foreach (var word in line.Words)
					if (word.Type == TextWordType.Space)
						_words.Add(SimpleTextWord.Space);
					else if (word.Type == TextWordType.Tab)
						_words.Add(SimpleTextWord.Tab);
					else
						_words.Add(new SimpleTextWord(TextWordType.Word, word.Word, word.Bold, word.Color));
			else
				_words.Add(new SimpleTextWord(TextWordType.Word, document.GetText(line), false, Color.Black));
		}

		public int LineLength
		{
			get
			{
				var length = 0;
				foreach (var word in _words)
					length += word.Word.Length;
				return length;
			}
		}

		public void SetBold(int startIndex, int endIndex, bool bold)
		{
			if (startIndex < 0)
				throw new ArgumentException("startIndex must be >= 0");
			if (startIndex > endIndex)
				throw new ArgumentException("startIndex must be <= endIndex");
			if (startIndex == endIndex) return;
			var pos = 0;
			for (var i = 0; i < _words.Count; i++)
			{
				var word = _words[i];
				if (pos >= endIndex)
					break;
				var wordEnd = pos + word.Word.Length;
				// 3 possibilities:
				if (startIndex <= pos && endIndex >= wordEnd)
				{
					// word is fully in region:
					word.Bold = bold;
				}
				else if (startIndex <= pos)
				{
					// beginning of word is in region
					var inRegionLength = endIndex - pos;
					var newWord = new SimpleTextWord(word.Type, word.Word.Substring(inRegionLength), word.Bold, word.Color);
					_words.Insert(i + 1, newWord);

					word.Bold = bold;
					word.Word = word.Word.Substring(0, inRegionLength);
				}
				else if (startIndex < wordEnd)
				{
					// end of word is in region (or middle of word is in region)
					var notInRegionLength = startIndex - pos;

					var newWord = new SimpleTextWord(word.Type, word.Word.Substring(notInRegionLength), word.Bold,
						word.Color);
					// newWord.Bold will be set in the next iteration
					_words.Insert(i + 1, newWord);

					word.Word = word.Word.Substring(0, notInRegionLength);
				}
				pos = wordEnd;
			}
		}

		public static float DrawDocumentWord(Graphics g, string word, PointF position, Font font, Color foreColor)
		{
			if (word == null || word.Length == 0)
				return 0f;
			var wordSize = g.MeasureString(word, font, 32768, _sf);

			g.DrawString(word,
				font,
				BrushRegistry.GetBrush(foreColor),
				position,
				_sf);
			return wordSize.Width;
		}

		public SizeF GetSpaceSize(Graphics g)
		{
			if (_spaceSize.IsEmpty)
				_spaceSize = g.MeasureString("-", _boldMonospacedFont, new PointF(0, 0), _sf);
			return _spaceSize;
		}

		public void DrawLine(Graphics g, ref float xPos, float xOffset, float yPos, Color c)
		{
			var spaceSize = GetSpaceSize(g);
			foreach (var word in _words)
				switch (word.Type)
				{
					case TextWordType.Space:
						xPos += spaceSize.Width;
						break;
					case TextWordType.Tab:
						var tabWidth = spaceSize.Width * 4;
						xPos += tabWidth;
						xPos = (int) ((xPos + 2) / tabWidth) * tabWidth;
						break;
					case TextWordType.Word:
						xPos += DrawDocumentWord(g,
							word.Word,
							new PointF(xPos + xOffset, yPos),
							word.Bold ? _boldMonospacedFont : _monospacedFont,
							c == Color.Empty ? word.Color : c
						);
						break;
				}
		}

		public void DrawLine(Graphics g, ref float xPos, float xOffset, float yPos)
		{
			DrawLine(g, ref xPos, xOffset, yPos, Color.Empty);
		}

		public float MeasureWidth(Graphics g, float xPos)
		{
			var spaceSize = GetSpaceSize(g);
			foreach (var word in _words)
				switch (word.Type)
				{
					case TextWordType.Space:
						xPos += spaceSize.Width;
						break;
					case TextWordType.Tab:
						var tabWidth = spaceSize.Width * 4;
						xPos += tabWidth;
						xPos = (int) ((xPos + 2) / tabWidth) * tabWidth;
						break;
					case TextWordType.Word:
						if (word.Word != null && word.Word.Length > 0)
							xPos += g.MeasureString(word.Word, word.Bold ? _boldMonospacedFont : _monospacedFont, 32768, _sf).Width;
						break;
				}
			return xPos;
		}

		private class SimpleTextWord
		{
			internal static readonly SimpleTextWord Space = new SimpleTextWord(TextWordType.Space, " ", false, Color.Black);
			internal static readonly SimpleTextWord Tab = new SimpleTextWord(TextWordType.Tab, "\t", false, Color.Black);
			internal bool Bold;
			internal readonly Color Color;
			internal readonly TextWordType Type;
			internal string Word;

			public SimpleTextWord(TextWordType type, string word, bool bold, Color color)
			{
				Type = type;
				Word = word;
				Bold = bold;
				Color = color;
			}
		}
	}
}