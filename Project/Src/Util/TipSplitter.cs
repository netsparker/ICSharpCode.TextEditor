// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="none" email=""/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Diagnostics;
using System.Drawing;

namespace ICSharpCode.TextEditor.Util
{
	internal class TipSplitter : TipSection
	{
		private readonly bool _isHorizontal;
		private readonly float[] _offsets;
		private readonly TipSection[] _tipSections;

		public TipSplitter(Graphics graphics, bool horizontal, params TipSection[] sections) : base(graphics)
		{
			Debug.Assert(sections != null);

			_isHorizontal = horizontal;
			_offsets = new float[sections.Length];
			_tipSections = (TipSection[]) sections.Clone();
		}

		public override void Draw(PointF location)
		{
			if (_isHorizontal)
				for (var i = 0; i < _tipSections.Length; i++)
					_tipSections[i].Draw
						(new PointF(location.X + _offsets[i], location.Y));
			else
				for (var i = 0; i < _tipSections.Length; i++)
					_tipSections[i].Draw
						(new PointF(location.X, location.Y + _offsets[i]));
		}

		protected override void OnMaximumSizeChanged()
		{
			base.OnMaximumSizeChanged();

			float currentDim = 0;
			float otherDim = 0;
			var availableArea = MaximumSize;

			for (var i = 0; i < _tipSections.Length; i++)
			{
				var section = _tipSections[i];

				section.SetMaximumSize(availableArea);

				var requiredArea = section.GetRequiredSize();
				_offsets[i] = currentDim;

				// It's best to start on pixel borders, so this will
				// round up to the nearest pixel. Otherwise there are
				// weird cutoff artifacts.
				float pixelsUsed;

				if (_isHorizontal)
				{
					pixelsUsed = (float) Math.Ceiling(requiredArea.Width);
					currentDim += pixelsUsed;

					availableArea.Width = Math.Max
						(0, availableArea.Width - pixelsUsed);

					otherDim = Math.Max(otherDim, requiredArea.Height);
				}
				else
				{
					pixelsUsed = (float) Math.Ceiling(requiredArea.Height);
					currentDim += pixelsUsed;

					availableArea.Height = Math.Max
						(0, availableArea.Height - pixelsUsed);

					otherDim = Math.Max(otherDim, requiredArea.Width);
				}
			}

			foreach (var section in _tipSections)
				if (_isHorizontal)
					section.SetAllocatedSize(new SizeF(section.GetRequiredSize().Width, otherDim));
				else
					section.SetAllocatedSize(new SizeF(otherDim, section.GetRequiredSize().Height));

			if (_isHorizontal)
				SetRequiredSize(new SizeF(currentDim, otherDim));
			else
				SetRequiredSize(new SizeF(otherDim, currentDim));
		}
	}
}