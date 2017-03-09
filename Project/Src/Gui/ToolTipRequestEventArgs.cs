// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System.Drawing;

namespace ICSharpCode.TextEditor
{
	public delegate void ToolTipRequestEventHandler(object sender, ToolTipRequestEventArgs e);

	public class ToolTipRequestEventArgs
	{
		internal string ToolTipText;

		public ToolTipRequestEventArgs(Point mousePosition, TextLocation logicalPosition, bool inDocument)
		{
			MousePosition = mousePosition;
			LogicalPosition = logicalPosition;
			InDocument = inDocument;
		}

		public Point MousePosition { get; }

		public TextLocation LogicalPosition { get; }

		public bool InDocument { get; }

		/// <summary>
		///     Gets if some client handling the event has already shown a tool tip.
		/// </summary>
		public bool ToolTipShown => ToolTipText != null;

		public void ShowToolTip(string text)
		{
			ToolTipText = text;
		}
	}
}