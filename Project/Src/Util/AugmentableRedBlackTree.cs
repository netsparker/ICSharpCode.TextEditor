// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ICSharpCode.TextEditor.Util
{
	internal sealed class RedBlackTreeNode<T>
	{
		internal bool Color;
		internal RedBlackTreeNode<T> Left, Right, Parent;
		internal T Val;

		internal RedBlackTreeNode(T val)
		{
			Val = val;
		}

		internal RedBlackTreeNode<T> LeftMost
		{
			get
			{
				var node = this;
				while (node.Left != null)
					node = node.Left;
				return node;
			}
		}

		internal RedBlackTreeNode<T> RightMost
		{
			get
			{
				var node = this;
				while (node.Right != null)
					node = node.Right;
				return node;
			}
		}
	}

	internal interface IRedBlackTreeHost<T> : IComparer<T>
	{
		bool Equals(T a, T b);

		void UpdateAfterChildrenChange(RedBlackTreeNode<T> node);
		void UpdateAfterRotateLeft(RedBlackTreeNode<T> node);
		void UpdateAfterRotateRight(RedBlackTreeNode<T> node);
	}

	/// <summary>
	///     Description of RedBlackTree.
	/// </summary>
	internal sealed class AugmentableRedBlackTree<T, THost> : ICollection<T> where THost : IRedBlackTreeHost<T>
	{
		private readonly THost _host;
		internal RedBlackTreeNode<T> Root;

		public AugmentableRedBlackTree(THost host)
		{
			if (host == null) throw new ArgumentNullException("host");
			_host = host;
		}

		public int Count { get; private set; }

		public void Clear()
		{
			Root = null;
			Count = 0;
		}

		#region Debugging code

#if DEBUG
		/// <summary>
		///     Check tree for consistency and being balanced.
		/// </summary>
		[Conditional("DATACONSISTENCYTEST")]
		private void CheckProperties()
		{
			var blackCount = -1;
			CheckNodeProperties(Root, null, Red, 0, ref blackCount);

			var nodeCount = 0;
			foreach (var val in this)
				nodeCount++;
			Debug.Assert(Count == nodeCount);
		}

		/*
		1. A node is either red or black.
		2. The root is black.
		3. All leaves are black. (The leaves are the NIL children.)
		4. Both children of every red node are black. (So every red node must have a black parent.)
		5. Every simple path from a node to a descendant leaf contains the same number of black nodes. (Not counting the leaf node.)
		 */

		private void CheckNodeProperties(RedBlackTreeNode<T> node, RedBlackTreeNode<T> parentNode, bool parentColor,
			int blackCount,
			ref int expectedBlackCount)
		{
			if (node == null) return;

			Debug.Assert(node.Parent == parentNode);

			if (parentColor == Red)
				Debug.Assert(node.Color == Black);
			if (node.Color == Black)
				blackCount++;
			if (node.Left == null && node.Right == null)
				if (expectedBlackCount == -1)
					expectedBlackCount = blackCount;
				else
					Debug.Assert(expectedBlackCount == blackCount);
			CheckNodeProperties(node.Left, node, node.Color, blackCount, ref expectedBlackCount);
			CheckNodeProperties(node.Right, node, node.Color, blackCount, ref expectedBlackCount);
		}

		public string GetTreeAsString()
		{
			var b = new StringBuilder();
			AppendTreeToString(Root, b, 0);
			return b.ToString();
		}

		private static void AppendTreeToString(RedBlackTreeNode<T> node, StringBuilder b, int indent)
		{
			if (node.Color == Red)
				b.Append("RED   ");
			else
				b.Append("BLACK ");
			b.AppendLine(node.Val.ToString());
			indent += 2;
			if (node.Left != null)
			{
				b.Append(' ', indent);
				b.Append("L: ");
				AppendTreeToString(node.Left, b, indent);
			}
			if (node.Right != null)
			{
				b.Append(' ', indent);
				b.Append("R: ");
				AppendTreeToString(node.Right, b, indent);
			}
		}
#endif

		#endregion

		#region Add

		public void Add(T item)
		{
			AddInternal(new RedBlackTreeNode<T>(item));
#if DEBUG
			CheckProperties();
#endif
		}

		private void AddInternal(RedBlackTreeNode<T> newNode)
		{
			Debug.Assert(newNode.Color == Black);
			if (Root == null)
			{
				Count = 1;
				Root = newNode;
				return;
			}
			// Insert into the tree
			var parentNode = Root;
			while (true)
				if (_host.Compare(newNode.Val, parentNode.Val) <= 0)
				{
					if (parentNode.Left == null)
					{
						InsertAsLeft(parentNode, newNode);
						return;
					}
					parentNode = parentNode.Left;
				}
				else
				{
					if (parentNode.Right == null)
					{
						InsertAsRight(parentNode, newNode);
						return;
					}
					parentNode = parentNode.Right;
				}
		}

		internal void InsertAsLeft(RedBlackTreeNode<T> parentNode, RedBlackTreeNode<T> newNode)
		{
			Debug.Assert(parentNode.Left == null);
			parentNode.Left = newNode;
			newNode.Parent = parentNode;
			newNode.Color = Red;
			_host.UpdateAfterChildrenChange(parentNode);
			FixTreeOnInsert(newNode);
			Count++;
		}

		internal void InsertAsRight(RedBlackTreeNode<T> parentNode, RedBlackTreeNode<T> newNode)
		{
			Debug.Assert(parentNode.Right == null);
			parentNode.Right = newNode;
			newNode.Parent = parentNode;
			newNode.Color = Red;
			_host.UpdateAfterChildrenChange(parentNode);
			FixTreeOnInsert(newNode);
			Count++;
		}

		private void FixTreeOnInsert(RedBlackTreeNode<T> node)
		{
			Debug.Assert(node != null);
			Debug.Assert(node.Color == Red);
			Debug.Assert(node.Left == null || node.Left.Color == Black);
			Debug.Assert(node.Right == null || node.Right.Color == Black);

			var parentNode = node.Parent;
			if (parentNode == null)
			{
				// we inserted in the root -> the node must be black
				// since this is a root node, making the node black increments the number of black nodes
				// on all paths by one, so it is still the same for all paths.
				node.Color = Black;
				return;
			}
			if (parentNode.Color == Black)
				return;
			// parentNode is red, so there is a conflict here!

			// because the root is black, parentNode is not the root -> there is a grandparent node
			var grandparentNode = parentNode.Parent;
			var uncleNode = Sibling(parentNode);
			if (uncleNode != null && uncleNode.Color == Red)
			{
				parentNode.Color = Black;
				uncleNode.Color = Black;
				grandparentNode.Color = Red;
				FixTreeOnInsert(grandparentNode);
				return;
			}
			// now we know: parent is red but uncle is black
			// First rotation:
			if (node == parentNode.Right && parentNode == grandparentNode.Left)
			{
				RotateLeft(parentNode);
				node = node.Left;
			}
			else if (node == parentNode.Left && parentNode == grandparentNode.Right)
			{
				RotateRight(parentNode);
				node = node.Right;
			}
			// because node might have changed, reassign variables:
			parentNode = node.Parent;
			grandparentNode = parentNode.Parent;

			// Now recolor a bit:
			parentNode.Color = Black;
			grandparentNode.Color = Red;
			// Second rotation:
			if (node == parentNode.Left && parentNode == grandparentNode.Left)
			{
				RotateRight(grandparentNode);
			}
			else
			{
				// because of the first rotation, this is guaranteed:
				Debug.Assert(node == parentNode.Right && parentNode == grandparentNode.Right);
				RotateLeft(grandparentNode);
			}
		}

		private void ReplaceNode(RedBlackTreeNode<T> replacedNode, RedBlackTreeNode<T> newNode)
		{
			if (replacedNode.Parent == null)
			{
				Debug.Assert(replacedNode == Root);
				Root = newNode;
			}
			else
			{
				if (replacedNode.Parent.Left == replacedNode)
					replacedNode.Parent.Left = newNode;
				else
					replacedNode.Parent.Right = newNode;
			}
			if (newNode != null)
				newNode.Parent = replacedNode.Parent;
			replacedNode.Parent = null;
		}

		private void RotateLeft(RedBlackTreeNode<T> p)
		{
			// let q be p's right child
			var q = p.Right;
			Debug.Assert(q != null);
			Debug.Assert(q.Parent == p);
			// set q to be the new root
			ReplaceNode(p, q);

			// set p's right child to be q's left child
			p.Right = q.Left;
			if (p.Right != null) p.Right.Parent = p;
			// set q's left child to be p
			q.Left = p;
			p.Parent = q;
			_host.UpdateAfterRotateLeft(p);
		}

		private void RotateRight(RedBlackTreeNode<T> p)
		{
			// let q be p's left child
			var q = p.Left;
			Debug.Assert(q != null);
			Debug.Assert(q.Parent == p);
			// set q to be the new root
			ReplaceNode(p, q);

			// set p's left child to be q's right child
			p.Left = q.Right;
			if (p.Left != null) p.Left.Parent = p;
			// set q's right child to be p
			q.Right = p;
			p.Parent = q;
			_host.UpdateAfterRotateRight(p);
		}

		private RedBlackTreeNode<T> Sibling(RedBlackTreeNode<T> node)
		{
			if (node == node.Parent.Left)
				return node.Parent.Right;
			return node.Parent.Left;
		}

		#endregion

		#region Remove

		public void RemoveAt(RedBlackTreeIterator<T> iterator)
		{
			var node = iterator.Node;
			if (node == null)
				throw new ArgumentException("Invalid iterator");
			while (node.Parent != null)
				node = node.Parent;
			if (node != Root)
				throw new ArgumentException("Iterator does not belong to this tree");
			RemoveNode(iterator.Node);
#if DEBUG
			CheckProperties();
#endif
		}

		internal void RemoveNode(RedBlackTreeNode<T> removedNode)
		{
			if (removedNode.Left != null && removedNode.Right != null)
			{
				// replace removedNode with it's in-order successor

				var leftMost = removedNode.Right.LeftMost;
				RemoveNode(leftMost); // remove leftMost from its current location

				// and overwrite the removedNode with it
				ReplaceNode(removedNode, leftMost);
				leftMost.Left = removedNode.Left;
				if (leftMost.Left != null) leftMost.Left.Parent = leftMost;
				leftMost.Right = removedNode.Right;
				if (leftMost.Right != null) leftMost.Right.Parent = leftMost;
				leftMost.Color = removedNode.Color;

				_host.UpdateAfterChildrenChange(leftMost);
				if (leftMost.Parent != null) _host.UpdateAfterChildrenChange(leftMost.Parent);
				return;
			}

			Count--;

			// now either removedNode.left or removedNode.right is null
			// get the remaining child
			var parentNode = removedNode.Parent;
			var childNode = removedNode.Left ?? removedNode.Right;
			ReplaceNode(removedNode, childNode);
			if (parentNode != null) _host.UpdateAfterChildrenChange(parentNode);
			if (removedNode.Color == Black)
				if (childNode != null && childNode.Color == Red)
					childNode.Color = Black;
				else
					FixTreeOnDelete(childNode, parentNode);
		}

		private static RedBlackTreeNode<T> Sibling(RedBlackTreeNode<T> node, RedBlackTreeNode<T> parentNode)
		{
			Debug.Assert(node == null || node.Parent == parentNode);
			if (node == parentNode.Left)
				return parentNode.Right;
			return parentNode.Left;
		}

		private const bool Red = true;
		private const bool Black = false;

		private static bool GetColor(RedBlackTreeNode<T> node)
		{
			return node != null ? node.Color : Black;
		}

		private void FixTreeOnDelete(RedBlackTreeNode<T> node, RedBlackTreeNode<T> parentNode)
		{
			Debug.Assert(node == null || node.Parent == parentNode);
			if (parentNode == null)
				return;

			// warning: node may be null
			var sibling = Sibling(node, parentNode);
			if (sibling.Color == Red)
			{
				parentNode.Color = Red;
				sibling.Color = Black;
				if (node == parentNode.Left)
					RotateLeft(parentNode);
				else
					RotateRight(parentNode);

				sibling = Sibling(node, parentNode); // update value of sibling after rotation
			}

			if (parentNode.Color == Black
			    && sibling.Color == Black
			    && GetColor(sibling.Left) == Black
			    && GetColor(sibling.Right) == Black)
			{
				sibling.Color = Red;
				FixTreeOnDelete(parentNode, parentNode.Parent);
				return;
			}

			if (parentNode.Color == Red
			    && sibling.Color == Black
			    && GetColor(sibling.Left) == Black
			    && GetColor(sibling.Right) == Black)
			{
				sibling.Color = Red;
				parentNode.Color = Black;
				return;
			}

			if (node == parentNode.Left &&
			    sibling.Color == Black &&
			    GetColor(sibling.Left) == Red &&
			    GetColor(sibling.Right) == Black)
			{
				sibling.Color = Red;
				sibling.Left.Color = Black;
				RotateRight(sibling);
			}
			else if (node == parentNode.Right &&
			         sibling.Color == Black &&
			         GetColor(sibling.Right) == Red &&
			         GetColor(sibling.Left) == Black)
			{
				sibling.Color = Red;
				sibling.Right.Color = Black;
				RotateLeft(sibling);
			}
			sibling = Sibling(node, parentNode); // update value of sibling after rotation

			sibling.Color = parentNode.Color;
			parentNode.Color = Black;
			if (node == parentNode.Left)
			{
				if (sibling.Right != null)
				{
					Debug.Assert(sibling.Right.Color == Red);
					sibling.Right.Color = Black;
				}
				RotateLeft(parentNode);
			}
			else
			{
				if (sibling.Left != null)
				{
					Debug.Assert(sibling.Left.Color == Red);
					sibling.Left.Color = Black;
				}
				RotateRight(parentNode);
			}
		}

		#endregion

		#region Find/LowerBound/UpperBound/GetEnumerator

		/// <summary>
		///     Returns the iterator pointing to the specified item, or an iterator in End state if the item is not found.
		/// </summary>
		public RedBlackTreeIterator<T> Find(T item)
		{
			var it = LowerBound(item);
			while (it.IsValid && _host.Compare(it.Current, item) == 0)
			{
				if (_host.Equals(it.Current, item))
					return it;
				it.MoveNext();
			}
			return default(RedBlackTreeIterator<T>);
		}

		/// <summary>
		///     Returns the iterator pointing to the first item greater or equal to <paramref name="item" />.
		/// </summary>
		public RedBlackTreeIterator<T> LowerBound(T item)
		{
			var node = Root;
			RedBlackTreeNode<T> resultNode = null;
			while (node != null)
				if (_host.Compare(node.Val, item) < 0)
				{
					node = node.Right;
				}
				else
				{
					resultNode = node;
					node = node.Left;
				}
			return new RedBlackTreeIterator<T>(resultNode);
		}

		/// <summary>
		///     Returns the iterator pointing to the first item greater than <paramref name="item" />.
		/// </summary>
		public RedBlackTreeIterator<T> UpperBound(T item)
		{
			var it = LowerBound(item);
			while (it.IsValid && _host.Compare(it.Current, item) == 0)
				it.MoveNext();
			return it;
		}

		/// <summary>
		///     Gets a tree iterator that starts on the first node.
		/// </summary>
		public RedBlackTreeIterator<T> Begin()
		{
			if (Root == null) return default(RedBlackTreeIterator<T>);
			return new RedBlackTreeIterator<T>(Root.LeftMost);
		}

		/// <summary>
		///     Gets a tree iterator that starts one node before the first node.
		/// </summary>
		public RedBlackTreeIterator<T> GetEnumerator()
		{
			if (Root == null) return default(RedBlackTreeIterator<T>);
			var dummyNode = new RedBlackTreeNode<T>(default(T));
			dummyNode.Right = Root;
			return new RedBlackTreeIterator<T>(dummyNode);
		}

		#endregion

		#region ICollection members

		public bool Contains(T item)
		{
			return Find(item).IsValid;
		}

		public bool Remove(T item)
		{
			var it = Find(item);
			if (!it.IsValid)
				return false;
			RemoveAt(it);
			return true;
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		bool ICollection<T>.IsReadOnly => false;

		public void CopyTo(T[] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException("array");
			foreach (var val in this)
				array[arrayIndex++] = val;
		}

		#endregion
	}
}