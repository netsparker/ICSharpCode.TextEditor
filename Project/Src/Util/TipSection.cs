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
	internal abstract class TipSection
	{
		private SizeF _tipRequiredSize;

		protected TipSection(Graphics graphics)
		{
			Graphics = graphics;
		}

		protected Graphics Graphics { get; }

		protected SizeF AllocatedSize { get; private set; }

		protected SizeF MaximumSize { get; private set; }

		public abstract void Draw(PointF location);

		public SizeF GetRequiredSize()
		{
			return _tipRequiredSize;
		}

		public void SetAllocatedSize(SizeF allocatedSize)
		{
			Debug.Assert(allocatedSize.Width >= _tipRequiredSize.Width &&
			             allocatedSize.Height >= _tipRequiredSize.Height);

			AllocatedSize = allocatedSize;
			OnAllocatedSizeChanged();
		}

		public void SetMaximumSize(SizeF maximumSize)
		{
			MaximumSize = maximumSize;
			OnMaximumSizeChanged();
		}

		protected virtual void OnAllocatedSizeChanged()
		{
		}

		protected virtual void OnMaximumSizeChanged()
		{
		}

		protected void SetRequiredSize(SizeF requiredSize)
		{
			requiredSize.Width = Math.Max(0, requiredSize.Width);
			requiredSize.Height = Math.Max(0, requiredSize.Height);
			requiredSize.Width = Math.Min(MaximumSize.Width, requiredSize.Width);
			requiredSize.Height = Math.Min(MaximumSize.Height, requiredSize.Height);

			_tipRequiredSize = requiredSize;
		}
	}
}