// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;

namespace ICSharpCode.TextEditor.Util
{
	internal struct RedBlackTreeIterator<T> : IEnumerator<T>
	{
		internal RedBlackTreeNode<T> Node;

		internal RedBlackTreeIterator(RedBlackTreeNode<T> node)
		{
			Node = node;
		}

		public bool IsValid => Node != null;

		public T Current
		{
			get
			{
				if (Node != null)
					return Node.Val;
				throw new InvalidOperationException();
			}
		}

		object IEnumerator.Current => Current;

		void IDisposable.Dispose()
		{
		}

		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		public bool MoveNext()
		{
			if (Node == null)
				return false;
			if (Node.Right != null)
			{
				Node = Node.Right.LeftMost;
			}
			else
			{
				RedBlackTreeNode<T> oldNode;
				do
				{
					oldNode = Node;
					Node = Node.Parent;
					// we are on the way up from the right part, don't output node again
				} while (Node != null && Node.Right == oldNode);
			}
			return Node != null;
		}

		public bool MoveBack()
		{
			if (Node == null)
				return false;
			if (Node.Left != null)
			{
				Node = Node.Left.RightMost;
			}
			else
			{
				RedBlackTreeNode<T> oldNode;
				do
				{
					oldNode = Node;
					Node = Node.Parent;
					// we are on the way up from the left part, don't output node again
				} while (Node != null && Node.Left == oldNode);
			}
			return Node != null;
		}
	}
}