// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System.Collections.Generic;
using System.Drawing;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     Contains brushes/pens for the text editor to speed up drawing. Re-Creation of brushes and pens
	///     seems too costly.
	/// </summary>
	public class BrushRegistry
	{
		private static readonly Dictionary<Color, Brush> _brushes = new Dictionary<Color, Brush>();
		private static readonly Dictionary<Color, Pen> _pens = new Dictionary<Color, Pen>();
		private static readonly Dictionary<Color, Pen> _dotPens = new Dictionary<Color, Pen>();

		private static readonly float[] DotPattern = {1, 1, 1, 1};

		public static Brush GetBrush(Color color)
		{
			lock (_brushes)
			{
				Brush brush;
				if (!_brushes.TryGetValue(color, out brush))
				{
					brush = new SolidBrush(color);
					_brushes.Add(color, brush);
				}
				return brush;
			}
		}

		public static Pen GetPen(Color color)
		{
			lock (_pens)
			{
				Pen pen;
				if (!_pens.TryGetValue(color, out pen))
				{
					pen = new Pen(color);
					_pens.Add(color, pen);
				}
				return pen;
			}
		}

		public static Pen GetDotPen(Color color)
		{
			lock (_dotPens)
			{
				Pen pen;
				if (!_dotPens.TryGetValue(color, out pen))
				{
					pen = new Pen(color);
					pen.DashPattern = DotPattern;
					_dotPens.Add(color, pen);
				}
				return pen;
			}
		}
	}
}