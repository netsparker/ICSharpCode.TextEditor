// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Windows.Forms;

namespace ICSharpCode.TextEditor.Util
{
	/// <summary>
	///     Accumulates mouse wheel deltas and reports the actual number of lines to scroll.
	/// </summary>
	internal class MouseWheelHandler
	{
		// CODE DUPLICATION: See ICSharpCode.SharpDevelop.Widgets.MouseWheelHandler

		private const int WheelDelta = 120;

		private int _mouseWheelDelta;

		public int GetScrollAmount(MouseEventArgs e)
		{
			// accumulate the delta to support high-resolution mice
			_mouseWheelDelta += e.Delta;

			var linesPerClick = Math.Max(SystemInformation.MouseWheelScrollLines, 1);

			var scrollDistance = _mouseWheelDelta * linesPerClick / WheelDelta;
			_mouseWheelDelta %= Math.Max(1, WheelDelta / linesPerClick);
			return scrollDistance;
		}
	}
}