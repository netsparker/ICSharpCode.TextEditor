// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     This class paints the textarea.
	/// </summary>
	[ToolboxItem(false)]
	public class TextAreaControl : Panel
	{
		private const int LineLengthCacheAdditionalSize = 100;

		private bool _adjustScrollBarsOnNextUpdate;

		private bool _disposed;

		private HRuler _hRuler;

		private int[] _lineLengthCache;
		private TextEditorControl _motherTextEditorControl;

		private readonly MouseWheelHandler _mouseWheelHandler = new MouseWheelHandler();

		private readonly int _scrollMarginHeight = 3;
		private Point _scrollToPosOnNextUpdate;

		public TextAreaControl(TextEditorControl motherTextEditorControl)
		{
			_motherTextEditorControl = motherTextEditorControl;

			TextArea = new TextArea(motherTextEditorControl, this);
			Controls.Add(TextArea);

			VScrollBar.ValueChanged += VScrollBarValueChanged;
			Controls.Add(VScrollBar);

			HScrollBar.ValueChanged += HScrollBarValueChanged;
			Controls.Add(HScrollBar);
			ResizeRedraw = true;

			Document.TextContentChanged += DocumentTextContentChanged;
			Document.DocumentChanged += AdjustScrollBarsOnDocumentChange;
			Document.UpdateCommited += DocumentUpdateCommitted;
		}

		public TextArea TextArea { get; }

		public SelectionManager SelectionManager => TextArea.SelectionManager;

		public Caret Caret => TextArea.Caret;

		[Browsable(false)]
		public IDocument Document
		{
			get
			{
				if (_motherTextEditorControl != null)
					return _motherTextEditorControl.Document;
				return null;
			}
		}

		public ITextEditorProperties TextEditorProperties
		{
			get
			{
				if (_motherTextEditorControl != null)
					return _motherTextEditorControl.TextEditorProperties;
				return null;
			}
		}

		public VScrollBar VScrollBar { get; private set; } = new VScrollBar();

		public HScrollBar HScrollBar { get; private set; } = new HScrollBar();

		public bool DoHandleMousewheel { get; set; } = true;

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				if (!_disposed)
				{
					_disposed = true;
					Document.TextContentChanged -= DocumentTextContentChanged;
					Document.DocumentChanged -= AdjustScrollBarsOnDocumentChange;
					Document.UpdateCommited -= DocumentUpdateCommitted;
					_motherTextEditorControl = null;
					if (VScrollBar != null)
					{
						VScrollBar.Dispose();
						VScrollBar = null;
					}
					if (HScrollBar != null)
					{
						HScrollBar.Dispose();
						HScrollBar = null;
					}
					if (_hRuler != null)
					{
						_hRuler.Dispose();
						_hRuler = null;
					}
				}
			base.Dispose(disposing);
		}

		private void DocumentTextContentChanged(object sender, EventArgs e)
		{
			// after the text content is changed abruptly, we need to validate the
			// caret position - otherwise the caret position is invalid for a short amount
			// of time, which can break client code that expects that the caret position is always valid
			Caret.ValidateCaretPos();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			ResizeTextArea();
		}

		public void ResizeTextArea()
		{
			var y = 0;
			var h = 0;
			if (_hRuler != null)
			{
				_hRuler.Bounds = new Rectangle(0,
					0,
					Width - SystemInformation.HorizontalScrollBarArrowWidth,
					TextArea.TextView.FontHeight);

				y = _hRuler.Bounds.Bottom;
				h = _hRuler.Bounds.Height;
			}

			TextArea.Bounds = new Rectangle(0, y,
				Width - SystemInformation.HorizontalScrollBarArrowWidth,
				Height - SystemInformation.VerticalScrollBarArrowHeight - h);
			SetScrollBarBounds();
		}

		public void SetScrollBarBounds()
		{
			VScrollBar.Bounds = new Rectangle(TextArea.Bounds.Right, 0, SystemInformation.HorizontalScrollBarArrowWidth,
				Height - SystemInformation.VerticalScrollBarArrowHeight);
			HScrollBar.Bounds = new Rectangle(0,
				TextArea.Bounds.Bottom,
				Width - SystemInformation.HorizontalScrollBarArrowWidth,
				SystemInformation.VerticalScrollBarArrowHeight);
		}

		private void AdjustScrollBarsOnDocumentChange(object sender, DocumentEventArgs e)
		{
			if (_motherTextEditorControl.IsInUpdate == false)
			{
				AdjustScrollBarsClearCache();
				AdjustScrollBars();
			}
			else
			{
				_adjustScrollBarsOnNextUpdate = true;
			}
		}

		private void DocumentUpdateCommitted(object sender, EventArgs e)
		{
			if (_motherTextEditorControl.IsInUpdate == false)
			{
				Caret.ValidateCaretPos();

				// AdjustScrollBarsOnCommittedUpdate
				if (!_scrollToPosOnNextUpdate.IsEmpty)
					ScrollTo(_scrollToPosOnNextUpdate.Y, _scrollToPosOnNextUpdate.X);
				if (_adjustScrollBarsOnNextUpdate)
				{
					AdjustScrollBarsClearCache();
					AdjustScrollBars();
				}
			}
		}

		private void AdjustScrollBarsClearCache()
		{
			if (_lineLengthCache != null)
				if (_lineLengthCache.Length < Document.TotalNumberOfLines + 2 * LineLengthCacheAdditionalSize)
					_lineLengthCache = null;
				else
					Array.Clear(_lineLengthCache, 0, _lineLengthCache.Length);
		}

		public void AdjustScrollBars()
		{
			_adjustScrollBarsOnNextUpdate = false;
			VScrollBar.Minimum = 0;
			// number of visible lines in document (folding!)
			VScrollBar.Maximum = TextArea.MaxVScrollValue;
			var max = 0;

			var firstLine = TextArea.TextView.FirstVisibleLine;
			var lastLine =
				Document.GetFirstLogicalLine(TextArea.TextView.FirstPhysicalLine + TextArea.TextView.VisibleLineCount);
			if (lastLine >= Document.TotalNumberOfLines)
				lastLine = Document.TotalNumberOfLines - 1;

			if (_lineLengthCache == null || _lineLengthCache.Length <= lastLine)
				_lineLengthCache = new int[lastLine + LineLengthCacheAdditionalSize];

			for (var lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
			{
				var lineSegment = Document.GetLineSegment(lineNumber);
				if (Document.FoldingManager.IsLineVisible(lineNumber))
					if (_lineLengthCache[lineNumber] > 0)
					{
						max = Math.Max(max, _lineLengthCache[lineNumber]);
					}
					else
					{
						var visualLength = TextArea.TextView.GetVisualColumnFast(lineSegment, lineSegment.Length);
						_lineLengthCache[lineNumber] = Math.Max(1, visualLength);
						max = Math.Max(max, visualLength);
					}
			}
			HScrollBar.Minimum = 0;
			HScrollBar.Maximum = Math.Max(max + 20, TextArea.TextView.VisibleColumnCount - 1);

			VScrollBar.LargeChange = Math.Max(0, TextArea.TextView.DrawingPosition.Height);
			VScrollBar.SmallChange = Math.Max(0, TextArea.TextView.FontHeight);

			HScrollBar.LargeChange = Math.Max(0, TextArea.TextView.VisibleColumnCount - 1);
			HScrollBar.SmallChange = Math.Max(0, TextArea.TextView.SpaceWidth);
		}

		public void OptionsChanged()
		{
			TextArea.OptionsChanged();

			if (TextArea.TextEditorProperties.ShowHorizontalRuler)
			{
				if (_hRuler == null)
				{
					_hRuler = new HRuler(TextArea);
					Controls.Add(_hRuler);
					ResizeTextArea();
				}
				else
				{
					_hRuler.Invalidate();
				}
			}
			else
			{
				if (_hRuler != null)
				{
					Controls.Remove(_hRuler);
					_hRuler.Dispose();
					_hRuler = null;
					ResizeTextArea();
				}
			}

			AdjustScrollBars();
		}

		private void VScrollBarValueChanged(object sender, EventArgs e)
		{
			TextArea.VirtualTop = new Point(TextArea.VirtualTop.X, VScrollBar.Value);
			TextArea.Invalidate();
			AdjustScrollBars();
		}

		private void HScrollBarValueChanged(object sender, EventArgs e)
		{
			TextArea.VirtualTop = new Point(HScrollBar.Value * TextArea.TextView.WideSpaceWidth, TextArea.VirtualTop.Y);
			TextArea.Invalidate();
		}

		public void HandleMouseWheel(MouseEventArgs e)
		{
			var scrollDistance = _mouseWheelHandler.GetScrollAmount(e);
			if (scrollDistance == 0)
				return;
			if ((ModifierKeys & Keys.Control) != 0 && TextEditorProperties.MouseWheelTextZoom)
			{
				if (scrollDistance > 0)
					_motherTextEditorControl.Font = new Font(_motherTextEditorControl.Font.Name,
						_motherTextEditorControl.Font.Size + 1);
				else
					_motherTextEditorControl.Font = new Font(_motherTextEditorControl.Font.Name,
						Math.Max(6, _motherTextEditorControl.Font.Size - 1));
			}
			else
			{
				if (TextEditorProperties.MouseWheelScrollDown)
					scrollDistance = -scrollDistance;
				var newValue = VScrollBar.Value + VScrollBar.SmallChange * scrollDistance;
				VScrollBar.Value = Math.Max(VScrollBar.Minimum, Math.Min(VScrollBar.Maximum - VScrollBar.LargeChange + 1, newValue));
			}
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);
			if (DoHandleMousewheel)
				HandleMouseWheel(e);
		}

		public void ScrollToCaret()
		{
			ScrollTo(TextArea.Caret.Line, TextArea.Caret.Column);
		}

		public void ScrollTo(int line, int column)
		{
			if (_motherTextEditorControl.IsInUpdate)
			{
				_scrollToPosOnNextUpdate = new Point(column, line);
				return;
			}
			_scrollToPosOnNextUpdate = Point.Empty;

			ScrollTo(line);

			var curCharMin = HScrollBar.Value - HScrollBar.Minimum;
			var curCharMax = curCharMin + TextArea.TextView.VisibleColumnCount;

			var pos = TextArea.TextView.GetVisualColumn(line, column);

			if (TextArea.TextView.VisibleColumnCount < 0)
			{
				HScrollBar.Value = 0;
			}
			else
			{
				if (pos < curCharMin)
				{
					HScrollBar.Value = Math.Max(0, pos - _scrollMarginHeight);
				}
				else
				{
					if (pos > curCharMax)
						HScrollBar.Value =
							Math.Max(0, Math.Min(HScrollBar.Maximum, pos - TextArea.TextView.VisibleColumnCount + _scrollMarginHeight));
				}
			}
		}

		/// <summary>
		///     Ensure that <paramref name="line" /> is visible.
		/// </summary>
		public void ScrollTo(int line)
		{
			line = Math.Max(0, Math.Min(Document.TotalNumberOfLines - 1, line));
			line = Document.GetVisibleLine(line);
			var curLineMin = TextArea.TextView.FirstPhysicalLine;
			if (TextArea.TextView.LineHeightRemainder > 0)
				curLineMin++;

			if (line - _scrollMarginHeight + 3 < curLineMin)
			{
				VScrollBar.Value = Math.Max(0,
					Math.Min(VScrollBar.Maximum, (line - _scrollMarginHeight + 3) * TextArea.TextView.FontHeight));
				VScrollBarValueChanged(this, EventArgs.Empty);
			}
			else
			{
				var curLineMax = curLineMin + TextArea.TextView.VisibleLineCount;
				if (line + _scrollMarginHeight - 1 > curLineMax)
				{
					if (TextArea.TextView.VisibleLineCount == 1)
						VScrollBar.Value = Math.Max(0,
							Math.Min(VScrollBar.Maximum, (line - _scrollMarginHeight - 1) * TextArea.TextView.FontHeight));
					else
						VScrollBar.Value = Math.Min(VScrollBar.Maximum,
							(line - TextArea.TextView.VisibleLineCount + _scrollMarginHeight - 1) * TextArea.TextView.FontHeight);
					VScrollBarValueChanged(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		///     Scroll so that the specified line is centered.
		/// </summary>
		/// <param name="line">Line to center view on</param>
		/// <param name="treshold">
		///     If this action would cause scrolling by less than or equal to
		///     <paramref name="treshold" /> lines in any direction, don't scroll.
		///     Use -1 to always center the view.
		/// </param>
		public void CenterViewOn(int line, int treshold)
		{
			line = Math.Max(0, Math.Min(Document.TotalNumberOfLines - 1, line));
			// convert line to visible line:
			line = Document.GetVisibleLine(line);
			// subtract half the visible line count
			line -= TextArea.TextView.VisibleLineCount / 2;

			var curLineMin = TextArea.TextView.FirstPhysicalLine;
			if (TextArea.TextView.LineHeightRemainder > 0)
				curLineMin++;
			if (Math.Abs(curLineMin - line) > treshold)
			{
				// scroll:
				VScrollBar.Value = Math.Max(0,
					Math.Min(VScrollBar.Maximum, (line - _scrollMarginHeight + 3) * TextArea.TextView.FontHeight));
				VScrollBarValueChanged(this, EventArgs.Empty);
			}
		}

		public void JumpTo(int line)
		{
			line = Math.Max(0, Math.Min(line, Document.TotalNumberOfLines - 1));
			var text = Document.GetText(Document.GetLineSegment(line));
			JumpTo(line, text.Length - text.TrimStart().Length);
		}

		public void JumpTo(int line, int column)
		{
			TextArea.Focus();
			TextArea.SelectionManager.ClearSelection();
			TextArea.Caret.Position = new TextLocation(column, line);
			TextArea.SetDesiredColumn();
			ScrollToCaret();
		}

		public event MouseEventHandler ShowContextMenu;

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 0x007B)
				if (ShowContextMenu != null)
				{
					var lParam = m.LParam.ToInt64();
					int x = unchecked((short) (lParam & 0xffff));
					int y = unchecked((short) ((lParam & 0xffff0000) >> 16));
					if (x == -1 && y == -1)
					{
						var pos = Caret.ScreenPosition;
						ShowContextMenu(this, new MouseEventArgs(MouseButtons.None, 0, pos.X, pos.Y + TextArea.TextView.FontHeight, 0));
					}
					else
					{
						var pos = PointToClient(new Point(x, y));
						ShowContextMenu(this, new MouseEventArgs(MouseButtons.Right, 1, pos.X, pos.Y, 0));
					}
				}
			base.WndProc(ref m);
		}

		protected override void OnEnter(EventArgs e)
		{
			// SD2-1072 - Make sure the caret line is valid if anyone
			// has handlers for the Enter event.
			Caret.ValidateCaretPos();
			base.OnEnter(e);
		}
	}
}