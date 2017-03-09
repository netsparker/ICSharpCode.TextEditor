// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;

namespace ICSharpCode.TextEditor.Undo
{
	/// <summary>
	///     This class implements an undo stack
	/// </summary>
	public class UndoStack
	{
		private int _actionCountInUndoGroup;
		private readonly Stack<IUndoableOperation> _redostack = new Stack<IUndoableOperation>();

		private int _undoGroupDepth;
		private readonly Stack<IUndoableOperation> _undostack = new Stack<IUndoableOperation>();

		/// <summary>
		///     Gets/Sets if changes to the document are protocolled by the undo stack.
		///     Used internally to disable the undo stack temporarily while undoing an action.
		/// </summary>
		internal bool AcceptChanges = true;

		public TextEditorControlBase TextEditorControl = null;

		/// <summary>
		///     Gets if there are actions on the undo stack.
		/// </summary>
		public bool CanUndo => _undostack.Count > 0;

		/// <summary>
		///     Gets if there are actions on the redo stack.
		/// </summary>
		public bool CanRedo => _redostack.Count > 0;

		/// <summary>
		///     Gets the number of actions on the undo stack.
		/// </summary>
		public int UndoItemCount => _undostack.Count;

		/// <summary>
		///     Gets the number of actions on the redo stack.
		/// </summary>
		public int RedoItemCount => _redostack.Count;

		/// <summary>
		/// </summary>
		public event EventHandler ActionUndone;

		/// <summary>
		/// </summary>
		public event EventHandler ActionRedone;

		public event OperationEventHandler OperationPushed;

		public void StartUndoGroup()
		{
			if (_undoGroupDepth == 0)
				_actionCountInUndoGroup = 0;
			_undoGroupDepth++;
			//Util.LoggingService.Debug("Open undo group (new depth=" + undoGroupDepth + ")");
		}

		public void EndUndoGroup()
		{
			if (_undoGroupDepth == 0)
				throw new InvalidOperationException("There are no open undo groups");
			_undoGroupDepth--;
			//Util.LoggingService.Debug("Close undo group (new depth=" + undoGroupDepth + ")");
			if (_undoGroupDepth == 0 && _actionCountInUndoGroup > 1)
			{
				var op = new UndoQueue(_undostack, _actionCountInUndoGroup);
				_undostack.Push(op);
				if (OperationPushed != null)
					OperationPushed(this, new OperationEventArgs(op));
			}
		}

		public void AssertNoUndoGroupOpen()
		{
			if (_undoGroupDepth != 0)
			{
				_undoGroupDepth = 0;
				throw new InvalidOperationException("No undo group should be open at this point");
			}
		}

		/// <summary>
		///     Call this method to undo the last operation on the stack
		/// </summary>
		public void Undo()
		{
			AssertNoUndoGroupOpen();
			if (_undostack.Count > 0)
			{
				var uedit = _undostack.Pop();
				_redostack.Push(uedit);
				uedit.Undo();
				OnActionUndone();
			}
		}

		/// <summary>
		///     Call this method to redo the last undone operation
		/// </summary>
		public void Redo()
		{
			AssertNoUndoGroupOpen();
			if (_redostack.Count > 0)
			{
				var uedit = _redostack.Pop();
				_undostack.Push(uedit);
				uedit.Redo();
				OnActionRedone();
			}
		}

		/// <summary>
		///     Call this method to push an UndoableOperation on the undostack, the redostack
		///     will be cleared, if you use this method.
		/// </summary>
		public void Push(IUndoableOperation operation)
		{
			if (operation == null)
				throw new ArgumentNullException("operation");

			if (AcceptChanges)
			{
				StartUndoGroup();
				_undostack.Push(operation);
				_actionCountInUndoGroup++;
				if (TextEditorControl != null)
				{
					_undostack.Push(new UndoableSetCaretPosition(this, TextEditorControl.ActiveTextAreaControl.Caret.Position));
					_actionCountInUndoGroup++;
				}
				EndUndoGroup();
				ClearRedoStack();
			}
		}

		/// <summary>
		///     Call this method, if you want to clear the redo stack
		/// </summary>
		public void ClearRedoStack()
		{
			_redostack.Clear();
		}

		/// <summary>
		///     Clears both the undo and redo stack.
		/// </summary>
		public void ClearAll()
		{
			AssertNoUndoGroupOpen();
			_undostack.Clear();
			_redostack.Clear();
			_actionCountInUndoGroup = 0;
		}

		/// <summary>
		/// </summary>
		protected void OnActionUndone()
		{
			if (ActionUndone != null)
				ActionUndone(null, null);
		}

		/// <summary>
		/// </summary>
		protected void OnActionRedone()
		{
			if (ActionRedone != null)
				ActionRedone(null, null);
		}

		private class UndoableSetCaretPosition : IUndoableOperation
		{
			private readonly TextLocation _pos;
			private TextLocation _redoPos;
			private readonly UndoStack _stack;

			public UndoableSetCaretPosition(UndoStack stack, TextLocation pos)
			{
				_stack = stack;
				_pos = pos;
			}

			public void Undo()
			{
				_redoPos = _stack.TextEditorControl.ActiveTextAreaControl.Caret.Position;
				_stack.TextEditorControl.ActiveTextAreaControl.Caret.Position = _pos;
				_stack.TextEditorControl.ActiveTextAreaControl.SelectionManager.ClearSelection();
			}

			public void Redo()
			{
				_stack.TextEditorControl.ActiveTextAreaControl.Caret.Position = _redoPos;
				_stack.TextEditorControl.ActiveTextAreaControl.SelectionManager.ClearSelection();
			}
		}
	}

	public class OperationEventArgs : EventArgs
	{
		public OperationEventArgs(IUndoableOperation op)
		{
			Operation = op;
		}

		public IUndoableOperation Operation { get; }
	}

	public delegate void OperationEventHandler(object sender, OperationEventArgs e);
}