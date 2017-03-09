// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Actions;
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor
{
	public class TextAreaClipboardHandler
	{
		public delegate bool ClipboardContainsTextDelegate();

		private const string LineSelectedType = "MSDEVLineSelect";

		/// <summary>
		///     Is called when CachedClipboardContainsText should be updated.
		///     If this property is null (the default value), the text editor uses
		///     System.Windows.Forms.Clipboard.ContainsText.
		/// </summary>
		/// <remarks>
		///     This property is useful if you want to prevent the default Clipboard.ContainsText
		///     behaviour that waits for the clipboard to be available - the clipboard might
		///     never become available if it is owned by a process that is paused by the debugger.
		/// </remarks>
		public static ClipboardContainsTextDelegate GetClipboardContainsText;

		// Code duplication: TextAreaClipboardHandler.cs also has SafeSetClipboard
		[ThreadStatic] private static int _safeSetClipboardDataVersion;
		private readonly TextArea _textArea;

		public TextAreaClipboardHandler(TextArea textArea)
		{
			_textArea = textArea;
			textArea.SelectionManager.SelectionChanged += DocumentSelectionChanged;
		}

		public bool EnableCut => _textArea.EnableCutOrPaste;

		public bool EnableCopy => true;

		public bool EnablePaste
		{
			get
			{
				if (!_textArea.EnableCutOrPaste)
					return false;
				var d = GetClipboardContainsText;
				if (d != null)
					return d();
				try
				{
					return Clipboard.ContainsText();
				}
				catch (ExternalException)
				{
					return false;
				}
			}
		}

		public bool EnableDelete
			=> _textArea.SelectionManager.HasSomethingSelected && !_textArea.SelectionManager.SelectionIsReadonly;

		public bool EnableSelectAll => true;

		private void DocumentSelectionChanged(object sender, EventArgs e)
		{
//			((DefaultWorkbench)WorkbenchSingleton.Workbench).UpdateToolbars();
		}

		// This is the type VS 2003 and 2005 use for flagging a whole line copy

		private bool CopyTextToClipboard(string stringToCopy, bool asLine)
		{
			if (stringToCopy.Length > 0)
			{
				var dataObject = new DataObject();
				dataObject.SetData(DataFormats.UnicodeText, true, stringToCopy);
				if (asLine)
				{
					var lineSelected = new MemoryStream(1);
					lineSelected.WriteByte(1);
					dataObject.SetData(LineSelectedType, false, lineSelected);
				}
				// Default has no highlighting, therefore we don't need RTF output
				if (_textArea.Document.HighlightingStrategy.Name != "Default")
					dataObject.SetData(DataFormats.Rtf, RtfWriter.GenerateRtf(_textArea));
				OnCopyText(new CopyTextEventArgs(stringToCopy));

				SafeSetClipboard(dataObject);
				return true;
			}
			return false;
		}

		private static void SafeSetClipboard(object dataObject)
		{
			// Work around ExternalException bug. (SD2-426)
			// Best reproducable inside Virtual PC.
			var version = unchecked(++_safeSetClipboardDataVersion);
			try
			{
				Clipboard.SetDataObject(dataObject, true);
			}
			catch (ExternalException)
			{
				var timer = new Timer();
				timer.Interval = 100;
				timer.Tick += delegate
				{
					timer.Stop();
					timer.Dispose();
					if (_safeSetClipboardDataVersion == version)
						try
						{
							Clipboard.SetDataObject(dataObject, true, 10, 50);
						}
						catch (ExternalException)
						{
						}
				};
				timer.Start();
			}
		}

		private bool CopyTextToClipboard(string stringToCopy)
		{
			return CopyTextToClipboard(stringToCopy, false);
		}

		public void Cut(object sender, EventArgs e)
		{
			if (_textArea.SelectionManager.HasSomethingSelected)
			{
				if (CopyTextToClipboard(_textArea.SelectionManager.SelectedText))
				{
					if (_textArea.SelectionManager.SelectionIsReadonly)
						return;
					// Remove text
					_textArea.BeginUpdate();
					_textArea.Caret.Position = _textArea.SelectionManager.SelectionCollection[0].StartPosition;
					_textArea.SelectionManager.RemoveSelectedText();
					_textArea.EndUpdate();
				}
			}
			else if (_textArea.Document.TextEditorProperties.CutCopyWholeLine)
			{
				// No text was selected, select and cut the entire line
				var curLineNr = _textArea.Document.GetLineNumberForOffset(_textArea.Caret.Offset);
				var lineWhereCaretIs = _textArea.Document.GetLineSegment(curLineNr);
				var caretLineText = _textArea.Document.GetText(lineWhereCaretIs.Offset, lineWhereCaretIs.TotalLength);
				_textArea.SelectionManager.SetSelection(_textArea.Document.OffsetToPosition(lineWhereCaretIs.Offset),
					_textArea.Document.OffsetToPosition(lineWhereCaretIs.Offset + lineWhereCaretIs.TotalLength));
				if (CopyTextToClipboard(caretLineText, true))
				{
					if (_textArea.SelectionManager.SelectionIsReadonly)
						return;
					// remove line
					_textArea.BeginUpdate();
					_textArea.Caret.Position = _textArea.Document.OffsetToPosition(lineWhereCaretIs.Offset);
					_textArea.SelectionManager.RemoveSelectedText();
					_textArea.Document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.PositionToEnd,
						new TextLocation(0, curLineNr)));
					_textArea.EndUpdate();
				}
			}
		}

		public void Copy(object sender, EventArgs e)
		{
			if (!CopyTextToClipboard(_textArea.SelectionManager.SelectedText) &&
			    _textArea.Document.TextEditorProperties.CutCopyWholeLine)
			{
				// No text was selected, select the entire line, copy it, and then deselect
				var curLineNr = _textArea.Document.GetLineNumberForOffset(_textArea.Caret.Offset);
				var lineWhereCaretIs = _textArea.Document.GetLineSegment(curLineNr);
				var caretLineText = _textArea.Document.GetText(lineWhereCaretIs.Offset, lineWhereCaretIs.TotalLength);
				CopyTextToClipboard(caretLineText, true);
			}
		}

		public void Paste(object sender, EventArgs e)
		{
			if (!_textArea.EnableCutOrPaste)
				return;
			// Clipboard.GetDataObject may throw an exception...
			for (var i = 0;; i++)
				try
				{
					var data = Clipboard.GetDataObject();
					if (data == null)
						return;
					var fullLine = data.GetDataPresent(LineSelectedType);
					if (data.GetDataPresent(DataFormats.UnicodeText))
					{
						var text = (string) data.GetData(DataFormats.UnicodeText);
						// we got NullReferenceExceptions here, apparently the clipboard can contain null strings
						if (!string.IsNullOrEmpty(text))
						{
							_textArea.Document.UndoStack.StartUndoGroup();
							try
							{
								if (_textArea.SelectionManager.HasSomethingSelected)
								{
									_textArea.Caret.Position = _textArea.SelectionManager.SelectionCollection[0].StartPosition;
									_textArea.SelectionManager.RemoveSelectedText();
								}
								if (fullLine)
								{
									var col = _textArea.Caret.Column;
									_textArea.Caret.Column = 0;
									if (!_textArea.IsReadOnly(_textArea.Caret.Offset))
										_textArea.InsertString(text);
									_textArea.Caret.Column = col;
								}
								else
								{
									// textArea.EnableCutOrPaste already checked readonly for this case
									_textArea.InsertString(text);
								}
							}
							finally
							{
								_textArea.Document.UndoStack.EndUndoGroup();
							}
						}
					}
					return;
				}
				catch (ExternalException)
				{
					// GetDataObject does not provide RetryTimes parameter
					if (i > 5) throw;
				}
		}

		public void Delete(object sender, EventArgs e)
		{
			new Delete().Execute(_textArea);
		}

		public void SelectAll(object sender, EventArgs e)
		{
			new SelectWholeDocument().Execute(_textArea);
		}

		protected virtual void OnCopyText(CopyTextEventArgs e)
		{
			if (CopyText != null)
				CopyText(this, e);
		}

		public event CopyTextEventHandler CopyText;
	}

	public delegate void CopyTextEventHandler(object sender, CopyTextEventArgs e);

	public class CopyTextEventArgs : EventArgs
	{
		public CopyTextEventArgs(string text)
		{
			Text = text;
		}

		public string Text { get; }
	}
}