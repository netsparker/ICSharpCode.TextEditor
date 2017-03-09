// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;

namespace ICSharpCode.TextEditor.Document
{
	/// <summary>
	///     A stack of Span instances. Works like Stack&lt;Span&gt;, but can be cloned quickly
	///     because it is implemented as linked list.
	/// </summary>
	public sealed class SpanStack : ICloneable, IEnumerable<Span>
	{
		private StackNode _top;

		public bool IsEmpty => _top == null;

		object ICloneable.Clone()
		{
			return Clone();
		}

		IEnumerator<Span> IEnumerable<Span>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public Span Pop()
		{
			var s = _top.Data;
			_top = _top.Previous;
			return s;
		}

		public Span Peek()
		{
			return _top.Data;
		}

		public void Push(Span s)
		{
			_top = new StackNode(_top, s);
		}

		public SpanStack Clone()
		{
			var n = new SpanStack();
			n._top = _top;
			return n;
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(new StackNode(_top, null));
		}

		internal sealed class StackNode
		{
			public readonly Span Data;
			public readonly StackNode Previous;

			public StackNode(StackNode previous, Span data)
			{
				Previous = previous;
				Data = data;
			}
		}

		public struct Enumerator : IEnumerator<Span>
		{
			private StackNode _c;

			internal Enumerator(StackNode node)
			{
				_c = node;
			}

			public Span Current => _c.Data;

			object IEnumerator.Current => _c.Data;

			public void Dispose()
			{
				_c = null;
			}

			public bool MoveNext()
			{
				_c = _c.Previous;
				return _c != null;
			}

			public void Reset()
			{
				throw new NotSupportedException();
			}
		}
	}
}