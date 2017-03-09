// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Shinsaku Nakagawa" email="shinsaku@users.sourceforge.jp"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ICSharpCode.TextEditor
{
	/// <summary>
	///     Used internally, not for own use.
	/// </summary>
	internal class Ime
	{
		private const int WmImeControl = 0x0283;

		private const int ImcSetcompositionwindow = 0x000c;
		private const int CfsPoint = 0x0002;

		private const int ImcSetcompositionfont = 0x000a;
		private static bool _disableIme;

		private Font _font;
		private IntPtr _hImeWnd;
		private IntPtr _hWnd;
		private Logfont _lf;

		public Ime(IntPtr hWnd, Font font)
		{
			// For unknown reasons, the IME support is causing crashes when used in a WOW64 process
			// or when used in .NET 4.0. We'll disable IME support in those cases.
			var processorArchitew6432 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
			if (processorArchitew6432 == "IA64" || processorArchitew6432 == "AMD64" ||
			    Environment.OSVersion.Platform == PlatformID.Unix || Environment.Version >= new Version(4, 0))
				_disableIme = true;
			else
				_hImeWnd = ImmGetDefaultIMEWnd(hWnd);
			_hWnd = hWnd;
			_font = font;
			SetImeWindowFont(font);
		}

		public Font Font
		{
			get { return _font; }
			set
			{
				if (!value.Equals(_font))
				{
					_font = value;
					_lf = null;
					SetImeWindowFont(value);
				}
			}
		}

		public IntPtr HWnd
		{
			set
			{
				if (_hWnd != value)
				{
					_hWnd = value;
					if (!_disableIme)
						_hImeWnd = ImmGetDefaultIMEWnd(value);
					SetImeWindowFont(_font);
				}
			}
		}

		[DllImport("imm32.dll")]
		private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, Compositionform lParam);

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam,
			[In] [MarshalAs(UnmanagedType.LPStruct)] Logfont lParam);

		private void SetImeWindowFont(Font f)
		{
			if (_disableIme || _hImeWnd == IntPtr.Zero) return;

			if (_lf == null)
			{
				_lf = new Logfont();
				f.ToLogFont(_lf);
				_lf.lfFaceName = f.Name; // This is very important! "Font.ToLogFont" Method sets invalid value to LOGFONT.lfFaceName
			}

			try
			{
				SendMessage(
					_hImeWnd,
					WmImeControl,
					new IntPtr(ImcSetcompositionfont),
					_lf
				);
			}
			catch (AccessViolationException ex)
			{
				Handle(ex);
			}
		}

		public void SetImeWindowLocation(int x, int y)
		{
			if (_disableIme || _hImeWnd == IntPtr.Zero) return;

			var p = new POINT();
			p.x = x;
			p.y = y;

			var lParam = new Compositionform();
			lParam.dwStyle = CfsPoint;
			lParam.ptCurrentPos = p;
			lParam.rcArea = new Rect();

			try
			{
				SendMessage(
					_hImeWnd,
					WmImeControl,
					new IntPtr(ImcSetcompositionwindow),
					lParam
				);
			}
			catch (AccessViolationException ex)
			{
				Handle(ex);
			}
		}

		private void Handle(Exception ex)
		{
			Console.WriteLine(ex);
			if (!_disableIme)
			{
				_disableIme = true;
				MessageBox.Show("Error calling IME: " + ex.Message + "\nIME is disabled.", "IME error");
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private class Compositionform
		{
			public int dwStyle;
			public POINT ptCurrentPos;
			public Rect rcArea;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class POINT
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class Rect
		{
			public int bottom = 0;
			public int left = 0;
			public int right = 0;
			public int top = 0;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class Logfont
		{
			public byte lfCharSet = 0;
			public byte lfClipPrecision = 0;
			public int lfEscapement = 0;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
			public int lfHeight = 0;
			public byte lfItalic = 0;
			public int lfOrientation = 0;
			public byte lfOutPrecision = 0;
			public byte lfPitchAndFamily = 0;
			public byte lfQuality = 0;
			public byte lfStrikeOut = 0;
			public byte lfUnderline = 0;
			public int lfWeight = 0;
			public int lfWidth = 0;
		}
	}
}