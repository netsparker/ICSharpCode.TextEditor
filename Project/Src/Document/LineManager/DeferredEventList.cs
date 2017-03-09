// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System.Collections.Generic;

namespace ICSharpCode.TextEditor.Document
{
	/// <summary>
	///     A list of events that are fired after the line manager has finished working.
	/// </summary>
	internal struct DeferredEventList
	{
		internal List<LineSegment> RemovedLines;
		internal List<TextAnchor> TextAnchor;

		public void AddRemovedLine(LineSegment line)
		{
			if (RemovedLines == null)
				RemovedLines = new List<LineSegment>();
			RemovedLines.Add(line);
		}

		public void AddDeletedAnchor(TextAnchor anchor)
		{
			if (TextAnchor == null)
				TextAnchor = new List<TextAnchor>();
			TextAnchor.Add(anchor);
		}

		public void RaiseEvents()
		{
			// removedLines is raised by the LineManager
			if (TextAnchor != null)
				foreach (var a in TextAnchor)
					a.RaiseDeleted();
		}
	}
}