// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICSharpCode.TextEditor.Gui.CompletionWindow
{
	/// <summary>
	///     Description of AbstractCompletionWindow.
	/// </summary>
	public abstract class AbstractCompletionWindow : Form
	{
		private static int _shadowStatus;
		private readonly Form _parentForm;
		private Rectangle _workingScreen;
		protected TextEditorControl Control;
		protected Size DrawingSize;

		protected AbstractCompletionWindow(Form parentForm, TextEditorControl control)
		{
			_workingScreen = Screen.GetWorkingArea(parentForm);
//			SetStyle(ControlStyles.Selectable, false);
			_parentForm = parentForm;
			Control = control;

			SetLocation();
			StartPosition = FormStartPosition.Manual;
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			MinimumSize = new Size(1, 1);
			Size = new Size(1, 1);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				var p = base.CreateParams;
				AddShadowToWindow(p);
				return p;
			}
		}

		protected override bool ShowWithoutActivation => true;

		protected virtual void SetLocation()
		{
			var textArea = Control.ActiveTextAreaControl.TextArea;
			var caretPos = textArea.Caret.Position;

			var xpos = textArea.TextView.GetDrawingXPos(caretPos.Y, caretPos.X);
			var rulerHeight = textArea.TextEditorProperties.ShowHorizontalRuler ? textArea.TextView.FontHeight : 0;
			var pos = new Point(textArea.TextView.DrawingPosition.X + xpos,
				textArea.TextView.DrawingPosition.Y + textArea.Document.GetVisibleLine(caretPos.Y) * textArea.TextView.FontHeight
				- textArea.TextView.TextArea.VirtualTop.Y + textArea.TextView.FontHeight + rulerHeight);

			var location = Control.ActiveTextAreaControl.PointToScreen(pos);

			// set bounds
			var bounds = new Rectangle(location, DrawingSize);

			if (!_workingScreen.Contains(bounds))
			{
				if (bounds.Right > _workingScreen.Right)
					bounds.X = _workingScreen.Right - bounds.Width;
				if (bounds.Left < _workingScreen.Left)
					bounds.X = _workingScreen.Left;
				if (bounds.Top < _workingScreen.Top)
					bounds.Y = _workingScreen.Top;
				if (bounds.Bottom > _workingScreen.Bottom)
				{
					bounds.Y = bounds.Y - bounds.Height - Control.ActiveTextAreaControl.TextArea.TextView.FontHeight;
					if (bounds.Bottom > _workingScreen.Bottom)
						bounds.Y = _workingScreen.Bottom - bounds.Height;
				}
			}
			Bounds = bounds;
		}

		/// <summary>
		///     Adds a shadow to the create params if it is supported by the operating system.
		/// </summary>
		public static void AddShadowToWindow(CreateParams createParams)
		{
			if (_shadowStatus == 0)
			{
				// Test OS version
				_shadowStatus = -1; // shadow not supported
				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				{
					var ver = Environment.OSVersion.Version;
					if (ver.Major > 5 || ver.Major == 5 && ver.Minor >= 1)
						_shadowStatus = 1;
				}
			}
			if (_shadowStatus == 1)
				createParams.ClassStyle |= 0x00020000; // set CS_DROPSHADOW
		}

		protected void ShowCompletionWindow()
		{
			Owner = _parentForm;
			Enabled = true;
			Show();

			Control.Focus();

			if (_parentForm != null)
				_parentForm.LocationChanged += ParentFormLocationChanged;

			Control.ActiveTextAreaControl.VScrollBar.ValueChanged += ParentFormLocationChanged;
			Control.ActiveTextAreaControl.HScrollBar.ValueChanged += ParentFormLocationChanged;
			Control.ActiveTextAreaControl.TextArea.DoProcessDialogKey += ProcessTextAreaKey;
			Control.ActiveTextAreaControl.Caret.PositionChanged += CaretOffsetChanged;
			Control.ActiveTextAreaControl.TextArea.LostFocus += TextEditorLostFocus;
			Control.Resize += ParentFormLocationChanged;

			foreach (Control c in Controls)
				c.MouseMove += ControlMouseMove;
		}

		private void ParentFormLocationChanged(object sender, EventArgs e)
		{
			SetLocation();
		}

		public virtual bool ProcessKeyEvent(char ch)
		{
			return false;
		}

		protected virtual bool ProcessTextAreaKey(Keys keyData)
		{
			if (!Visible)
				return false;
			switch (keyData)
			{
				case Keys.Escape:
					Close();
					return true;
			}
			return false;
		}

		protected virtual void CaretOffsetChanged(object sender, EventArgs e)
		{
		}

		protected void TextEditorLostFocus(object sender, EventArgs e)
		{
			if (!Control.ActiveTextAreaControl.TextArea.Focused && !ContainsFocus)
				Close();
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			// take out the inserted methods
			_parentForm.LocationChanged -= ParentFormLocationChanged;

			foreach (Control c in Controls)
				c.MouseMove -= ControlMouseMove;

			if (Control.ActiveTextAreaControl.VScrollBar != null)
				Control.ActiveTextAreaControl.VScrollBar.ValueChanged -= ParentFormLocationChanged;
			if (Control.ActiveTextAreaControl.HScrollBar != null)
				Control.ActiveTextAreaControl.HScrollBar.ValueChanged -= ParentFormLocationChanged;

			Control.ActiveTextAreaControl.TextArea.LostFocus -= TextEditorLostFocus;
			Control.ActiveTextAreaControl.Caret.PositionChanged -= CaretOffsetChanged;
			Control.ActiveTextAreaControl.TextArea.DoProcessDialogKey -= ProcessTextAreaKey;
			Control.Resize -= ParentFormLocationChanged;
			Dispose();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			ControlMouseMove(this, e);
		}

		/// <summary>
		///     Invoked when the mouse moves over this form or any child control.
		///     Shows the mouse cursor on the text area if it has been hidden.
		/// </summary>
		/// <remarks>
		///     Derived classes should attach this handler to the MouseMove event
		///     of all created controls which are not added to the Controls
		///     collection.
		/// </remarks>
		protected void ControlMouseMove(object sender, MouseEventArgs e)
		{
			Control.ActiveTextAreaControl.TextArea.ShowHiddenCursor(false);
		}
	}
}