// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ICSharpCode.TextEditor.Util
{
	/// <summary>
	///     A IList{T} that checks that it is only accessed on the thread that created it, and that
	///     it is not modified while an enumerator is running.
	/// </summary>
	internal sealed class CheckedList<T> : IList<T>
	{
		private readonly IList<T> _baseList;
		private readonly int _threadId;
		private int _enumeratorCount;

		public CheckedList() : this(new List<T>())
		{
		}

		public CheckedList(IList<T> baseList)
		{
			if (baseList == null)
				throw new ArgumentNullException("baseList");
			_baseList = baseList;
			_threadId = Thread.CurrentThread.ManagedThreadId;
		}

		public T this[int index]
		{
			get
			{
				CheckRead();
				return _baseList[index];
			}
			set
			{
				CheckWrite();
				_baseList[index] = value;
			}
		}

		public int Count
		{
			get
			{
				CheckRead();
				return _baseList.Count;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				CheckRead();
				return _baseList.IsReadOnly;
			}
		}

		public int IndexOf(T item)
		{
			CheckRead();
			return _baseList.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			CheckWrite();
			_baseList.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			CheckWrite();
			_baseList.RemoveAt(index);
		}

		public void Add(T item)
		{
			CheckWrite();
			_baseList.Add(item);
		}

		public void Clear()
		{
			CheckWrite();
			_baseList.Clear();
		}

		public bool Contains(T item)
		{
			CheckRead();
			return _baseList.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			CheckRead();
			_baseList.CopyTo(array, arrayIndex);
		}

		public bool Remove(T item)
		{
			CheckWrite();
			return _baseList.Remove(item);
		}

		public IEnumerator<T> GetEnumerator()
		{
			CheckRead();
			return Enumerate();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			CheckRead();
			return Enumerate();
		}

		private void CheckRead()
		{
			if (Thread.CurrentThread.ManagedThreadId != _threadId)
				throw new InvalidOperationException("CheckList cannot be accessed from this thread!");
		}

		private void CheckWrite()
		{
			if (Thread.CurrentThread.ManagedThreadId != _threadId)
				throw new InvalidOperationException("CheckList cannot be accessed from this thread!");
			if (_enumeratorCount != 0)
				throw new InvalidOperationException("CheckList cannot be written to while enumerators are active!");
		}

		private IEnumerator<T> Enumerate()
		{
			CheckRead();
			try
			{
				_enumeratorCount++;
				foreach (var val in _baseList)
				{
					yield return val;
					CheckRead();
				}
			}
			finally
			{
				_enumeratorCount--;
				CheckRead();
			}
		}
	}
}