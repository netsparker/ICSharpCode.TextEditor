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
	internal class TipSpacer : TipSection
	{
		private SizeF _spacerSize;

		public TipSpacer(Graphics graphics, SizeF size) : base(graphics)
		{
			_spacerSize = size;
		}

		public override void Draw(PointF location)
		{
		}

		protected override void OnMaximumSizeChanged()
		{
			base.OnMaximumSizeChanged();

			SetRequiredSize(new SizeF
			(Math.Min(MaximumSize.Width, _spacerSize.Width),
				Math.Min(MaximumSize.Height, _spacerSize.Height)));
		}
	}
}