// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     In this enumeration are all caret modes listed.
	/// </summary>
	public enum CaretMode
	{
		/// <summary>
		///     If the caret is in insert mode typed characters will be
		///     inserted at the caret position
		/// </summary>
		InsertMode,

		/// <summary>
		///     If the caret is in overwirte mode typed characters will
		///     overwrite the character at the caret position
		/// </summary>
		OverwriteMode
	}


	public class Caret : IDisposable
	{
		private static bool _caretCreated;
		private readonly CaretImplementation _caretImplementation;
		private CaretMode _caretMode;
		private int _column;
		private Point _currentPos = new Point(-1, -1);

		private bool _firePositionChangedAfterUpdateEnd;
		private bool _hidden = true;
		private Ime _ime;
		private int _line;

		private int _oldLine = -1;
		private bool _outstandingUpdate;
		private TextArea _textArea;

		public Caret(TextArea textArea)
		{
			_textArea = textArea;
			textArea.GotFocus += GotFocus;
			textArea.LostFocus += LostFocus;
			if (Environment.OSVersion.Platform == PlatformID.Unix)
				_caretImplementation = new ManagedCaret(this);
			else
				_caretImplementation = new Win32Caret(this);
		}

		/// <value>
		///     The 'prefered' xPos in which the caret moves, when it is moved
		///     up/down. Measured in pixels, not in characters!
		/// </value>
		public int DesiredColumn { get; set; }

		/// <value>
		///     The current caret mode.
		/// </value>
		public CaretMode CaretMode
		{
			get { return _caretMode; }
			set
			{
				_caretMode = value;
				OnCaretModeChanged(EventArgs.Empty);
			}
		}

		public int Line
		{
			get { return _line; }
			set
			{
				_line = value;
				ValidateCaretPos();
				UpdateCaretPosition();
				OnPositionChanged(EventArgs.Empty);
			}
		}

		public int Column
		{
			get { return _column; }
			set
			{
				_column = value;
				ValidateCaretPos();
				UpdateCaretPosition();
				OnPositionChanged(EventArgs.Empty);
			}
		}

		public TextLocation Position
		{
			get { return new TextLocation(_column, _line); }
			set
			{
				_line = value.Y;
				_column = value.X;
				ValidateCaretPos();
				UpdateCaretPosition();
				OnPositionChanged(EventArgs.Empty);
			}
		}

		public int Offset => _textArea.Document.PositionToOffset(Position);

		public Point ScreenPosition
		{
			get
			{
				var xpos = _textArea.TextView.GetDrawingXPos(_line, _column);
				return new Point(_textArea.TextView.DrawingPosition.X + xpos,
					_textArea.TextView.DrawingPosition.Y
					+ _textArea.Document.GetVisibleLine(_line) * _textArea.TextView.FontHeight
					- _textArea.TextView.TextArea.VirtualTop.Y);
			}
		}

		public void Dispose()
		{
			_textArea.GotFocus -= GotFocus;
			_textArea.LostFocus -= LostFocus;
			_textArea = null;
			_caretImplementation.Dispose();
		}

		public TextLocation ValidatePosition(TextLocation pos)
		{
			var line = Math.Max(0, Math.Min(_textArea.Document.TotalNumberOfLines - 1, pos.Y));
			var column = Math.Max(0, pos.X);

			if (column == int.MaxValue || !_textArea.TextEditorProperties.AllowCaretBeyondEol)
			{
				var lineSegment = _textArea.Document.GetLineSegment(line);
				column = Math.Min(column, lineSegment.Length);
			}
			return new TextLocation(column, line);
		}

		/// <remarks>
		///     If the caret position is outside the document text bounds
		///     it is set to the correct position by calling ValidateCaretPos.
		/// </remarks>
		public void ValidateCaretPos()
		{
			_line = Math.Max(0, Math.Min(_textArea.Document.TotalNumberOfLines - 1, _line));
			_column = Math.Max(0, _column);

			if (_column == int.MaxValue || !_textArea.TextEditorProperties.AllowCaretBeyondEol)
			{
				var lineSegment = _textArea.Document.GetLineSegment(_line);
				_column = Math.Min(_column, lineSegment.Length);
			}
		}

		private void CreateCaret()
		{
			while (!_caretCreated)
				switch (_caretMode)
				{
					case CaretMode.InsertMode:
						_caretCreated = _caretImplementation.Create(2, _textArea.TextView.FontHeight);
						break;
					case CaretMode.OverwriteMode:
						_caretCreated = _caretImplementation.Create(_textArea.TextView.SpaceWidth, _textArea.TextView.FontHeight);
						break;
				}
			if (_currentPos.X < 0)
			{
				ValidateCaretPos();
				_currentPos = ScreenPosition;
			}
			_caretImplementation.SetPosition(_currentPos.X, _currentPos.Y);
			_caretImplementation.Show();
		}

		public void RecreateCaret()
		{
			Log("RecreateCaret");
			DisposeCaret();
			if (!_hidden)
				CreateCaret();
		}

		private void DisposeCaret()
		{
			if (_caretCreated)
			{
				_caretCreated = false;
				_caretImplementation.Hide();
				_caretImplementation.Destroy();
			}
		}

		private void GotFocus(object sender, EventArgs e)
		{
			Log("GotFocus, IsInUpdate=" + _textArea.MotherTextEditorControl.IsInUpdate);
			_hidden = false;
			if (!_textArea.MotherTextEditorControl.IsInUpdate)
			{
				CreateCaret();
				UpdateCaretPosition();
			}
		}

		private void LostFocus(object sender, EventArgs e)
		{
			Log("LostFocus");
			_hidden = true;
			DisposeCaret();
		}

		internal void OnEndUpdate()
		{
			if (_outstandingUpdate)
				UpdateCaretPosition();
		}

		private void PaintCaretLine(Graphics g)
		{
			if (!_textArea.Document.TextEditorProperties.CaretLine)
				return;

			var caretLineColor = _textArea.Document.HighlightingStrategy.GetColorFor("CaretLine");

			g.DrawLine(BrushRegistry.GetDotPen(caretLineColor.Color),
				_currentPos.X,
				0,
				_currentPos.X,
				_textArea.DisplayRectangle.Height);
		}

		public void UpdateCaretPosition()
		{
			Log("UpdateCaretPosition");

			if (_textArea.TextEditorProperties.CaretLine)
			{
				_textArea.Invalidate();
			}
			else
			{
				if (_caretImplementation.RequireRedrawOnPositionChange)
				{
					_textArea.UpdateLine(_oldLine);
					if (_line != _oldLine)
						_textArea.UpdateLine(_line);
				}
				else
				{
					if (_textArea.MotherTextAreaControl.TextEditorProperties.LineViewerStyle == LineViewerStyle.FullRow &&
					    _oldLine != _line)
					{
						_textArea.UpdateLine(_oldLine);
						_textArea.UpdateLine(_line);
					}
				}
			}
			_oldLine = _line;


			if (_hidden || _textArea.MotherTextEditorControl.IsInUpdate)
			{
				_outstandingUpdate = true;
				return;
			}
			_outstandingUpdate = false;
			ValidateCaretPos();
			var lineNr = _line;
			var xpos = _textArea.TextView.GetDrawingXPos(lineNr, _column);
			//LineSegment lineSegment = textArea.Document.GetLineSegment(lineNr);
			var pos = ScreenPosition;
			if (xpos >= 0)
			{
				CreateCaret();
				var success = _caretImplementation.SetPosition(pos.X, pos.Y);
				if (!success)
				{
					_caretImplementation.Destroy();
					_caretCreated = false;
					UpdateCaretPosition();
				}
			}
			else
			{
				_caretImplementation.Destroy();
			}

			// set the input method editor location
			if (_ime == null)
			{
				_ime = new Ime(_textArea.Handle, _textArea.Document.TextEditorProperties.Font);
			}
			else
			{
				_ime.HWnd = _textArea.Handle;
				_ime.Font = _textArea.Document.TextEditorProperties.Font;
			}
			_ime.SetImeWindowLocation(pos.X, pos.Y);

			_currentPos = pos;
		}

		[Conditional("DEBUG")]
		private static void Log(string text)
		{
			//Console.WriteLine(text);
		}

		private void FirePositionChangedAfterUpdateEnd(object sender, EventArgs e)
		{
			OnPositionChanged(EventArgs.Empty);
		}

		protected virtual void OnPositionChanged(EventArgs e)
		{
			if (_textArea.MotherTextEditorControl.IsInUpdate)
			{
				if (_firePositionChangedAfterUpdateEnd == false)
				{
					_firePositionChangedAfterUpdateEnd = true;
					_textArea.Document.UpdateCommited += FirePositionChangedAfterUpdateEnd;
				}
				return;
			}
			if (_firePositionChangedAfterUpdateEnd)
			{
				_textArea.Document.UpdateCommited -= FirePositionChangedAfterUpdateEnd;
				_firePositionChangedAfterUpdateEnd = false;
			}

			var foldings = _textArea.Document.FoldingManager.GetFoldingsFromPosition(_line, _column);
			var shouldUpdate = false;
			foreach (var foldMarker in foldings)
			{
				shouldUpdate |= foldMarker.IsFolded;
				foldMarker.IsFolded = false;
			}

			if (shouldUpdate)
				_textArea.Document.FoldingManager.NotifyFoldingsChanged(EventArgs.Empty);

			if (PositionChanged != null)
				PositionChanged(this, e);
			_textArea.ScrollToCaret();
		}

		protected virtual void OnCaretModeChanged(EventArgs e)
		{
			if (CaretModeChanged != null)
				CaretModeChanged(this, e);
			_caretImplementation.Hide();
			_caretImplementation.Destroy();
			_caretCreated = false;
			CreateCaret();
			_caretImplementation.Show();
		}

		/// <remarks>
		///     Is called each time the caret is moved.
		/// </remarks>
		public event EventHandler PositionChanged;

		/// <remarks>
		///     Is called each time the CaretMode has changed.
		/// </remarks>
		public event EventHandler CaretModeChanged;

		#region Caret implementation

		internal void PaintCaret(Graphics g)
		{
			_caretImplementation.PaintCaret(g);
			PaintCaretLine(g);
		}

		private abstract class CaretImplementation : IDisposable
		{
			public bool RequireRedrawOnPositionChange;

			public virtual void Dispose()
			{
				Destroy();
			}

			public abstract bool Create(int width, int height);
			public abstract void Hide();
			public abstract void Show();
			public abstract bool SetPosition(int x, int y);
			public abstract void PaintCaret(Graphics g);
			public abstract void Destroy();
		}

		private class ManagedCaret : CaretImplementation
		{
			private bool _blink = true;
			private readonly Caret _parentCaret;
			private readonly TextArea _textArea;
			private readonly Timer _timer = new Timer {Interval = 300};
			private bool _visible;
			private int _x, _y, _width, _height;

			public ManagedCaret(Caret caret)
			{
				RequireRedrawOnPositionChange = true;
				_textArea = caret._textArea;
				_parentCaret = caret;
				_timer.Tick += CaretTimerTick;
			}

			private void CaretTimerTick(object sender, EventArgs e)
			{
				_blink = !_blink;
				if (_visible)
					_textArea.UpdateLine(_parentCaret.Line);
			}

			public override bool Create(int width, int height)
			{
				_visible = true;
				_width = width - 2;
				_height = height;
				_timer.Enabled = true;
				return true;
			}

			public override void Hide()
			{
				_visible = false;
			}

			public override void Show()
			{
				_visible = true;
			}

			public override bool SetPosition(int x, int y)
			{
				_x = x - 1;
				_y = y;
				return true;
			}

			public override void PaintCaret(Graphics g)
			{
				if (_visible && _blink)
					g.DrawRectangle(Pens.Gray, _x, _y, _width, _height);
			}

			public override void Destroy()
			{
				_visible = false;
				_timer.Enabled = false;
			}

			public override void Dispose()
			{
				base.Dispose();
				_timer.Dispose();
			}
		}

		private class Win32Caret : CaretImplementation
		{
			private readonly TextArea _textArea;

			public Win32Caret(Caret caret)
			{
				_textArea = caret._textArea;
			}

			[DllImport("User32.dll")]
			private static extern bool CreateCaret(IntPtr hWnd, int hBitmap, int nWidth, int nHeight);

			[DllImport("User32.dll")]
			private static extern bool SetCaretPos(int x, int y);

			[DllImport("User32.dll")]
			private static extern bool DestroyCaret();

			[DllImport("User32.dll")]
			private static extern bool ShowCaret(IntPtr hWnd);

			[DllImport("User32.dll")]
			private static extern bool HideCaret(IntPtr hWnd);

			public override bool Create(int width, int height)
			{
				return CreateCaret(_textArea.Handle, 0, width, height);
			}

			public override void Hide()
			{
				HideCaret(_textArea.Handle);
			}

			public override void Show()
			{
				ShowCaret(_textArea.Handle);
			}

			public override bool SetPosition(int x, int y)
			{
				return SetCaretPos(x, y);
			}

			public override void PaintCaret(Graphics g)
			{
			}

			public override void Destroy()
			{
				DestroyCaret();
			}
		}

		#endregion
	}
}