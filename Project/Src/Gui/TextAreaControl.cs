﻿// <file>
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

		private readonly MouseWheelHandler _mouseWheelHandler = new MouseWheelHandler();

		private readonly int _scrollMarginHeight = 3;

		private bool _adjustScrollBarsOnNextUpdate;

		private bool _disposed;

		private HRuler _hRuler;

		private int[] _lineLengthCache;
		private TextEditorControl _motherTextEditorControl;
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
			Document.FoldingManager.FoldingsChanged += FoldingManagerOnFoldingsChanged;
		}

		private void FoldingManagerOnFoldingsChanged(object sender, EventArgs eventArgs)
		{
			ResizeTextArea();
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
			ResizeTextArea(true);
		}

		public void ResizeTextArea(bool forceRedraw = false)
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

			var fontHeight = TextArea.TextView.FontHeight;

			var totalLineHeight = GetNumberOfVisibleLines() * fontHeight;

			// If the lines cannot fit the TextArea draw the VScrollBar
			var drawVScrollBar = totalLineHeight > Height || TextArea.VirtualTop.Y > 0;

			var width = TextArea.TextView.DrawingPosition.Width;

			// Measuring string length is not exactly accurate, add 10 as a error margin
			var drawHScrollBar = width > 0 && GetMaximumVisibleLineWidth() + 10 > TextArea.TextView.DrawingPosition.Width ||
			                     TextArea.VirtualTop.X > 0;

			AdjustScrollBars();

			if (!forceRedraw && !IsRedrawRequired(drawVScrollBar, drawHScrollBar))
			{
				return;
			}

			VScrollBar.ValueChanged -= VScrollBarValueChanged;
			HScrollBar.ValueChanged -= HScrollBarValueChanged;

			if (drawHScrollBar && drawVScrollBar)
			{
				TextArea.Bounds = new Rectangle(0, y,
					Width - SystemInformation.HorizontalScrollBarArrowWidth,
					Height - SystemInformation.VerticalScrollBarArrowHeight - h);

				Controls.Remove(VScrollBar);
				Controls.Remove(HScrollBar);

				Controls.Add(VScrollBar);
				Controls.Add(HScrollBar);

				VScrollBar.ValueChanged += VScrollBarValueChanged;
				HScrollBar.ValueChanged += HScrollBarValueChanged;

				SetScrollBarBounds(true, true);
			}
			else if (drawVScrollBar)
			{
				TextArea.Bounds = new Rectangle(0, y,
					Width - SystemInformation.HorizontalScrollBarArrowWidth,
					Height);

				// If VScrollBar was not visible before scroll to the end
				if (!Controls.Contains(VScrollBar))
					VScrollBar.Value = VScrollBar.Maximum;
				else
					Controls.Remove(VScrollBar);

				Controls.Add(VScrollBar);
				Controls.Remove(HScrollBar);

				VScrollBar.ValueChanged += VScrollBarValueChanged;

				SetScrollBarBounds(true, false);
			}
			else if (drawHScrollBar)
			{
				TextArea.Bounds = new Rectangle(0, y,
					Width,
					Height - SystemInformation.VerticalScrollBarArrowHeight - h);

				Controls.Remove(HScrollBar);

				Controls.Add(HScrollBar);
				Controls.Remove(VScrollBar);

				HScrollBar.ValueChanged += HScrollBarValueChanged;

				SetScrollBarBounds(false, true);
			}
			else
			{
				Controls.Remove(VScrollBar);
				Controls.Remove(HScrollBar);

				TextArea.Bounds = new Rectangle(0, y,
					Width,
					Height);
			}

			TextArea.Invalidate();
		}

		private bool IsRedrawRequired(bool drawVScrollBar, bool drawHScrollBar)
		{
			var vScrollBarVisible = Controls.Contains(VScrollBar);
			var hScrollBarVisible = Controls.Contains(HScrollBar);

			if ((drawVScrollBar && !vScrollBarVisible) || (!drawVScrollBar && vScrollBarVisible))
			{
				return true;
			}

			if ((drawHScrollBar && !hScrollBarVisible) || (!drawHScrollBar && hScrollBarVisible))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the number of visible lines in the document by excluding lines that are not visible because of folding.
		/// </summary>
		/// <returns>The number of visible lines.</returns>
		private int GetNumberOfVisibleLines()
		{
			var lines = 0;

			for (var i = 0; i < Document.TotalNumberOfLines; i++)
			{
				if (Document.FoldingManager.IsLineVisible(i))
				{
					lines++;
				}
			}

			return lines;
		}

		public void SetScrollBarBounds(bool setVertical, bool setHorizontal)
		{
			if (setVertical)
			{
				VScrollBar.Bounds = new Rectangle(TextArea.Bounds.Right, 0, SystemInformation.HorizontalScrollBarArrowWidth,
					setHorizontal ? Height - SystemInformation.VerticalScrollBarArrowHeight : Height);
				VScrollBar.Invalidate();
			}

			if (setHorizontal)
			{
				HScrollBar.Bounds = new Rectangle(0,
					TextArea.Bounds.Bottom,
					Width - SystemInformation.HorizontalScrollBarArrowWidth,
					SystemInformation.VerticalScrollBarArrowHeight);
				HScrollBar.Invalidate();
			}
		}

		private int GetMaximumVisibleLineWidth()
		{
			var max = 0;
			using (var graphics = TextArea.CreateGraphics())
			{
				var firstLine = TextArea.TextView.FirstVisibleLine;
				var lastLine =
					Document.GetFirstLogicalLine(TextArea.TextView.FirstPhysicalLine + TextArea.TextView.VisibleLineCount);
				if (lastLine >= Document.TotalNumberOfLines)
					lastLine = Document.TotalNumberOfLines - 1;
				var tabIndent = Document.TextEditorProperties.TabIndent;
				var minTabWidth = 4;
				var wideSpaceWidth = TextArea.TextView.WideSpaceWidth;
				var fontContainer = TextEditorProperties.FontContainer;

				for (var lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
				{
					var lineSegment = Document.GetLineSegment(lineNumber);

					if (Document.FoldingManager.IsLineVisible(lineNumber))
					{
						var lineWidth = 0;
						var words = lineSegment.Words;
						var wordCount = words.Count;
						var offset = 0;

						for (var i = 0; i < wordCount; i++)
						{
							var word = words[i];

							switch (word.Type)
							{
								case TextWordType.Space:
									lineWidth += TextArea.TextView.SpaceWidth;
									break;
								case TextWordType.Tab:
									// go to next tab position
									lineWidth = (lineWidth + minTabWidth) / tabIndent / wideSpaceWidth * tabIndent * wideSpaceWidth;
									lineWidth += tabIndent * wideSpaceWidth;
									break;
								case TextWordType.Word:
									var text = Document.GetText(offset + lineSegment.Offset, word.Length);

									lineWidth += TextArea.TextView.MeasureStringWidth(graphics, text,
										word.GetFont(fontContainer) ?? fontContainer.RegularFont);
									break;
							}

							offset += word.Length;
						}

						max = Math.Max(max, lineWidth);
					}
				}
			}

			return max;
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

				ResizeTextArea();
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
			if (TextArea == null)
				return;

			_adjustScrollBarsOnNextUpdate = false;
			VScrollBar.Minimum = 0;
			
			VScrollBar.Maximum = (Document.GetVisibleLine(Document.TotalNumberOfLines - 1) + 1) * TextArea.TextView.FontHeight;
			var max = 0;

			var firstLine = TextArea.TextView.FirstVisibleLine;
			var lastLine = Document.GetFirstLogicalLine(TextArea.TextView.FirstPhysicalLine + TextArea.TextView.VisibleLineCount);
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
			HScrollBar.Maximum = Math.Max(max + 3, TextArea.TextView.VisibleColumnCount - 1);

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
			ResizeTextArea();
		}

		private void HScrollBarValueChanged(object sender, EventArgs e)
		{
			TextArea.VirtualTop = new Point(HScrollBar.Value * TextArea.TextView.WideSpaceWidth, TextArea.VirtualTop.Y);
			ResizeTextArea();
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