// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="none" email=""/>
//     <version>$Revision$</version>
// </file>

using System.Drawing;
using System.Drawing.Text;
using System.Text;

namespace ICSharpCode.TextEditor.Document
{
	public enum BracketMatchingStyle
	{
		Before,
		After
	}

	public class DefaultTextEditorProperties : ITextEditorProperties
	{
		private static Font _defaultFont;

		public DefaultTextEditorProperties()
		{
			if (_defaultFont == null)
				_defaultFont = new Font("Courier New", 10);
			FontContainer = new FontContainer(_defaultFont);
		}

		public int TabIndent { get; set; } = 4;

		public int IndentationSize { get; set; } = 4;

		public IndentStyle IndentStyle { get; set; } = IndentStyle.Smart;

		public bool CaretLine { get; set; }

		public DocumentSelectionMode DocumentSelectionMode { get; set; } = DocumentSelectionMode.Normal;

		public bool AllowCaretBeyondEol { get; set; }

		public bool ShowMatchingBracket { get; set; } = true;

		public bool ShowLineNumbers { get; set; } = true;

		public bool ShowSpaces { get; set; }

		public bool ShowTabs { get; set; }

		public bool ShowEolMarker { get; set; }

		public bool ShowInvalidLines { get; set; }

		public bool IsIconBarVisible { get; set; }

		public bool EnableFolding { get; set; } = true;

		public bool ShowHorizontalRuler { get; set; }

		public bool ShowVerticalRuler { get; set; } = true;

		public bool ConvertTabsToSpaces { get; set; }

		public TextRenderingHint TextRenderingHint { get; set; } =
			TextRenderingHint.SystemDefault;

		public bool MouseWheelScrollDown { get; set; } = true;

		public bool MouseWheelTextZoom { get; set; } = true;

		public bool HideMouseCursor { get; set; }

		public bool CutCopyWholeLine { get; set; } = true;

		public Encoding Encoding { get; set; } = Encoding.UTF8;

		public int VerticalRulerRow { get; set; } = 80;

		public LineViewerStyle LineViewerStyle { get; set; } = LineViewerStyle.None;

		public string LineTerminator { get; set; } = "\r\n";

		public bool AutoInsertCurlyBracket { get; set; } = true;

		public Font Font
		{
			get { return FontContainer.DefaultFont; }
			set { FontContainer.DefaultFont = value; }
		}

		public FontContainer FontContainer { get; }

		public BracketMatchingStyle BracketMatchingStyle { get; set; } = BracketMatchingStyle.After;

		public bool SupportReadOnlySegments { get; set; }
	}
}