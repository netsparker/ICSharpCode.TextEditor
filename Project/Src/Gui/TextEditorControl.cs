// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     This class is used for a basic text area control
	/// </summary>
	[ToolboxBitmap("ICSharpCode.TextEditor.Resources.TextEditorControl.bmp")]
	[ToolboxItem(true)]
	public class TextEditorControl : TextEditorControlBase
	{
		private TextAreaControl _activeTextAreaControl;
		private readonly TextAreaControl _primaryTextArea;

		private PrintDocument _printDocument;
		private TextAreaControl _secondaryTextArea;
		private Splitter _textAreaSplitter;
		protected Panel TextAreaPanel = new Panel();

		public TextEditorControl()
		{
			SetStyle(ControlStyles.ContainerControl, true);

			TextAreaPanel.Dock = DockStyle.Fill;

			Document = new DocumentFactory().CreateDocument();
			Document.HighlightingStrategy = HighlightingStrategyFactory.CreateHighlightingStrategy();

			_primaryTextArea = new TextAreaControl(this);
			_activeTextAreaControl = _primaryTextArea;
			_primaryTextArea.TextArea.GotFocus += delegate { SetActiveTextAreaControl(_primaryTextArea); };
			_primaryTextArea.Dock = DockStyle.Fill;
			TextAreaPanel.Controls.Add(_primaryTextArea);
			InitializeTextAreaControl(_primaryTextArea);
			Controls.Add(TextAreaPanel);
			ResizeRedraw = true;
			Document.UpdateCommited += CommitUpdateRequested;
			OptionsChanged();
		}

		[Browsable(false)]
		public PrintDocument PrintDocument
		{
			get
			{
				if (_printDocument == null)
				{
					_printDocument = new PrintDocument();
					_printDocument.BeginPrint += BeginPrint;
					_printDocument.PrintPage += PrintPage;
				}
				return _printDocument;
			}
		}

		public override TextAreaControl ActiveTextAreaControl => _activeTextAreaControl;

		[Browsable(false)]
		public bool EnableUndo => Document.UndoStack.CanUndo;

		[Browsable(false)]
		public bool EnableRedo => Document.UndoStack.CanRedo;

		protected void SetActiveTextAreaControl(TextAreaControl value)
		{
			if (_activeTextAreaControl != value)
			{
				_activeTextAreaControl = value;

				if (ActiveTextAreaControlChanged != null)
					ActiveTextAreaControlChanged(this, EventArgs.Empty);
			}
		}

		public event EventHandler ActiveTextAreaControlChanged;

		protected virtual void InitializeTextAreaControl(TextAreaControl newControl)
		{
		}

		public override void OptionsChanged()
		{
			_primaryTextArea.OptionsChanged();
			if (_secondaryTextArea != null)
				_secondaryTextArea.OptionsChanged();
		}

		public void Split()
		{
			if (_secondaryTextArea == null)
			{
				_secondaryTextArea = new TextAreaControl(this);
				_secondaryTextArea.Dock = DockStyle.Bottom;
				_secondaryTextArea.Height = Height / 2;

				_secondaryTextArea.TextArea.GotFocus += delegate { SetActiveTextAreaControl(_secondaryTextArea); };

				_textAreaSplitter = new Splitter();
				_textAreaSplitter.BorderStyle = BorderStyle.FixedSingle;
				_textAreaSplitter.Height = 8;
				_textAreaSplitter.Dock = DockStyle.Bottom;
				TextAreaPanel.Controls.Add(_textAreaSplitter);
				TextAreaPanel.Controls.Add(_secondaryTextArea);
				InitializeTextAreaControl(_secondaryTextArea);
				_secondaryTextArea.OptionsChanged();
			}
			else
			{
				SetActiveTextAreaControl(_primaryTextArea);

				TextAreaPanel.Controls.Remove(_secondaryTextArea);
				TextAreaPanel.Controls.Remove(_textAreaSplitter);

				_secondaryTextArea.Dispose();
				_textAreaSplitter.Dispose();
				_secondaryTextArea = null;
				_textAreaSplitter = null;
			}
		}

		public void Undo()
		{
			if (Document.ReadOnly)
				return;
			if (Document.UndoStack.CanUndo)
			{
				BeginUpdate();
				Document.UndoStack.Undo();

				Document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.WholeTextArea));
				_primaryTextArea.TextArea.UpdateMatchingBracket();
				if (_secondaryTextArea != null)
					_secondaryTextArea.TextArea.UpdateMatchingBracket();
				EndUpdate();
			}
		}

		public void Redo()
		{
			if (Document.ReadOnly)
				return;
			if (Document.UndoStack.CanRedo)
			{
				BeginUpdate();
				Document.UndoStack.Redo();

				Document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.WholeTextArea));
				_primaryTextArea.TextArea.UpdateMatchingBracket();
				if (_secondaryTextArea != null)
					_secondaryTextArea.TextArea.UpdateMatchingBracket();
				EndUpdate();
			}
		}

		public virtual void SetHighlighting(string name)
		{
			Document.HighlightingStrategy = HighlightingStrategyFactory.CreateHighlightingStrategy(name);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_printDocument != null)
				{
					_printDocument.BeginPrint -= BeginPrint;
					_printDocument.PrintPage -= PrintPage;
					_printDocument = null;
				}
				Document.UndoStack.ClearAll();
				Document.UpdateCommited -= CommitUpdateRequested;
				if (TextAreaPanel != null)
				{
					if (_secondaryTextArea != null)
					{
						_secondaryTextArea.Dispose();
						_textAreaSplitter.Dispose();
						_secondaryTextArea = null;
						_textAreaSplitter = null;
					}
					if (_primaryTextArea != null)
						_primaryTextArea.Dispose();
					TextAreaPanel.Dispose();
					TextAreaPanel = null;
				}
			}
			base.Dispose(disposing);
		}

		#region Update Methods

		public override void EndUpdate()
		{
			base.EndUpdate();
			Document.CommitUpdate();
			if (!IsInUpdate)
				ActiveTextAreaControl.Caret.OnEndUpdate();
		}

		private void CommitUpdateRequested(object sender, EventArgs e)
		{
			if (IsInUpdate)
				return;
			foreach (var update in Document.UpdateQueue)
				switch (update.TextAreaUpdateType)
				{
					case TextAreaUpdateType.PositionToEnd:
						_primaryTextArea.TextArea.UpdateToEnd(update.Position.Y);
						if (_secondaryTextArea != null)
							_secondaryTextArea.TextArea.UpdateToEnd(update.Position.Y);
						break;
					case TextAreaUpdateType.PositionToLineEnd:
					case TextAreaUpdateType.SingleLine:
						_primaryTextArea.TextArea.UpdateLine(update.Position.Y);
						if (_secondaryTextArea != null)
							_secondaryTextArea.TextArea.UpdateLine(update.Position.Y);
						break;
					case TextAreaUpdateType.SinglePosition:
						_primaryTextArea.TextArea.UpdateLine(update.Position.Y, update.Position.X, update.Position.X);
						if (_secondaryTextArea != null)
							_secondaryTextArea.TextArea.UpdateLine(update.Position.Y, update.Position.X, update.Position.X);
						break;
					case TextAreaUpdateType.LinesBetween:
						_primaryTextArea.TextArea.UpdateLines(update.Position.X, update.Position.Y);
						if (_secondaryTextArea != null)
							_secondaryTextArea.TextArea.UpdateLines(update.Position.X, update.Position.Y);
						break;
					case TextAreaUpdateType.WholeTextArea:
						_primaryTextArea.TextArea.Invalidate();
						if (_secondaryTextArea != null)
							_secondaryTextArea.TextArea.Invalidate();
						break;
				}
			Document.UpdateQueue.Clear();
//			this.primaryTextArea.TextArea.Update();
//			if (this.secondaryTextArea != null) {
//				this.secondaryTextArea.TextArea.Update();
//			}
		}

		#endregion

		#region Printing routines

		private int _curLineNr;
		private float _curTabIndent;
		private StringFormat _printingStringFormat;

		private void BeginPrint(object sender, PrintEventArgs ev)
		{
			_curLineNr = 0;
			_printingStringFormat = (StringFormat) StringFormat.GenericTypographic.Clone();

			// 100 should be enough for everyone ...err ?
			var tabStops = new float[100];
			for (var i = 0; i < tabStops.Length; ++i)
				tabStops[i] = TabIndent * _primaryTextArea.TextArea.TextView.WideSpaceWidth;

			_printingStringFormat.SetTabStops(0, tabStops);
		}

		private void Advance(ref float x, ref float y, float maxWidth, float size, float fontHeight)
		{
			if (x + size < maxWidth)
			{
				x += size;
			}
			else
			{
				x = _curTabIndent;
				y += fontHeight;
			}
		}

		// btw. I hate source code duplication ... but this time I don't care !!!!
		private float MeasurePrintingHeight(Graphics g, LineSegment line, float maxWidth)
		{
			float xPos = 0;
			float yPos = 0;
			var fontHeight = Font.GetHeight(g);
//			bool  gotNonWhitespace = false;
			_curTabIndent = 0;
			var fontContainer = TextEditorProperties.FontContainer;
			foreach (var word in line.Words)
				switch (word.Type)
				{
					case TextWordType.Space:
						Advance(ref xPos, ref yPos, maxWidth, _primaryTextArea.TextArea.TextView.SpaceWidth, fontHeight);
//						if (!gotNonWhitespace) {
//							curTabIndent = xPos;
//						}
						break;
					case TextWordType.Tab:
						Advance(ref xPos, ref yPos, maxWidth, TabIndent * _primaryTextArea.TextArea.TextView.WideSpaceWidth, fontHeight);
//						if (!gotNonWhitespace) {
//							curTabIndent = xPos;
//						}
						break;
					case TextWordType.Word:
//						if (!gotNonWhitespace) {
//							gotNonWhitespace = true;
//							curTabIndent    += TabIndent * primaryTextArea.TextArea.TextView.GetWidth(' ');
//						}
						var drawingSize = g.MeasureString(word.Word, word.GetFont(fontContainer), new SizeF(maxWidth, fontHeight * 100),
							_printingStringFormat);
						Advance(ref xPos, ref yPos, maxWidth, drawingSize.Width, fontHeight);
						break;
				}
			return yPos + fontHeight;
		}

		private void DrawLine(Graphics g, LineSegment line, float yPos, RectangleF margin)
		{
			float xPos = 0;
			var fontHeight = Font.GetHeight(g);
//			bool  gotNonWhitespace = false;
			_curTabIndent = 0;

			var fontContainer = TextEditorProperties.FontContainer;
			foreach (var word in line.Words)
				switch (word.Type)
				{
					case TextWordType.Space:
						Advance(ref xPos, ref yPos, margin.Width, _primaryTextArea.TextArea.TextView.SpaceWidth, fontHeight);
//						if (!gotNonWhitespace) {
//							curTabIndent = xPos;
//						}
						break;
					case TextWordType.Tab:
						Advance(ref xPos, ref yPos, margin.Width, TabIndent * _primaryTextArea.TextArea.TextView.WideSpaceWidth,
							fontHeight);
//						if (!gotNonWhitespace) {
//							curTabIndent = xPos;
//						}
						break;
					case TextWordType.Word:
//						if (!gotNonWhitespace) {
//							gotNonWhitespace = true;
//							curTabIndent    += TabIndent * primaryTextArea.TextArea.TextView.GetWidth(' ');
//						}
						g.DrawString(word.Word, word.GetFont(fontContainer), BrushRegistry.GetBrush(word.Color), xPos + margin.X, yPos);
						var drawingSize = g.MeasureString(word.Word, word.GetFont(fontContainer),
							new SizeF(margin.Width, fontHeight * 100), _printingStringFormat);
						Advance(ref xPos, ref yPos, margin.Width, drawingSize.Width, fontHeight);
						break;
				}
		}

		private void PrintPage(object sender, PrintPageEventArgs ev)
		{
			var g = ev.Graphics;
			float yPos = ev.MarginBounds.Top;

			while (_curLineNr < Document.TotalNumberOfLines)
			{
				var curLine = Document.GetLineSegment(_curLineNr);
				if (curLine.Words != null)
				{
					var drawingHeight = MeasurePrintingHeight(g, curLine, ev.MarginBounds.Width);
					if (drawingHeight + yPos > ev.MarginBounds.Bottom)
						break;

					DrawLine(g, curLine, yPos, ev.MarginBounds);
					yPos += drawingHeight;
				}
				++_curLineNr;
			}

			// If more lines exist, print another page.
			ev.HasMorePages = _curLineNr < Document.TotalNumberOfLines;
		}

		#endregion
	}
}