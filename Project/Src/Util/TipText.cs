// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="none" email=""/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Drawing;

namespace ICSharpCode.TextEditor.Util
{
	internal class CountTipText : TipText
	{
		private readonly float _triHeight = 10;
		private readonly float _triWidth = 10;

		public Rectangle DrawingRectangle1;
		public Rectangle DrawingRectangle2;

		public CountTipText(Graphics graphics, Font font, string text) : base(graphics, font, text)
		{
		}

		private void DrawTriangle(float x, float y, bool flipped)
		{
			var brush = BrushRegistry.GetBrush(Color.FromArgb(192, 192, 192));
			Graphics.FillRectangle(brush, new RectangleF(x, y, _triHeight, _triHeight));
			var triHeight2 = _triHeight / 2;
			var triHeight4 = _triHeight / 4;
			brush = Brushes.Black;
			if (flipped)
				Graphics.FillPolygon(brush, new[]
				{
					new PointF(x, y + triHeight2 - triHeight4),
					new PointF(x + _triWidth / 2, y + triHeight2 + triHeight4),
					new PointF(x + _triWidth, y + triHeight2 - triHeight4)
				});
			else
				Graphics.FillPolygon(brush, new[]
				{
					new PointF(x, y + triHeight2 + triHeight4),
					new PointF(x + _triWidth / 2, y + triHeight2 - triHeight4),
					new PointF(x + _triWidth, y + triHeight2 + triHeight4)
				});
		}

		public override void Draw(PointF location)
		{
			if (tipText != null && tipText.Length > 0)
			{
				base.Draw(new PointF(location.X + _triWidth + 4, location.Y));
				DrawingRectangle1 = new Rectangle((int) location.X + 2,
					(int) location.Y + 2,
					(int) _triWidth,
					(int) _triHeight);
				DrawingRectangle2 = new Rectangle((int) (location.X + AllocatedSize.Width - _triWidth - 2),
					(int) location.Y + 2,
					(int) _triWidth,
					(int) _triHeight);
				DrawTriangle(location.X + 2, location.Y + 2, false);
				DrawTriangle(location.X + AllocatedSize.Width - _triWidth - 2, location.Y + 2, true);
			}
		}

		protected override void OnMaximumSizeChanged()
		{
			if (IsTextVisible())
			{
				var tipSize = Graphics.MeasureString
				(tipText, TipFont, MaximumSize,
					GetInternalStringFormat());
				tipSize.Width += _triWidth * 2 + 8;
				SetRequiredSize(tipSize);
			}
			else
			{
				SetRequiredSize(SizeF.Empty);
			}
		}
	}

	internal class TipText : TipSection
	{
		protected StringAlignment HorzAlign;
		protected Color TipColor;
		protected Font TipFont;
		protected StringFormat TipFormat;
		protected string tipText;
		protected StringAlignment VertAlign;

		public TipText(Graphics graphics, Font font, string text) :
			base(graphics)
		{
			TipFont = font;
			tipText = text;
			if (text != null && text.Length > short.MaxValue)
				throw new ArgumentException("TipText: text too long (max. is " + short.MaxValue + " characters)", "text");

			Color = SystemColors.InfoText;
			HorizontalAlignment = StringAlignment.Near;
			VerticalAlignment = StringAlignment.Near;
		}

		public Color Color
		{
			get { return TipColor; }
			set { TipColor = value; }
		}

		public StringAlignment HorizontalAlignment
		{
			get { return HorzAlign; }
			set
			{
				HorzAlign = value;
				TipFormat = null;
			}
		}

		public StringAlignment VerticalAlignment
		{
			get { return VertAlign; }
			set
			{
				VertAlign = value;
				TipFormat = null;
			}
		}

		public override void Draw(PointF location)
		{
			if (IsTextVisible())
			{
				var drawRectangle = new RectangleF(location, AllocatedSize);

				Graphics.DrawString(tipText, TipFont,
					BrushRegistry.GetBrush(Color),
					drawRectangle,
					GetInternalStringFormat());
			}
		}

		protected StringFormat GetInternalStringFormat()
		{
			if (TipFormat == null)
				TipFormat = CreateTipStringFormat(HorzAlign, VertAlign);

			return TipFormat;
		}

		protected override void OnMaximumSizeChanged()
		{
			base.OnMaximumSizeChanged();

			if (IsTextVisible())
			{
				var tipSize = Graphics.MeasureString
				(tipText, TipFont, MaximumSize,
					GetInternalStringFormat());

				SetRequiredSize(tipSize);
			}
			else
			{
				SetRequiredSize(SizeF.Empty);
			}
		}

		private static StringFormat CreateTipStringFormat(StringAlignment horizontalAlignment,
			StringAlignment verticalAlignment)
		{
			var format = (StringFormat) StringFormat.GenericTypographic.Clone();
			format.FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces;
			// note: Align Near, Line Center seemed to do something before

			format.Alignment = horizontalAlignment;
			format.LineAlignment = verticalAlignment;

			return format;
		}

		protected bool IsTextVisible()
		{
			return tipText != null && tipText.Length > 0;
		}
	}
}