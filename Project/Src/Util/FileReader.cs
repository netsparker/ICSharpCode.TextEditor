// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.IO;
using System.Text;

namespace ICSharpCode.TextEditor.Util
{
	/// <summary>
	///     Class that can open text files with auto-detection of the encoding.
	/// </summary>
	public static class FileReader
	{
		public static bool IsUnicode(Encoding encoding)
		{
			var codepage = encoding.CodePage;
			// return true if codepage is any UTF codepage
			return codepage == 65001 || codepage == 65000 || codepage == 1200 || codepage == 1201;
		}

		public static string ReadFileContent(Stream fs, ref Encoding encoding)
		{
			using (var reader = OpenStream(fs, encoding))
			{
				reader.Peek();
				encoding = reader.CurrentEncoding;
				return reader.ReadToEnd();
			}
		}

		public static string ReadFileContent(string fileName, Encoding encoding)
		{
			using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				return ReadFileContent(fs, ref encoding);
			}
		}

		public static StreamReader OpenStream(Stream fs, Encoding defaultEncoding)
		{
			if (fs == null)
				throw new ArgumentNullException("fs");

			if (fs.Length >= 2)
			{
				// the autodetection of StreamReader is not capable of detecting the difference
				// between ISO-8859-1 and UTF-8 without BOM.
				var firstByte = fs.ReadByte();
				var secondByte = fs.ReadByte();
				switch ((firstByte << 8) | secondByte)
				{
					case 0x0000: // either UTF-32 Big Endian or a binary file; use StreamReader
					case 0xfffe: // Unicode BOM (UTF-16 LE or UTF-32 LE)
					case 0xfeff: // UTF-16 BE BOM
					case 0xefbb: // start of UTF-8 BOM
						// StreamReader autodetection works
						fs.Position = 0;
						return new StreamReader(fs);
					default:
						return AutoDetect(fs, (byte) firstByte, (byte) secondByte, defaultEncoding);
				}
			}
			if (defaultEncoding != null)
				return new StreamReader(fs, defaultEncoding);
			return new StreamReader(fs);
		}

		private static StreamReader AutoDetect(Stream fs, byte firstByte, byte secondByte, Encoding defaultEncoding)
		{
			var max = (int) Math.Min(fs.Length, 500000); // look at max. 500 KB
			const int ascii = 0;
			const int error = 1;
			const int utf8 = 2;
			const int utf8Sequence = 3;
			var state = ascii;
			var sequenceLength = 0;
			byte b;
			for (var i = 0; i < max; i++)
			{
				if (i == 0)
					b = firstByte;
				else if (i == 1)
					b = secondByte;
				else
					b = (byte) fs.ReadByte();
				if (b < 0x80)
				{
					// normal ASCII character
					if (state == utf8Sequence)
					{
						state = error;
						break;
					}
				}
				else if (b < 0xc0)
				{
					// 10xxxxxx : continues UTF8 byte sequence
					if (state == utf8Sequence)
					{
						--sequenceLength;
						if (sequenceLength < 0)
						{
							state = error;
							break;
						}
						if (sequenceLength == 0)
							state = utf8;
					}
					else
					{
						state = error;
						break;
					}
				}
				else if (b >= 0xc2 && b < 0xf5)
				{
					// beginning of byte sequence
					if (state == utf8 || state == ascii)
					{
						state = utf8Sequence;
						if (b < 0xe0)
							sequenceLength = 1; // one more byte following
						else if (b < 0xf0)
							sequenceLength = 2; // two more bytes following
						else
							sequenceLength = 3; // three more bytes following
					}
					else
					{
						state = error;
						break;
					}
				}
				else
				{
					// 0xc0, 0xc1, 0xf5 to 0xff are invalid in UTF-8 (see RFC 3629)
					state = error;
					break;
				}
			}
			fs.Position = 0;
			switch (state)
			{
				case ascii:
				case error:
					// when the file seems to be ASCII or non-UTF8,
					// we read it using the user-specified encoding so it is saved again
					// using that encoding.
					if (IsUnicode(defaultEncoding))
						defaultEncoding = Encoding.Default; // use system encoding instead
					return new StreamReader(fs, defaultEncoding);
				default:
					return new StreamReader(fs);
			}
		}
	}
}