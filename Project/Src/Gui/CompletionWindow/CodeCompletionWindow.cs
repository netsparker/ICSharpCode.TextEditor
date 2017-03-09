// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor.Gui.CompletionWindow
{
	public class CodeCompletionWindow : AbstractCompletionWindow
	{
		private const int ScrollbarWidth = 16;
		private const int MaxListLength = 10;
		private CodeCompletionListView _codeCompletionListView;
		private readonly ICompletionData[] _completionData;
		private readonly ICompletionDataProvider _dataProvider;
		private DeclarationViewWindow _declarationViewWindow;
		private readonly IDocument _document;
		private int _endOffset;
		private readonly bool _fixedListViewWidth = true;

		private bool _inScrollUpdate;

		private readonly MouseWheelHandler _mouseWheelHandler = new MouseWheelHandler();
		private readonly bool _showDeclarationWindow = true;

		private int _startOffset;
		private readonly VScrollBar _vScrollBar = new VScrollBar();
		private Rectangle _workingScreen;

		private CodeCompletionWindow(ICompletionDataProvider completionDataProvider, ICompletionData[] completionData,
			Form parentForm,
			TextEditorControl control, bool showDeclarationWindow, bool fixedListViewWidth) : base(parentForm, control)
		{
			_dataProvider = completionDataProvider;
			_completionData = completionData;
			_document = control.Document;
			_showDeclarationWindow = showDeclarationWindow;
			_fixedListViewWidth = fixedListViewWidth;

			_workingScreen = Screen.GetWorkingArea(Location);
			_startOffset = control.ActiveTextAreaControl.Caret.Offset + 1;
			_endOffset = _startOffset;
			if (completionDataProvider.PreSelection != null)
			{
				_startOffset -= completionDataProvider.PreSelection.Length + 1;
				_endOffset--;
			}

			_codeCompletionListView = new CodeCompletionListView(completionData);
			_codeCompletionListView.ImageList = completionDataProvider.ImageList;
			_codeCompletionListView.Dock = DockStyle.Fill;
			_codeCompletionListView.SelectedItemChanged += CodeCompletionListViewSelectedItemChanged;
			_codeCompletionListView.DoubleClick += CodeCompletionListViewDoubleClick;
			_codeCompletionListView.Click += CodeCompletionListViewClick;
			Controls.Add(_codeCompletionListView);

			if (completionData.Length > MaxListLength)
			{
				_vScrollBar.Dock = DockStyle.Right;
				_vScrollBar.Minimum = 0;
				_vScrollBar.Maximum = completionData.Length - 1;
				_vScrollBar.SmallChange = 1;
				_vScrollBar.LargeChange = MaxListLength;
				_codeCompletionListView.FirstItemChanged += CodeCompletionListViewFirstItemChanged;
				Controls.Add(_vScrollBar);
			}

			DrawingSize = GetListViewSize();
			SetLocation();

			if (_declarationViewWindow == null)
				_declarationViewWindow = new DeclarationViewWindow(parentForm);
			SetDeclarationViewLocation();
			_declarationViewWindow.ShowDeclarationViewWindow();
			_declarationViewWindow.MouseMove += ControlMouseMove;
			control.Focus();
			CodeCompletionListViewSelectedItemChanged(this, EventArgs.Empty);

			if (completionDataProvider.DefaultIndex >= 0)
				_codeCompletionListView.SelectIndex(completionDataProvider.DefaultIndex);

			if (completionDataProvider.PreSelection != null)
				CaretOffsetChanged(this, EventArgs.Empty);

			_vScrollBar.ValueChanged += VScrollBarValueChanged;
			_document.DocumentAboutToBeChanged += DocumentAboutToBeChanged;
		}

		/// <summary>
		///     When this flag is set, code completion closes if the caret moves to the
		///     beginning of the allowed range. This is useful in Ctrl+Space and "complete when typing",
		///     but not in dot-completion.
		/// </summary>
		public bool CloseWhenCaretAtBeginning { get; set; }

		public static CodeCompletionWindow ShowCompletionWindow(Form parent, TextEditorControl control, string fileName,
			ICompletionDataProvider completionDataProvider, char firstChar)
		{
			return ShowCompletionWindow(parent, control, fileName, completionDataProvider, firstChar, true, true);
		}

		public static CodeCompletionWindow ShowCompletionWindow(Form parent, TextEditorControl control, string fileName,
			ICompletionDataProvider completionDataProvider, char firstChar, bool showDeclarationWindow, bool fixedListViewWidth)
		{
			var completionData = completionDataProvider.GenerateCompletionData(fileName,
				control.ActiveTextAreaControl.TextArea, firstChar);
			if (completionData == null || completionData.Length == 0)
				return null;
			var codeCompletionWindow = new CodeCompletionWindow(completionDataProvider, completionData, parent,
				control, showDeclarationWindow, fixedListViewWidth);
			codeCompletionWindow.CloseWhenCaretAtBeginning = firstChar == '\0';
			codeCompletionWindow.ShowCompletionWindow();
			return codeCompletionWindow;
		}

		private void CodeCompletionListViewFirstItemChanged(object sender, EventArgs e)
		{
			if (_inScrollUpdate) return;
			_inScrollUpdate = true;
			_vScrollBar.Value = Math.Min(_vScrollBar.Maximum, _codeCompletionListView.FirstItem);
			_inScrollUpdate = false;
		}

		private void VScrollBarValueChanged(object sender, EventArgs e)
		{
			if (_inScrollUpdate) return;
			_inScrollUpdate = true;
			_codeCompletionListView.FirstItem = _vScrollBar.Value;
			_codeCompletionListView.Refresh();
			Control.ActiveTextAreaControl.TextArea.Focus();
			_inScrollUpdate = false;
		}

		private void SetDeclarationViewLocation()
		{
			//  This method uses the side with more free space
			var leftSpace = Bounds.Left - _workingScreen.Left;
			var rightSpace = _workingScreen.Right - Bounds.Right;
			Point pos;
			// The declaration view window has better line break when used on
			// the right side, so prefer the right side to the left.
			if (rightSpace * 2 > leftSpace)
			{
				_declarationViewWindow.FixedWidth = false;
				pos = new Point(Bounds.Right, Bounds.Top);
				if (_declarationViewWindow.Location != pos)
					_declarationViewWindow.Location = pos;
			}
			else
			{
				_declarationViewWindow.Width =
					_declarationViewWindow.GetRequiredLeftHandSideWidth(new Point(Bounds.Left, Bounds.Top));
				_declarationViewWindow.FixedWidth = true;
				if (Bounds.Left < _declarationViewWindow.Width)
					pos = new Point(0, Bounds.Top);
				else
					pos = new Point(Bounds.Left - _declarationViewWindow.Width, Bounds.Top);
				if (_declarationViewWindow.Location != pos)
					_declarationViewWindow.Location = pos;
				_declarationViewWindow.Refresh();
			}
		}

		protected override void SetLocation()
		{
			base.SetLocation();
			if (_declarationViewWindow != null)
				SetDeclarationViewLocation();
		}

		public void HandleMouseWheel(MouseEventArgs e)
		{
			var scrollDistance = _mouseWheelHandler.GetScrollAmount(e);
			if (scrollDistance == 0)
				return;
			if (Control.TextEditorProperties.MouseWheelScrollDown)
				scrollDistance = -scrollDistance;
			var newValue = _vScrollBar.Value + _vScrollBar.SmallChange * scrollDistance;
			_vScrollBar.Value = Math.Max(_vScrollBar.Minimum,
				Math.Min(_vScrollBar.Maximum - _vScrollBar.LargeChange + 1, newValue));
		}

		private void CodeCompletionListViewSelectedItemChanged(object sender, EventArgs e)
		{
			var data = _codeCompletionListView.SelectedCompletionData;
			if (_showDeclarationWindow && data != null && data.Description != null && data.Description.Length > 0)
			{
				_declarationViewWindow.Description = data.Description;
				SetDeclarationViewLocation();
			}
			else
			{
				_declarationViewWindow.Description = null;
			}
		}

		public override bool ProcessKeyEvent(char ch)
		{
			switch (_dataProvider.ProcessKey(ch))
			{
				case CompletionDataProviderKeyResult.BeforeStartKey:
					// increment start+end, then process as normal char
					++_startOffset;
					++_endOffset;
					return base.ProcessKeyEvent(ch);
				case CompletionDataProviderKeyResult.NormalKey:
					// just process normally
					return base.ProcessKeyEvent(ch);
				case CompletionDataProviderKeyResult.InsertionKey:
					return InsertSelectedItem(ch);
				default:
					throw new InvalidOperationException("Invalid return value of dataProvider.ProcessKey");
			}
		}

		private void DocumentAboutToBeChanged(object sender, DocumentEventArgs e)
		{
			// => startOffset test required so that this startOffset/endOffset are not incremented again
			//    for BeforeStartKey characters
			if (e.Offset >= _startOffset && e.Offset <= _endOffset)
			{
				if (e.Length > 0)
					_endOffset -= e.Length;
				if (!string.IsNullOrEmpty(e.Text))
					_endOffset += e.Text.Length;
			}
		}

		protected override void CaretOffsetChanged(object sender, EventArgs e)
		{
			var offset = Control.ActiveTextAreaControl.Caret.Offset;
			if (offset == _startOffset)
			{
				if (CloseWhenCaretAtBeginning)
					Close();
				return;
			}
			if (offset < _startOffset || offset > _endOffset)
				Close();
			else
				_codeCompletionListView.SelectItemWithStart(Control.Document.GetText(_startOffset, offset - _startOffset));
		}

		protected override bool ProcessTextAreaKey(Keys keyData)
		{
			if (!Visible)
				return false;

			switch (keyData)
			{
				case Keys.Home:
					_codeCompletionListView.SelectIndex(0);
					return true;
				case Keys.End:
					_codeCompletionListView.SelectIndex(_completionData.Length - 1);
					return true;
				case Keys.PageDown:
					_codeCompletionListView.PageDown();
					return true;
				case Keys.PageUp:
					_codeCompletionListView.PageUp();
					return true;
				case Keys.Down:
					_codeCompletionListView.SelectNextItem();
					return true;
				case Keys.Up:
					_codeCompletionListView.SelectPrevItem();
					return true;
				case Keys.Tab:
					InsertSelectedItem('\t');
					return true;
				case Keys.Return:
					InsertSelectedItem('\n');
					return true;
			}
			return base.ProcessTextAreaKey(keyData);
		}

		private void CodeCompletionListViewDoubleClick(object sender, EventArgs e)
		{
			InsertSelectedItem('\0');
		}

		private void CodeCompletionListViewClick(object sender, EventArgs e)
		{
			Control.ActiveTextAreaControl.TextArea.Focus();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_document.DocumentAboutToBeChanged -= DocumentAboutToBeChanged;
				if (_codeCompletionListView != null)
				{
					_codeCompletionListView.Dispose();
					_codeCompletionListView = null;
				}
				if (_declarationViewWindow != null)
				{
					_declarationViewWindow.Dispose();
					_declarationViewWindow = null;
				}
			}
			base.Dispose(disposing);
		}

		private bool InsertSelectedItem(char ch)
		{
			_document.DocumentAboutToBeChanged -= DocumentAboutToBeChanged;
			var data = _codeCompletionListView.SelectedCompletionData;
			var result = false;
			if (data != null)
			{
				Control.BeginUpdate();

				try
				{
					if (_endOffset - _startOffset > 0)
						Control.Document.Remove(_startOffset, _endOffset - _startOffset);
					Debug.Assert(_startOffset <= _document.TextLength);
					result = _dataProvider.InsertAction(data, Control.ActiveTextAreaControl.TextArea, _startOffset, ch);
				}
				finally
				{
					Control.EndUpdate();
				}
			}
			Close();
			return result;
		}

		private Size GetListViewSize()
		{
			var height = _codeCompletionListView.ItemHeight * Math.Min(MaxListLength, _completionData.Length);
			var width = _codeCompletionListView.ItemHeight * 10;
			if (!_fixedListViewWidth)
				width = GetListViewWidth(width, height);
			return new Size(width, height);
		}

		/// <summary>
		///     Gets the list view width large enough to handle the longest completion data
		///     text string.
		/// </summary>
		/// <param name="defaultWidth">The default width of the list view.</param>
		/// <param name="height">
		///     The height of the list view.  This is
		///     used to determine if the scrollbar is visible.
		/// </param>
		/// <returns>
		///     The list view width to accommodate the longest completion
		///     data text string; otherwise the default width.
		/// </returns>
		private int GetListViewWidth(int defaultWidth, int height)
		{
			float width = defaultWidth;
			using (var graphics = _codeCompletionListView.CreateGraphics())
			{
				for (var i = 0; i < _completionData.Length; ++i)
				{
					var itemWidth = graphics.MeasureString(_completionData[i].Text, _codeCompletionListView.Font).Width;
					if (itemWidth > width)
						width = itemWidth;
				}
			}

			float totalItemsHeight = _codeCompletionListView.ItemHeight * _completionData.Length;
			if (totalItemsHeight > height)
				width += ScrollbarWidth; // Compensate for scroll bar.
			return (int) width;
		}
	}
}