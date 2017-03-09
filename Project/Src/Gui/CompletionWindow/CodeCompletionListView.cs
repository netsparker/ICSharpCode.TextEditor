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
	///     Description of CodeCompletionListView.
	/// </summary>
	public class CodeCompletionListView : UserControl
	{
		private readonly ICompletionData[] _completionData;
		private int _firstItem;
		private int _selectedItem = -1;

		public CodeCompletionListView(ICompletionData[] completionData)
		{
			Array.Sort(completionData, DefaultCompletionData.Compare);
			_completionData = completionData;

//			this.KeyDown += new System.Windows.Forms.KeyEventHandler(OnKey);
//			SetStyle(ControlStyles.Selectable, false);
//			SetStyle(ControlStyles.UserPaint, true);
//			SetStyle(ControlStyles.DoubleBuffer, false);
		}

		public ImageList ImageList { get; set; }

		public int FirstItem
		{
			get { return _firstItem; }
			set
			{
				if (_firstItem != value)
				{
					_firstItem = value;
					OnFirstItemChanged(EventArgs.Empty);
				}
			}
		}

		public ICompletionData SelectedCompletionData
		{
			get
			{
				if (_selectedItem < 0)
					return null;
				return _completionData[_selectedItem];
			}
		}

		public int ItemHeight => Math.Max(ImageList.ImageSize.Height, (int) (Font.Height * 1.25));

		public int MaxVisibleItem => Height / ItemHeight;

		public void Close()
		{
			if (_completionData != null)
				Array.Clear(_completionData, 0, _completionData.Length);
			base.Dispose();
		}

		public void SelectIndex(int index)
		{
			var oldSelectedItem = _selectedItem;
			var oldFirstItem = _firstItem;

			index = Math.Max(0, index);
			_selectedItem = Math.Max(0, Math.Min(_completionData.Length - 1, index));
			if (_selectedItem < _firstItem)
				FirstItem = _selectedItem;
			if (_firstItem + MaxVisibleItem <= _selectedItem)
				FirstItem = _selectedItem - MaxVisibleItem + 1;
			if (oldSelectedItem != _selectedItem)
			{
				if (_firstItem != oldFirstItem)
				{
					Invalidate();
				}
				else
				{
					var min = Math.Min(_selectedItem, oldSelectedItem) - _firstItem;
					var max = Math.Max(_selectedItem, oldSelectedItem) - _firstItem;
					Invalidate(new Rectangle(0, 1 + min * ItemHeight, Width, (max - min + 1) * ItemHeight));
				}
				OnSelectedItemChanged(EventArgs.Empty);
			}
		}

		public void CenterViewOn(int index)
		{
			var oldFirstItem = FirstItem;
			var firstItem = index - MaxVisibleItem / 2;
			if (firstItem < 0)
				FirstItem = 0;
			else if (firstItem >= _completionData.Length - MaxVisibleItem)
				FirstItem = _completionData.Length - MaxVisibleItem;
			else
				FirstItem = firstItem;
			if (FirstItem != oldFirstItem)
				Invalidate();
		}

		public void ClearSelection()
		{
			if (_selectedItem < 0)
				return;
			var itemNum = _selectedItem - _firstItem;
			_selectedItem = -1;
			Invalidate(new Rectangle(0, itemNum * ItemHeight, Width, (itemNum + 1) * ItemHeight + 1));
			Update();
			OnSelectedItemChanged(EventArgs.Empty);
		}

		public void PageDown()
		{
			SelectIndex(_selectedItem + MaxVisibleItem);
		}

		public void PageUp()
		{
			SelectIndex(_selectedItem - MaxVisibleItem);
		}

		public void SelectNextItem()
		{
			SelectIndex(_selectedItem + 1);
		}

		public void SelectPrevItem()
		{
			SelectIndex(_selectedItem - 1);
		}

		public void SelectItemWithStart(string startText)
		{
			if (startText == null || startText.Length == 0) return;
			var originalStartText = startText;
			startText = startText.ToLower();
			var bestIndex = -1;
			var bestQuality = -1;
			// Qualities: 0 = match start
			//            1 = match start case sensitive
			//            2 = full match
			//            3 = full match case sensitive
			double bestPriority = 0;
			for (var i = 0; i < _completionData.Length; ++i)
			{
				var itemText = _completionData[i].Text;
				var lowerText = itemText.ToLower();
				if (lowerText.StartsWith(startText))
				{
					var priority = _completionData[i].Priority;
					int quality;
					if (lowerText == startText)
						if (itemText == originalStartText)
							quality = 3;
						else
							quality = 2;
					else if (itemText.StartsWith(originalStartText))
						quality = 1;
					else
						quality = 0;
					bool useThisItem;
					if (bestQuality < quality)
					{
						useThisItem = true;
					}
					else
					{
						if (bestIndex == _selectedItem)
							useThisItem = false;
						else if (i == _selectedItem)
							useThisItem = bestQuality == quality;
						else
							useThisItem = bestQuality == quality && bestPriority < priority;
					}
					if (useThisItem)
					{
						bestIndex = i;
						bestPriority = priority;
						bestQuality = quality;
					}
				}
			}
			if (bestIndex < 0)
			{
				ClearSelection();
			}
			else
			{
				if (bestIndex < _firstItem || _firstItem + MaxVisibleItem <= bestIndex)
				{
					SelectIndex(bestIndex);
					CenterViewOn(bestIndex);
				}
				else
				{
					SelectIndex(bestIndex);
				}
			}
		}

		protected override void OnPaint(PaintEventArgs pe)
		{
			float yPos = 1;
			float itemHeight = ItemHeight;
			// Maintain aspect ratio
			var imageWidth = (int) (itemHeight * ImageList.ImageSize.Width / ImageList.ImageSize.Height);

			var curItem = _firstItem;
			var g = pe.Graphics;
			while (curItem < _completionData.Length && yPos < Height)
			{
				var drawingBackground = new RectangleF(1, yPos, Width - 2, itemHeight);
				if (drawingBackground.IntersectsWith(pe.ClipRectangle))
				{
					// draw Background
					if (curItem == _selectedItem)
						g.FillRectangle(SystemBrushes.Highlight, drawingBackground);
					else
						g.FillRectangle(SystemBrushes.Window, drawingBackground);

					// draw Icon
					var xPos = 0;
					if (ImageList != null && _completionData[curItem].ImageIndex < ImageList.Images.Count)
					{
						g.DrawImage(ImageList.Images[_completionData[curItem].ImageIndex], new RectangleF(1, yPos, imageWidth, itemHeight));
						xPos = imageWidth;
					}

					// draw text
					if (curItem == _selectedItem)
						g.DrawString(_completionData[curItem].Text, Font, SystemBrushes.HighlightText, xPos, yPos);
					else
						g.DrawString(_completionData[curItem].Text, Font, SystemBrushes.WindowText, xPos, yPos);
				}

				yPos += itemHeight;
				++curItem;
			}
			g.DrawRectangle(SystemPens.Control, new Rectangle(0, 0, Width - 1, Height - 1));
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			float yPos = 1;
			var curItem = _firstItem;
			float itemHeight = ItemHeight;

			while (curItem < _completionData.Length && yPos < Height)
			{
				var drawingBackground = new RectangleF(1, yPos, Width - 2, itemHeight);
				if (drawingBackground.Contains(e.X, e.Y))
				{
					SelectIndex(curItem);
					break;
				}
				yPos += itemHeight;
				++curItem;
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pe)
		{
		}

		protected virtual void OnSelectedItemChanged(EventArgs e)
		{
			if (SelectedItemChanged != null)
				SelectedItemChanged(this, e);
		}

		protected virtual void OnFirstItemChanged(EventArgs e)
		{
			if (FirstItemChanged != null)
				FirstItemChanged(this, e);
		}

		public event EventHandler SelectedItemChanged;
		public event EventHandler FirstItemChanged;
	}
}