// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     This class handles all mouse stuff for a textArea.
	/// </summary>
	public class TextAreaMouseHandler
	{
		private static readonly Point NilPoint = new Point(-1, -1);

		private MouseButtons _button;
		private bool _clickedOnSelectedText;
		private bool _dodragdrop;
		private bool _doubleclick;

		private bool _gotmousedown;
		private Point _lastmousedownpos = NilPoint;
		private TextLocation _maxSelection = TextLocation.Empty;

		private TextLocation _minSelection = TextLocation.Empty;
		private Point _mousedownpos = NilPoint;
		private readonly TextArea _textArea;

		public TextAreaMouseHandler(TextArea ttextArea)
		{
			_textArea = ttextArea;
		}

		public void Attach()
		{
			_textArea.Click += TextAreaClick;
			_textArea.MouseMove += TextAreaMouseMove;

			_textArea.MouseDown += OnMouseDown;
			_textArea.DoubleClick += OnDoubleClick;
			_textArea.MouseLeave += OnMouseLeave;
			_textArea.MouseUp += OnMouseUp;
			_textArea.LostFocus += TextAreaLostFocus;
			_textArea.ToolTipRequest += OnToolTipRequest;
		}

		private void OnToolTipRequest(object sender, ToolTipRequestEventArgs e)
		{
			if (e.ToolTipShown)
				return;
			var mousepos = e.MousePosition;
			var marker = _textArea.TextView.GetFoldMarkerFromPosition(mousepos.X - _textArea.TextView.DrawingPosition.X,
				mousepos.Y - _textArea.TextView.DrawingPosition.Y);
			if (marker != null && marker.IsFolded)
			{
				var sb = new StringBuilder(marker.InnerText);

				// max 10 lines
				var endLines = 0;
				for (var i = 0; i < sb.Length; ++i)
					if (sb[i] == '\n')
					{
						++endLines;
						if (endLines >= 10)
						{
							sb.Remove(i + 1, sb.Length - i - 1);
							sb.Append(Environment.NewLine);
							sb.Append("...");
							break;
						}
					}
				sb.Replace("\t", "    ");
				e.ShowToolTip(sb.ToString());
				return;
			}

			var markers = _textArea.Document.MarkerStrategy.GetMarkers(e.LogicalPosition);
			foreach (var tm in markers)
				if (tm.ToolTip != null)
				{
					e.ShowToolTip(tm.ToolTip.Replace("\t", "    "));
					return;
				}
		}

		private void ShowHiddenCursorIfMovedOrLeft()
		{
			_textArea.ShowHiddenCursor(!_textArea.Focused ||
			                           !_textArea.ClientRectangle.Contains(_textArea.PointToClient(Cursor.Position)));
		}

		private void TextAreaLostFocus(object sender, EventArgs e)
		{
			// The call to ShowHiddenCursorIfMovedOrLeft is delayed
			// until pending messages have been processed
			// so that it can properly detect whether the TextArea
			// has really lost focus.
			// For example, the CodeCompletionWindow gets focus when it is shown,
			// but immediately gives back focus to the TextArea.
			_textArea.BeginInvoke(new MethodInvoker(ShowHiddenCursorIfMovedOrLeft));
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			ShowHiddenCursorIfMovedOrLeft();
			_gotmousedown = false;
			_mousedownpos = NilPoint;
		}

		private void OnMouseUp(object sender, MouseEventArgs e)
		{
			_textArea.SelectionManager.SelectFrom.Where = WhereFrom.None;
			_gotmousedown = false;
			_mousedownpos = NilPoint;
		}

		private void TextAreaClick(object sender, EventArgs e)
		{
			Point mousepos;
			mousepos = _textArea.Mousepos;

			if (_dodragdrop)
				return;

			if (_clickedOnSelectedText && _textArea.TextView.DrawingPosition.Contains(mousepos.X, mousepos.Y))
			{
				_textArea.SelectionManager.ClearSelection();

				var clickPosition = _textArea.TextView.GetLogicalPosition(
					mousepos.X - _textArea.TextView.DrawingPosition.X,
					mousepos.Y - _textArea.TextView.DrawingPosition.Y);
				_textArea.Caret.Position = clickPosition;
				_textArea.SetDesiredColumn();
			}
		}


		private void TextAreaMouseMove(object sender, MouseEventArgs e)
		{
			_textArea.Mousepos = e.Location;

			// honour the starting selection strategy
			switch (_textArea.SelectionManager.SelectFrom.Where)
			{
				case WhereFrom.Gutter:
					ExtendSelectionToMouse();
					return;

				case WhereFrom.Area:
					break;
			}
			_textArea.ShowHiddenCursor(false);
			if (_dodragdrop)
			{
				_dodragdrop = false;
				return;
			}

			_doubleclick = false;
			_textArea.Mousepos = new Point(e.X, e.Y);

			if (_clickedOnSelectedText)
			{
				if (Math.Abs(_mousedownpos.X - e.X) >= SystemInformation.DragSize.Width / 2 ||
				    Math.Abs(_mousedownpos.Y - e.Y) >= SystemInformation.DragSize.Height / 2)
				{
					_clickedOnSelectedText = false;
					var selection = _textArea.SelectionManager.GetSelectionAt(_textArea.Caret.Offset);
					if (selection != null)
					{
						var text = selection.SelectedText;
						var isReadOnly = SelectionManager.SelectionIsReadOnly(_textArea.Document, selection);
						if (text != null && text.Length > 0)
						{
							var dataObject = new DataObject();
							dataObject.SetData(DataFormats.UnicodeText, true, text);
							dataObject.SetData(selection);
							_dodragdrop = true;
							_textArea.DoDragDrop(dataObject, isReadOnly ? DragDropEffects.All & ~DragDropEffects.Move : DragDropEffects.All);
						}
					}
				}

				return;
			}

			if (e.Button == MouseButtons.Left)
				if (_gotmousedown && _textArea.SelectionManager.SelectFrom.Where == WhereFrom.Area)
					ExtendSelectionToMouse();
		}

		private void ExtendSelectionToMouse()
		{
			Point mousepos;
			mousepos = _textArea.Mousepos;
			var realmousepos = _textArea.TextView.GetLogicalPosition(
				Math.Max(0, mousepos.X - _textArea.TextView.DrawingPosition.X),
				mousepos.Y - _textArea.TextView.DrawingPosition.Y);
			var y = realmousepos.Y;
			realmousepos = _textArea.Caret.ValidatePosition(realmousepos);
			var oldPos = _textArea.Caret.Position;
			if (oldPos == realmousepos && _textArea.SelectionManager.SelectFrom.Where != WhereFrom.Gutter)
				return;

			// the selection is from the gutter
			if (_textArea.SelectionManager.SelectFrom.Where == WhereFrom.Gutter)
				if (realmousepos.Y < _textArea.SelectionManager.SelectionStart.Y)
					_textArea.Caret.Position = new TextLocation(0, realmousepos.Y);
				else
					_textArea.Caret.Position = _textArea.SelectionManager.NextValidPosition(realmousepos.Y);
			else
				_textArea.Caret.Position = realmousepos;

			// moves selection across whole words for double-click initiated selection
			if (!_minSelection.IsEmpty && _textArea.SelectionManager.SelectionCollection.Count > 0 &&
			    _textArea.SelectionManager.SelectFrom.Where == WhereFrom.Area)
			{
				// Extend selection when selection was started with double-click
				var selection = _textArea.SelectionManager.SelectionCollection[0];
				var min = _textArea.SelectionManager.GreaterEqPos(_minSelection, _maxSelection)
					? _maxSelection
					: _minSelection;
				var max = _textArea.SelectionManager.GreaterEqPos(_minSelection, _maxSelection)
					? _minSelection
					: _maxSelection;
				if (_textArea.SelectionManager.GreaterEqPos(max, realmousepos) &&
				    _textArea.SelectionManager.GreaterEqPos(realmousepos, min))
				{
					_textArea.SelectionManager.SetSelection(min, max);
				}
				else if (_textArea.SelectionManager.GreaterEqPos(max, realmousepos))
				{
					var moff = _textArea.Document.PositionToOffset(realmousepos);
					min = _textArea.Document.OffsetToPosition(FindWordStart(_textArea.Document, moff));
					_textArea.SelectionManager.SetSelection(min, max);
				}
				else
				{
					var moff = _textArea.Document.PositionToOffset(realmousepos);
					max = _textArea.Document.OffsetToPosition(FindWordEnd(_textArea.Document, moff));
					_textArea.SelectionManager.SetSelection(min, max);
				}
			}
			else
			{
				_textArea.SelectionManager.ExtendSelection(oldPos, _textArea.Caret.Position);
			}
			_textArea.SetDesiredColumn();
		}

		private void DoubleClickSelectionExtend()
		{
			Point mousepos;
			mousepos = _textArea.Mousepos;

			_textArea.SelectionManager.ClearSelection();
			if (_textArea.TextView.DrawingPosition.Contains(mousepos.X, mousepos.Y))
			{
				var marker = _textArea.TextView.GetFoldMarkerFromPosition(mousepos.X - _textArea.TextView.DrawingPosition.X,
					mousepos.Y - _textArea.TextView.DrawingPosition.Y);
				if (marker != null && marker.IsFolded)
				{
					marker.IsFolded = false;
					_textArea.MotherTextAreaControl.AdjustScrollBars();
				}
				if (_textArea.Caret.Offset < _textArea.Document.TextLength)
				{
					switch (_textArea.Document.GetCharAt(_textArea.Caret.Offset))
					{
						case '"':
							if (_textArea.Caret.Offset < _textArea.Document.TextLength)
							{
								var next = FindNext(_textArea.Document, _textArea.Caret.Offset + 1, '"');
								_minSelection = _textArea.Caret.Position;
								if (next > _textArea.Caret.Offset && next < _textArea.Document.TextLength)
									next += 1;
								_maxSelection = _textArea.Document.OffsetToPosition(next);
							}
							break;
						default:
							_minSelection = _textArea.Document.OffsetToPosition(FindWordStart(_textArea.Document, _textArea.Caret.Offset));
							_maxSelection = _textArea.Document.OffsetToPosition(FindWordEnd(_textArea.Document, _textArea.Caret.Offset));
							break;
					}
					_textArea.Caret.Position = _maxSelection;
					_textArea.SelectionManager.ExtendSelection(_minSelection, _maxSelection);
				}

				if (_textArea.SelectionManager.selectionCollection.Count > 0)
				{
					var selection = _textArea.SelectionManager.selectionCollection[0];

					selection.StartPosition = _minSelection;
					selection.EndPosition = _maxSelection;
					_textArea.SelectionManager.SelectionStart = _minSelection;
				}

				// after a double-click selection, the caret is placed correctly,
				// but it is not positioned internally.  The effect is when the cursor
				// is moved up or down a line, the caret will take on the column first
				// clicked on for the double-click
				_textArea.SetDesiredColumn();

				// HACK WARNING !!!
				// must refresh here, because when a error tooltip is showed and the underlined
				// code is double clicked the textArea don't update corrctly, updateline doesn't
				// work ... but the refresh does.
				// Mike
				_textArea.Refresh();
			}
		}

		private void OnMouseDown(object sender, MouseEventArgs e)
		{
			Point mousepos;
			_textArea.Mousepos = e.Location;
			mousepos = e.Location;

			if (_dodragdrop)
				return;

			if (_doubleclick)
			{
				_doubleclick = false;
				return;
			}

			if (_textArea.TextView.DrawingPosition.Contains(mousepos.X, mousepos.Y))
			{
				_gotmousedown = true;
				_textArea.SelectionManager.SelectFrom.Where = WhereFrom.Area;
				_button = e.Button;

				// double-click
				if (_button == MouseButtons.Left && e.Clicks == 2)
				{
					var deltaX = Math.Abs(_lastmousedownpos.X - e.X);
					var deltaY = Math.Abs(_lastmousedownpos.Y - e.Y);
					if (deltaX <= SystemInformation.DoubleClickSize.Width &&
					    deltaY <= SystemInformation.DoubleClickSize.Height)
					{
						DoubleClickSelectionExtend();
						_lastmousedownpos = new Point(e.X, e.Y);

						if (_textArea.SelectionManager.SelectFrom.Where == WhereFrom.Gutter)
							if (!_minSelection.IsEmpty && !_maxSelection.IsEmpty && _textArea.SelectionManager.SelectionCollection.Count > 0)
							{
								_textArea.SelectionManager.SelectionCollection[0].StartPosition = _minSelection;
								_textArea.SelectionManager.SelectionCollection[0].EndPosition = _maxSelection;
								_textArea.SelectionManager.SelectionStart = _minSelection;

								_minSelection = TextLocation.Empty;
								_maxSelection = TextLocation.Empty;
							}
						return;
					}
				}
				_minSelection = TextLocation.Empty;
				_maxSelection = TextLocation.Empty;

				_lastmousedownpos = _mousedownpos = new Point(e.X, e.Y);

				if (_button == MouseButtons.Left)
				{
					var marker = _textArea.TextView.GetFoldMarkerFromPosition(mousepos.X - _textArea.TextView.DrawingPosition.X,
						mousepos.Y - _textArea.TextView.DrawingPosition.Y);
					if (marker != null && marker.IsFolded)
					{
						if (_textArea.SelectionManager.HasSomethingSelected)
							_clickedOnSelectedText = true;

						var startLocation = new TextLocation(marker.StartColumn, marker.StartLine);
						var endLocation = new TextLocation(marker.EndColumn, marker.EndLine);
						_textArea.SelectionManager.SetSelection(new DefaultSelection(_textArea.TextView.Document, startLocation,
							endLocation));
						_textArea.Caret.Position = startLocation;
						_textArea.SetDesiredColumn();
						_textArea.Focus();
						return;
					}

					if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
					{
						ExtendSelectionToMouse();
					}
					else
					{
						var realmousepos =
							_textArea.TextView.GetLogicalPosition(mousepos.X - _textArea.TextView.DrawingPosition.X,
								mousepos.Y - _textArea.TextView.DrawingPosition.Y);
						_clickedOnSelectedText = false;

						var offset = _textArea.Document.PositionToOffset(realmousepos);

						if (_textArea.SelectionManager.HasSomethingSelected &&
						    _textArea.SelectionManager.IsSelected(offset))
						{
							_clickedOnSelectedText = true;
						}
						else
						{
							_textArea.SelectionManager.ClearSelection();
							if (mousepos.Y > 0 && mousepos.Y < _textArea.TextView.DrawingPosition.Height)
							{
								var pos = new TextLocation();
								pos.Y = Math.Min(_textArea.Document.TotalNumberOfLines - 1, realmousepos.Y);
								pos.X = realmousepos.X;
								_textArea.Caret.Position = pos;
								_textArea.SetDesiredColumn();
							}
						}
					}
				}
				else if (_button == MouseButtons.Right)
				{
					// Rightclick sets the cursor to the click position unless
					// the previous selection was clicked
					var realmousepos = _textArea.TextView.GetLogicalPosition(
						mousepos.X - _textArea.TextView.DrawingPosition.X, mousepos.Y - _textArea.TextView.DrawingPosition.Y);
					var offset = _textArea.Document.PositionToOffset(realmousepos);
					if (!_textArea.SelectionManager.HasSomethingSelected ||
					    !_textArea.SelectionManager.IsSelected(offset))
					{
						_textArea.SelectionManager.ClearSelection();
						if (mousepos.Y > 0 && mousepos.Y < _textArea.TextView.DrawingPosition.Height)
						{
							var pos = new TextLocation();
							pos.Y = Math.Min(_textArea.Document.TotalNumberOfLines - 1, realmousepos.Y);
							pos.X = realmousepos.X;
							_textArea.Caret.Position = pos;
							_textArea.SetDesiredColumn();
						}
					}
				}
			}
			_textArea.Focus();
		}

		private int FindNext(IDocument document, int offset, char ch)
		{
			var line = document.GetLineSegmentForOffset(offset);
			var endPos = line.Offset + line.Length;

			while (offset < endPos && document.GetCharAt(offset) != ch)
				++offset;
			return offset;
		}

		private bool IsSelectableChar(char ch)
		{
			return char.IsLetterOrDigit(ch) || ch == '_';
		}

		private int FindWordStart(IDocument document, int offset)
		{
			var line = document.GetLineSegmentForOffset(offset);

			if (offset > 0 && char.IsWhiteSpace(document.GetCharAt(offset - 1)) && char.IsWhiteSpace(document.GetCharAt(offset)))
			{
				while (offset > line.Offset && char.IsWhiteSpace(document.GetCharAt(offset - 1)))
					--offset;
			}
			else if (IsSelectableChar(document.GetCharAt(offset)) ||
			         offset > 0 && char.IsWhiteSpace(document.GetCharAt(offset)) &&
			         IsSelectableChar(document.GetCharAt(offset - 1)))
			{
				while (offset > line.Offset && IsSelectableChar(document.GetCharAt(offset - 1)))
					--offset;
			}
			else
			{
				if (offset > 0 && !char.IsWhiteSpace(document.GetCharAt(offset - 1)) &&
				    !IsSelectableChar(document.GetCharAt(offset - 1)))
					return Math.Max(0, offset - 1);
			}
			return offset;
		}

		private int FindWordEnd(IDocument document, int offset)
		{
			var line = document.GetLineSegmentForOffset(offset);
			if (line.Length == 0)
				return offset;
			var endPos = line.Offset + line.Length;
			offset = Math.Min(offset, endPos - 1);

			if (IsSelectableChar(document.GetCharAt(offset)))
			{
				while (offset < endPos && IsSelectableChar(document.GetCharAt(offset)))
					++offset;
			}
			else if (char.IsWhiteSpace(document.GetCharAt(offset)))
			{
				if (offset > 0 && char.IsWhiteSpace(document.GetCharAt(offset - 1)))
					while (offset < endPos && char.IsWhiteSpace(document.GetCharAt(offset)))
						++offset;
			}
			else
			{
				return Math.Max(0, offset + 1);
			}

			return offset;
		}

		private void OnDoubleClick(object sender, EventArgs e)
		{
			if (_dodragdrop)
				return;

			_textArea.SelectionManager.SelectFrom.Where = WhereFrom.Area;
			_doubleclick = true;
		}
	}
}