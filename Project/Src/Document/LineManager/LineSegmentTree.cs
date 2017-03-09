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
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor.Document
{
	/// <summary>
	///     Data structure for efficient management of the line segments (most operations are O(lg n)).
	///     This implements an augmented red-black tree where each node has fields for the number of
	///     nodes in its subtree (like an order statistics tree) for access by index(=line number).
	///     Additionally, each node knows the total length of all segments in its subtree.
	///     This means we can find nodes by offset in O(lg n) time. Since the offset itself is not stored in
	///     the line segment but computed from the lengths stored in the tree, we adjusting the offsets when
	///     text is inserted in one line means we just have to increment the totalLength of the affected line and
	///     its parent nodes - an O(lg n) operation.
	///     However this means getting the line number or offset from a LineSegment is not a constant time
	///     operation, but takes O(lg n).
	///     NOTE: The tree is never empty, Clear() causes it to contain an empty segment.
	/// </summary>
	internal sealed class LineSegmentTree : IList<LineSegment>
	{
		private readonly AugmentableRedBlackTree<RbNode, MyHost> _tree =
			new AugmentableRedBlackTree<RbNode, MyHost>(new MyHost());

		public LineSegmentTree()
		{
			Clear();
		}

		/// <summary>
		///     Gets the total length of all line segments. Runs in O(1).
		/// </summary>
		public int TotalLength
		{
			get
			{
				if (_tree.Root == null)
					return 0;
				return _tree.Root.Val.totalLength;
			}
		}

		/// <summary>
		///     Gets the number of items in the collections. Runs in O(1).
		/// </summary>
		public int Count => _tree.Count;

		/// <summary>
		///     Gets or sets an item by index. Runs in O(lg n).
		/// </summary>
		public LineSegment this[int index]
		{
			get { return GetNode(index).Val.LineSegment; }
			set { throw new NotSupportedException(); }
		}

		bool ICollection<LineSegment>.IsReadOnly => true;

		/// <summary>
		///     Gets the index of an item. Runs in O(lg n).
		/// </summary>
		public int IndexOf(LineSegment item)
		{
			var index = item.LineNumber;
			if (index < 0 || index >= Count)
				return -1;
			if (item != this[index])
				return -1;
			return index;
		}

		void IList<LineSegment>.RemoveAt(int index)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		///     Clears the list. Runs in O(1).
		/// </summary>
		public void Clear()
		{
			_tree.Clear();
			var emptySegment = new LineSegment();
			emptySegment.TotalLength = 0;
			emptySegment.DelimiterLength = 0;
			_tree.Add(new RbNode(emptySegment));
			emptySegment.TreeEntry = GetEnumeratorForIndex(0);
#if DEBUG
			CheckProperties();
#endif
		}

		/// <summary>
		///     Tests whether an item is in the list. Runs in O(n).
		/// </summary>
		public bool Contains(LineSegment item)
		{
			return IndexOf(item) >= 0;
		}

		/// <summary>
		///     Copies all elements from the list to the array.
		/// </summary>
		public void CopyTo(LineSegment[] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException("array");
			foreach (var val in this)
				array[arrayIndex++] = val;
		}

		IEnumerator<LineSegment> IEnumerable<LineSegment>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		void IList<LineSegment>.Insert(int index, LineSegment item)
		{
			throw new NotSupportedException();
		}

		void ICollection<LineSegment>.Add(LineSegment item)
		{
			throw new NotSupportedException();
		}

		bool ICollection<LineSegment>.Remove(LineSegment item)
		{
			throw new NotSupportedException();
		}

		private RedBlackTreeNode<RbNode> GetNode(int index)
		{
			if (index < 0 || index >= _tree.Count)
				throw new ArgumentOutOfRangeException("index", index, "index should be between 0 and " + (_tree.Count - 1));
			var node = _tree.Root;
			while (true)
				if (node.Left != null && index < node.Left.Val.count)
				{
					node = node.Left;
				}
				else
				{
					if (node.Left != null)
						index -= node.Left.Val.count;
					if (index == 0)
						return node;
					index--;
					node = node.Right;
				}
		}

		private static int GetIndexFromNode(RedBlackTreeNode<RbNode> node)
		{
			var index = node.Left != null ? node.Left.Val.count : 0;
			while (node.Parent != null)
			{
				if (node == node.Parent.Right)
				{
					if (node.Parent.Left != null)
						index += node.Parent.Left.Val.count;
					index++;
				}
				node = node.Parent;
			}
			return index;
		}

		private RedBlackTreeNode<RbNode> GetNodeByOffset(int offset)
		{
			if (offset < 0 || offset > TotalLength)
				throw new ArgumentOutOfRangeException("offset", offset, "offset should be between 0 and " + TotalLength);
			if (offset == TotalLength)
			{
				if (_tree.Root == null)
					throw new InvalidOperationException("Cannot call GetNodeByOffset while tree is empty.");
				return _tree.Root.RightMost;
			}
			var node = _tree.Root;
			while (true)
				if (node.Left != null && offset < node.Left.Val.totalLength)
				{
					node = node.Left;
				}
				else
				{
					if (node.Left != null)
						offset -= node.Left.Val.totalLength;
					offset -= node.Val.LineSegment.TotalLength;
					if (offset < 0)
						return node;
					node = node.Right;
				}
		}

		private static int GetOffsetFromNode(RedBlackTreeNode<RbNode> node)
		{
			var offset = node.Left != null ? node.Left.Val.totalLength : 0;
			while (node.Parent != null)
			{
				if (node == node.Parent.Right)
				{
					if (node.Parent.Left != null)
						offset += node.Parent.Left.Val.totalLength;
					offset += node.Parent.Val.LineSegment.TotalLength;
				}
				node = node.Parent;
			}
			return offset;
		}

		public LineSegment GetByOffset(int offset)
		{
			return GetNodeByOffset(offset).Val.LineSegment;
		}

		/// <summary>
		///     Updates the length of a line segment. Runs in O(lg n).
		/// </summary>
		public void SetSegmentLength(LineSegment segment, int newTotalLength)
		{
			if (segment == null)
				throw new ArgumentNullException("segment");
			var node = segment.TreeEntry.It.Node;
			segment.TotalLength = newTotalLength;
			default(MyHost).UpdateAfterChildrenChange(node);
#if DEBUG
			CheckProperties();
#endif
		}

		public void RemoveSegment(LineSegment segment)
		{
			_tree.RemoveAt(segment.TreeEntry.It);
#if DEBUG
			CheckProperties();
#endif
		}

		public LineSegment InsertSegmentAfter(LineSegment segment, int length)
		{
			var newSegment = new LineSegment();
			newSegment.TotalLength = length;
			newSegment.DelimiterLength = segment.DelimiterLength;

			newSegment.TreeEntry = InsertAfter(segment.TreeEntry.It.Node, newSegment);
			return newSegment;
		}

		private Enumerator InsertAfter(RedBlackTreeNode<RbNode> node, LineSegment newSegment)
		{
			var newNode = new RedBlackTreeNode<RbNode>(new RbNode(newSegment));
			if (node.Right == null)
				_tree.InsertAsRight(node, newNode);
			else
				_tree.InsertAsLeft(node.Right.LeftMost, newNode);
#if DEBUG
			CheckProperties();
#endif
			return new Enumerator(new RedBlackTreeIterator<RbNode>(newNode));
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(_tree.GetEnumerator());
		}

		public Enumerator GetEnumeratorForIndex(int index)
		{
			return new Enumerator(new RedBlackTreeIterator<RbNode>(GetNode(index)));
		}

		public Enumerator GetEnumeratorForOffset(int offset)
		{
			return new Enumerator(new RedBlackTreeIterator<RbNode>(GetNodeByOffset(offset)));
		}

		internal struct RbNode
		{
			internal LineSegment LineSegment;
			internal int count;
			internal int totalLength;

			public RbNode(LineSegment lineSegment)
			{
				LineSegment = lineSegment;
				count = 1;
				totalLength = lineSegment.TotalLength;
			}

			public override string ToString()
			{
				return "[RBNode count=" + count + " totalLength=" + totalLength
				       + " lineSegment.LineNumber=" + LineSegment.LineNumber
				       + " lineSegment.Offset=" + LineSegment.Offset
				       + " lineSegment.TotalLength=" + LineSegment.TotalLength
				       + " lineSegment.DelimiterLength=" + LineSegment.DelimiterLength + "]";
			}
		}

		private struct MyHost : IRedBlackTreeHost<RbNode>
		{
			public int Compare(RbNode x, RbNode y)
			{
				throw new NotImplementedException();
			}

			public bool Equals(RbNode a, RbNode b)
			{
				throw new NotImplementedException();
			}

			public void UpdateAfterChildrenChange(RedBlackTreeNode<RbNode> node)
			{
				var count = 1;
				var totalLength = node.Val.LineSegment.TotalLength;
				if (node.Left != null)
				{
					count += node.Left.Val.count;
					totalLength += node.Left.Val.totalLength;
				}
				if (node.Right != null)
				{
					count += node.Right.Val.count;
					totalLength += node.Right.Val.totalLength;
				}
				if (count != node.Val.count || totalLength != node.Val.totalLength)
				{
					node.Val.count = count;
					node.Val.totalLength = totalLength;
					if (node.Parent != null) UpdateAfterChildrenChange(node.Parent);
				}
			}

			public void UpdateAfterRotateLeft(RedBlackTreeNode<RbNode> node)
			{
				UpdateAfterChildrenChange(node);
				UpdateAfterChildrenChange(node.Parent);
			}

			public void UpdateAfterRotateRight(RedBlackTreeNode<RbNode> node)
			{
				UpdateAfterChildrenChange(node);
				UpdateAfterChildrenChange(node.Parent);
			}
		}

		public struct Enumerator : IEnumerator<LineSegment>
		{
			/// <summary>
			///     An invalid enumerator value. Calling MoveNext on the invalid enumerator
			///     will always return false, accessing Current will throw an exception.
			/// </summary>
			public static readonly Enumerator Invalid = default(Enumerator);

			internal RedBlackTreeIterator<RbNode> It;

			internal Enumerator(RedBlackTreeIterator<RbNode> it)
			{
				It = it;
			}

			/// <summary>
			///     Gets the current value. Runs in O(1).
			/// </summary>
			public LineSegment Current => It.Current.LineSegment;

			public bool IsValid => It.IsValid;

			/// <summary>
			///     Gets the index of the current value. Runs in O(lg n).
			/// </summary>
			public int CurrentIndex
			{
				get
				{
					if (It.Node == null)
						throw new InvalidOperationException();
					return GetIndexFromNode(It.Node);
				}
			}

			/// <summary>
			///     Gets the offset of the current value. Runs in O(lg n).
			/// </summary>
			public int CurrentOffset
			{
				get
				{
					if (It.Node == null)
						throw new InvalidOperationException();
					return GetOffsetFromNode(It.Node);
				}
			}

			object IEnumerator.Current => It.Current.LineSegment;

			public void Dispose()
			{
			}

			/// <summary>
			///     Moves to the next index. Runs in O(lg n), but for k calls, the combined time is only O(k+lg n).
			/// </summary>
			public bool MoveNext()
			{
				return It.MoveNext();
			}

			/// <summary>
			///     Moves to the previous index. Runs in O(lg n), but for k calls, the combined time is only O(k+lg n).
			/// </summary>
			public bool MoveBack()
			{
				return It.MoveBack();
			}

			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

#if DEBUG
		[Conditional("DATACONSISTENCYTEST")]
		private void CheckProperties()
		{
			if (_tree.Root == null)
			{
				Debug.Assert(Count == 0);
			}
			else
			{
				Debug.Assert(_tree.Root.Val.count == Count);
				CheckProperties(_tree.Root);
			}
		}

		private void CheckProperties(RedBlackTreeNode<RbNode> node)
		{
			var count = 1;
			var totalLength = node.Val.LineSegment.TotalLength;
			if (node.Left != null)
			{
				CheckProperties(node.Left);
				count += node.Left.Val.count;
				totalLength += node.Left.Val.totalLength;
			}
			if (node.Right != null)
			{
				CheckProperties(node.Right);
				count += node.Right.Val.count;
				totalLength += node.Right.Val.totalLength;
			}
			Debug.Assert(node.Val.count == count);
			Debug.Assert(node.Val.totalLength == totalLength);
		}

		public string GetTreeAsString()
		{
			return _tree.GetTreeAsString();
		}
#endif
	}
}