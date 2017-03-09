// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Gui.CompletionWindow;
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor.Gui.InsightWindow
{
	public class InsightWindow : AbstractCompletionWindow
	{
		private readonly MouseWheelHandler _mouseWheelHandler = new MouseWheelHandler();

		public InsightWindow(Form parentForm, TextEditorControl control) : base(parentForm, control)
		{
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
		}

		public void ShowInsightWindow()
		{
			if (!Visible)
			{
				if (_insightDataProviderStack.Count > 0)
					ShowCompletionWindow();
			}
			else
			{
				Refresh();
			}
		}

		public void HandleMouseWheel(MouseEventArgs e)
		{
			if (DataProvider != null && DataProvider.InsightDataCount > 0)
			{
				var distance = _mouseWheelHandler.GetScrollAmount(e);
				if (Control.TextEditorProperties.MouseWheelScrollDown)
					distance = -distance;
				if (distance > 0)
					CurrentData = (CurrentData + 1) % DataProvider.InsightDataCount;
				else if (distance < 0)
					CurrentData = (CurrentData + DataProvider.InsightDataCount - 1) % DataProvider.InsightDataCount;
				Refresh();
			}
		}

		#region Event handling routines

		protected override bool ProcessTextAreaKey(Keys keyData)
		{
			if (!Visible)
				return false;
			switch (keyData)
			{
				case Keys.Down:
					if (DataProvider != null && DataProvider.InsightDataCount > 0)
					{
						CurrentData = (CurrentData + 1) % DataProvider.InsightDataCount;
						Refresh();
					}
					return true;
				case Keys.Up:
					if (DataProvider != null && DataProvider.InsightDataCount > 0)
					{
						CurrentData = (CurrentData + DataProvider.InsightDataCount - 1) % DataProvider.InsightDataCount;
						Refresh();
					}
					return true;
			}
			return base.ProcessTextAreaKey(keyData);
		}

		protected override void CaretOffsetChanged(object sender, EventArgs e)
		{
			// move the window under the caret (don't change the x position)
			var caretPos = Control.ActiveTextAreaControl.Caret.Position;
			var y = (1 + caretPos.Y) * Control.ActiveTextAreaControl.TextArea.TextView.FontHeight
			        - Control.ActiveTextAreaControl.TextArea.VirtualTop.Y - 1
			        + Control.ActiveTextAreaControl.TextArea.TextView.DrawingPosition.Y;

			var xpos = Control.ActiveTextAreaControl.TextArea.TextView.GetDrawingXPos(caretPos.Y, caretPos.X);
			var ypos = (Control.ActiveTextAreaControl.Document.GetVisibleLine(caretPos.Y) + 1) *
			           Control.ActiveTextAreaControl.TextArea.TextView.FontHeight
			           - Control.ActiveTextAreaControl.TextArea.VirtualTop.Y;
			var rulerHeight = Control.TextEditorProperties.ShowHorizontalRuler
				? Control.ActiveTextAreaControl.TextArea.TextView.FontHeight
				: 0;

			var p = Control.ActiveTextAreaControl.PointToScreen(new Point(xpos, ypos + rulerHeight));
			if (p.Y != Location.Y)
				Location = p;

			while (DataProvider != null && DataProvider.CaretOffsetChanged())
				CloseCurrentDataProvider();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			Control.ActiveTextAreaControl.TextArea.Focus();
			if (TipPainterTools.DrawingRectangle1.Contains(e.X, e.Y))
			{
				CurrentData = (CurrentData + DataProvider.InsightDataCount - 1) % DataProvider.InsightDataCount;
				Refresh();
			}
			if (TipPainterTools.DrawingRectangle2.Contains(e.X, e.Y))
			{
				CurrentData = (CurrentData + 1) % DataProvider.InsightDataCount;
				Refresh();
			}
		}

		#endregion

		#region Insight Window Drawing routines

		protected override void OnPaint(PaintEventArgs pe)
		{
			string methodCountMessage = null, description;
			if (DataProvider == null || DataProvider.InsightDataCount < 1)
			{
				description = "Unknown Method";
			}
			else
			{
				if (DataProvider.InsightDataCount > 1)
					methodCountMessage = Control.GetRangeDescription(CurrentData + 1, DataProvider.InsightDataCount);
				description = DataProvider.GetInsightData(CurrentData);
			}

			DrawingSize = TipPainterTools.GetDrawingSizeHelpTipFromCombinedDescription(this,
				pe.Graphics,
				Font,
				methodCountMessage,
				description);
			if (DrawingSize != Size)
				SetLocation();
			else
				TipPainterTools.DrawHelpTipFromCombinedDescription(this, pe.Graphics, Font, methodCountMessage, description);
		}

		protected override void OnPaintBackground(PaintEventArgs pe)
		{
			pe.Graphics.FillRectangle(SystemBrushes.Info, pe.ClipRectangle);
		}

		#endregion

		#region InsightDataProvider handling

		private readonly Stack<InsightDataProviderStackElement> _insightDataProviderStack =
			new Stack<InsightDataProviderStackElement>();

		private int CurrentData
		{
			get { return _insightDataProviderStack.Peek().currentData; }
			set { _insightDataProviderStack.Peek().currentData = value; }
		}

		private IInsightDataProvider DataProvider
		{
			get
			{
				if (_insightDataProviderStack.Count == 0)
					return null;
				return _insightDataProviderStack.Peek().dataProvider;
			}
		}

		public void AddInsightDataProvider(IInsightDataProvider provider, string fileName)
		{
			provider.SetupDataProvider(fileName, Control.ActiveTextAreaControl.TextArea);
			if (provider.InsightDataCount > 0)
				_insightDataProviderStack.Push(new InsightDataProviderStackElement(provider));
		}

		private void CloseCurrentDataProvider()
		{
			_insightDataProviderStack.Pop();
			if (_insightDataProviderStack.Count == 0)
				Close();
			else
				Refresh();
		}

		private class InsightDataProviderStackElement
		{
			public int currentData;
			public readonly IInsightDataProvider dataProvider;

			public InsightDataProviderStackElement(IInsightDataProvider dataProvider)
			{
				currentData = Math.Max(dataProvider.DefaultIndex, 0);
				this.dataProvider = dataProvider;
			}
		}

		#endregion
	}
}