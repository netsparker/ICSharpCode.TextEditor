// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Text;
using System.Threading;

namespace ICSharpCode.TextEditor.Document
{
	public class GapTextBufferStrategy : ITextBufferStrategy
	{
		private const int MinGapLength = 128;
		private const int MaxGapLength = 2048;

		private char[] _buffer = new char[0];
		private string _cachedContent;

		private int _gapBeginOffset;
		private int _gapEndOffset;
		private int _gapLength; // gapLength == gapEndOffset - gapBeginOffset

		public int Length => _buffer.Length - _gapLength;

		public void SetContent(string text)
		{
			if (text == null)
				text = string.Empty;
			_cachedContent = text;
			_buffer = text.ToCharArray();
			_gapBeginOffset = _gapEndOffset = _gapLength = 0;
		}

		public char GetCharAt(int offset)
		{
#if DEBUG
			CheckThread();
#endif

			if (offset < 0 || offset >= Length)
				throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset < " + Length);

			return offset < _gapBeginOffset ? _buffer[offset] : _buffer[offset + _gapLength];
		}

		public string GetText(int offset, int length)
		{
#if DEBUG
			CheckThread();
#endif

			if (offset < 0 || offset > Length)
				throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset <= " + Length);
			if (length < 0 || offset + length > Length)
				throw new ArgumentOutOfRangeException("length", length,
					"0 <= length, offset(" + offset + ")+length <= " + Length);
			if (offset == 0 && length == Length)
			{
				if (_cachedContent != null)
					return _cachedContent;
				return _cachedContent = GetTextInternal(offset, length);
			}
			return GetTextInternal(offset, length);
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
			if (text == null)
				text = string.Empty;

#if DEBUG
			CheckThread();
#endif

			if (offset < 0 || offset > Length)
				throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset <= " + Length);
			if (length < 0 || offset + length > Length)
				throw new ArgumentOutOfRangeException("length", length, "0 <= length, offset+length <= " + Length);

			_cachedContent = null;

			// Math.Max is used so that if we need to resize the array
			// the new array has enough space for all old chars
			PlaceGap(offset, text.Length - length);
			_gapEndOffset += length; // delete removed text
			text.CopyTo(0, _buffer, _gapBeginOffset, text.Length);
			_gapBeginOffset += text.Length;
			_gapLength = _gapEndOffset - _gapBeginOffset;
			if (_gapLength > MaxGapLength)
				MakeNewBuffer(_gapBeginOffset, MinGapLength);
		}

		private string GetTextInternal(int offset, int length)
		{
			var end = offset + length;

			if (end < _gapBeginOffset)
				return new string(_buffer, offset, length);

			if (offset > _gapBeginOffset)
				return new string(_buffer, offset + _gapLength, length);

			var block1Size = _gapBeginOffset - offset;
			var block2Size = end - _gapBeginOffset;

			var buf = new StringBuilder(block1Size + block2Size);
			buf.Append(_buffer, offset, block1Size);
			buf.Append(_buffer, _gapEndOffset, block2Size);
			return buf.ToString();
		}

		private void PlaceGap(int newGapOffset, int minRequiredGapLength)
		{
			if (_gapLength < minRequiredGapLength)
			{
				// enlarge gap
				MakeNewBuffer(newGapOffset, minRequiredGapLength);
			}
			else
			{
				while (newGapOffset < _gapBeginOffset)
					_buffer[--_gapEndOffset] = _buffer[--_gapBeginOffset];
				while (newGapOffset > _gapBeginOffset)
					_buffer[_gapBeginOffset++] = _buffer[_gapEndOffset++];
			}
		}

		private void MakeNewBuffer(int newGapOffset, int newGapLength)
		{
			if (newGapLength < MinGapLength) newGapLength = MinGapLength;

			var newBuffer = new char[Length + newGapLength];
			if (newGapOffset < _gapBeginOffset)
			{
				// gap is moving backwards

				// first part:
				Array.Copy(_buffer, 0, newBuffer, 0, newGapOffset);
				// moving middle part:
				Array.Copy(_buffer, newGapOffset, newBuffer, newGapOffset + newGapLength, _gapBeginOffset - newGapOffset);
				// last part:
				Array.Copy(_buffer, _gapEndOffset, newBuffer, newBuffer.Length - (_buffer.Length - _gapEndOffset),
					_buffer.Length - _gapEndOffset);
			}
			else
			{
				// gap is moving forwards
				// first part:
				Array.Copy(_buffer, 0, newBuffer, 0, _gapBeginOffset);
				// moving middle part:
				Array.Copy(_buffer, _gapEndOffset, newBuffer, _gapBeginOffset, newGapOffset - _gapBeginOffset);
				// last part:
				var lastPartLength = newBuffer.Length - (newGapOffset + newGapLength);
				Array.Copy(_buffer, _buffer.Length - lastPartLength, newBuffer, newGapOffset + newGapLength, lastPartLength);
			}

			_gapBeginOffset = newGapOffset;
			_gapEndOffset = newGapOffset + newGapLength;
			_gapLength = newGapLength;
			_buffer = newBuffer;
		}

#if DEBUG
		private readonly int _creatorThread = Thread.CurrentThread.ManagedThreadId;

		private void CheckThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != _creatorThread)
				throw new InvalidOperationException("GapTextBufferStategy is not thread-safe!");
		}
#endif
	}
}