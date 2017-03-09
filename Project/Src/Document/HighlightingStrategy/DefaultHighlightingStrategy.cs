// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ICSharpCode.TextEditor.Document
{
	public class DefaultHighlightingStrategy : IHighlightingStrategyUsingRuleSets
	{
		private HighlightRuleSet _defaultRuleSet;
		private Dictionary<string, HighlightColor> _environmentColors = new Dictionary<string, HighlightColor>();
		protected HighlightRuleSet ActiveRuleSet;
		protected Span ActiveSpan;
		protected int CurrentLength;

		// Line state variable
		protected LineSegment CurrentLine;
		protected int CurrentLineNumber;

		// Line scanning state variables
		protected int CurrentOffset;

		// Span stack state variable
		protected SpanStack CurrentSpanStack;

		// Span state variables
		protected bool InSpan;

		public DefaultHighlightingStrategy() : this("Default")
		{
		}

		public DefaultHighlightingStrategy(string name)
		{
			Name = name;

			DigitColor = new HighlightColor(SystemColors.WindowText, false, false);
			DefaultTextColor = new HighlightColor(SystemColors.WindowText, false, false);

			// set small 'default color environment'
			_environmentColors["Default"] = new HighlightBackground("WindowText", "Window", false, false);
			_environmentColors["Selection"] = new HighlightColor("HighlightText", "Highlight", false, false);
			_environmentColors["VRuler"] = new HighlightColor("ControlLight", "Window", false, false);
			_environmentColors["InvalidLines"] = new HighlightColor(Color.Red, false, false);
			_environmentColors["CaretMarker"] = new HighlightColor(Color.Yellow, false, false);
			_environmentColors["CaretLine"] = new HighlightBackground("ControlLight", "Window", false, false);
			_environmentColors["LineNumbers"] = new HighlightBackground("ControlDark", "Window", false, false);

			_environmentColors["FoldLine"] = new HighlightColor("ControlDark", false, false);
			_environmentColors["FoldMarker"] = new HighlightColor("WindowText", "Window", false, false);
			_environmentColors["SelectedFoldLine"] = new HighlightColor("WindowText", false, false);
			_environmentColors["EOLMarkers"] = new HighlightColor("ControlLight", "Window", false, false);
			_environmentColors["SpaceMarkers"] = new HighlightColor("ControlLight", "Window", false, false);
			_environmentColors["TabMarkers"] = new HighlightColor("ControlLight", "Window", false, false);
		}

		public HighlightColor DigitColor { get; set; }

		public IEnumerable<KeyValuePair<string, HighlightColor>> EnvironmentColors => _environmentColors;

		public List<HighlightRuleSet> Rules { get; private set; } = new List<HighlightRuleSet>();

//		internal void SetDefaultColor(HighlightBackground color)
//		{
//			return (HighlightColor)environmentColors[name];
//			defaultColor = color;
//		}

		public HighlightColor DefaultTextColor { get; private set; }

		public Dictionary<string, string> Properties { get; private set; } = new Dictionary<string, string>();

		public string Name { get; private set; }

		public string[] Extensions { set; get; }

		public HighlightColor GetColorFor(string name)
		{
			HighlightColor color;
			if (_environmentColors.TryGetValue(name, out color))
				return color;
			return DefaultTextColor;
		}

		public HighlightColor GetColor(IDocument document, LineSegment currentSegment, int currentOffset, int currentLength)
		{
			return GetColor(_defaultRuleSet, document, currentSegment, currentOffset, currentLength);
		}

		public HighlightRuleSet GetRuleSet(Span aSpan)
		{
			if (aSpan == null)
				return _defaultRuleSet;
			if (aSpan.RuleSet != null)
			{
				if (aSpan.RuleSet.Reference != null)
					return aSpan.RuleSet.Highlighter.GetRuleSet(null);
				return aSpan.RuleSet;
			}
			return null;
		}

		public virtual void MarkTokens(IDocument document)
		{
			if (Rules.Count == 0)
				return;

			var lineNumber = 0;

			while (lineNumber < document.TotalNumberOfLines)
			{
				var previousLine = lineNumber > 0 ? document.GetLineSegment(lineNumber - 1) : null;
				if (lineNumber >= document.LineSegmentCollection.Count)
					break; // then the last line is not in the collection :)

				CurrentSpanStack = previousLine != null && previousLine.HighlightSpanStack != null
					? previousLine.HighlightSpanStack.Clone()
					: null;

				if (CurrentSpanStack != null)
				{
					while (!CurrentSpanStack.IsEmpty && CurrentSpanStack.Peek().StopEol)
						CurrentSpanStack.Pop();
					if (CurrentSpanStack.IsEmpty) CurrentSpanStack = null;
				}

				CurrentLine = document.LineSegmentCollection[lineNumber];

				if (CurrentLine.Length == -1)
					return;

				CurrentLineNumber = lineNumber;
				var words = ParseLine(document);
				// Alex: clear old words
				if (CurrentLine.Words != null)
					CurrentLine.Words.Clear();
				CurrentLine.Words = words;
				CurrentLine.HighlightSpanStack = CurrentSpanStack == null || CurrentSpanStack.IsEmpty ? null : CurrentSpanStack;

				++lineNumber;
			}
			document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.WholeTextArea));
			document.CommitUpdate();
			CurrentLine = null;
		}

		public virtual void MarkTokens(IDocument document, List<LineSegment> inputLines)
		{
			if (Rules.Count == 0)
				return;

			var processedLines = new Dictionary<LineSegment, bool>();

			var spanChanged = false;
			var documentLineSegmentCount = document.LineSegmentCollection.Count;

			foreach (var lineToProcess in inputLines)
				if (!processedLines.ContainsKey(lineToProcess))
				{
					var lineNumber = lineToProcess.LineNumber;
					var processNextLine = true;

					if (lineNumber != -1)
						while (processNextLine && lineNumber < documentLineSegmentCount)
						{
							processNextLine = MarkTokensInLine(document, lineNumber, ref spanChanged);
							processedLines[CurrentLine] = true;
							++lineNumber;
						}
				}

			if (spanChanged || inputLines.Count > 20)
				document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.WholeTextArea));
			else
				foreach (var lineToProcess in inputLines)
					document.RequestUpdate(new TextAreaUpdate(TextAreaUpdateType.SingleLine, lineToProcess.LineNumber));
			document.CommitUpdate();
			CurrentLine = null;
		}

		protected void ImportSettingsFrom(DefaultHighlightingStrategy source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			Properties = source.Properties;
			Extensions = source.Extensions;
			DigitColor = source.DigitColor;
			_defaultRuleSet = source._defaultRuleSet;
			Name = source.Name;
			Rules = source.Rules;
			_environmentColors = source._environmentColors;
			DefaultTextColor = source.DefaultTextColor;
		}

		public HighlightRuleSet FindHighlightRuleSet(string name)
		{
			foreach (var ruleSet in Rules)
				if (ruleSet.Name == name)
					return ruleSet;
			return null;
		}

		public void AddRuleSet(HighlightRuleSet aRuleSet)
		{
			var existing = FindHighlightRuleSet(aRuleSet.Name);
			if (existing != null)
				existing.MergeFrom(aRuleSet);
			else
				Rules.Add(aRuleSet);
		}

		public void ResolveReferences()
		{
			// Resolve references from Span definitions to RuleSets
			ResolveRuleSetReferences();
			// Resolve references from RuleSet defintitions to Highlighters defined in an external mode file
			ResolveExternalReferences();
		}

		private void ResolveRuleSetReferences()
		{
			foreach (var ruleSet in Rules)
			{
				if (ruleSet.Name == null)
					_defaultRuleSet = ruleSet;

				foreach (Span aSpan in ruleSet.Spans)
					if (aSpan.Rule != null)
					{
						var found = false;
						foreach (var refSet in Rules)
							if (refSet.Name == aSpan.Rule)
							{
								found = true;
								aSpan.RuleSet = refSet;
								break;
							}
						if (!found)
						{
							aSpan.RuleSet = null;
							throw new HighlightingDefinitionInvalidException("The RuleSet " + aSpan.Rule +
							                                                 " could not be found in mode definition " + Name);
						}
					}
					else
					{
						aSpan.RuleSet = null;
					}
			}

			if (_defaultRuleSet == null)
				throw new HighlightingDefinitionInvalidException("No default RuleSet is defined for mode definition " + Name);
		}

		private void ResolveExternalReferences()
		{
			foreach (var ruleSet in Rules)
			{
				ruleSet.Highlighter = this;
				if (ruleSet.Reference != null)
				{
					var highlighter = HighlightingManager.Manager.FindHighlighter(ruleSet.Reference);

					if (highlighter == null)
						throw new HighlightingDefinitionInvalidException("The mode defintion " + ruleSet.Reference +
						                                                 " which is refered from the " + Name +
						                                                 " mode definition could not be found");
					if (highlighter is IHighlightingStrategyUsingRuleSets)
						ruleSet.Highlighter = (IHighlightingStrategyUsingRuleSets) highlighter;
					else
						throw new HighlightingDefinitionInvalidException("The mode defintion " + ruleSet.Reference +
						                                                 " which is refered from the " + Name +
						                                                 " mode definition does not implement IHighlightingStrategyUsingRuleSets");
				}
			}
		}

		public void SetColorFor(string name, HighlightColor color)
		{
			if (name == "Default")
				DefaultTextColor = new HighlightColor(color.Color, color.Bold, color.Italic);
			_environmentColors[name] = color;
		}

		protected virtual HighlightColor GetColor(HighlightRuleSet ruleSet, IDocument document, LineSegment currentSegment,
			int currentOffset, int currentLength)
		{
			if (ruleSet != null)
			{
				if (ruleSet.Reference != null)
					return ruleSet.Highlighter.GetColor(document, currentSegment, currentOffset, currentLength);
				return (HighlightColor) ruleSet.KeyWords[document, currentSegment, currentOffset, currentLength];
			}
			return null;
		}

		private bool MarkTokensInLine(IDocument document, int lineNumber, ref bool spanChanged)
		{
			CurrentLineNumber = lineNumber;
			var processNextLine = false;
			var previousLine = lineNumber > 0 ? document.GetLineSegment(lineNumber - 1) : null;

			CurrentSpanStack = previousLine != null && previousLine.HighlightSpanStack != null
				? previousLine.HighlightSpanStack.Clone()
				: null;
			if (CurrentSpanStack != null)
			{
				while (!CurrentSpanStack.IsEmpty && CurrentSpanStack.Peek().StopEol)
					CurrentSpanStack.Pop();
				if (CurrentSpanStack.IsEmpty)
					CurrentSpanStack = null;
			}

			CurrentLine = document.LineSegmentCollection[lineNumber];

			if (CurrentLine.Length == -1)
				return false;

			var words = ParseLine(document);

			if (CurrentSpanStack != null && CurrentSpanStack.IsEmpty)
				CurrentSpanStack = null;

			// Check if the span state has changed, if so we must re-render the next line
			// This check may seem utterly complicated but I didn't want to introduce any function calls
			// or allocations here for perf reasons.
			if (CurrentLine.HighlightSpanStack != CurrentSpanStack)
				if (CurrentLine.HighlightSpanStack == null)
				{
					processNextLine = false;
					foreach (var sp in CurrentSpanStack)
						if (!sp.StopEol)
						{
							spanChanged = true;
							processNextLine = true;
							break;
						}
				}
				else if (CurrentSpanStack == null)
				{
					processNextLine = false;
					foreach (var sp in CurrentLine.HighlightSpanStack)
						if (!sp.StopEol)
						{
							spanChanged = true;
							processNextLine = true;
							break;
						}
				}
				else
				{
					var e1 = CurrentSpanStack.GetEnumerator();
					var e2 = CurrentLine.HighlightSpanStack.GetEnumerator();
					var done = false;
					while (!done)
					{
						var blockSpanIn1 = false;
						while (e1.MoveNext())
							if (!e1.Current.StopEol)
							{
								blockSpanIn1 = true;
								break;
							}
						var blockSpanIn2 = false;
						while (e2.MoveNext())
							if (!e2.Current.StopEol)
							{
								blockSpanIn2 = true;
								break;
							}
						if (blockSpanIn1 || blockSpanIn2)
						{
							if (blockSpanIn1 && blockSpanIn2)
							{
								if (e1.Current != e2.Current)
								{
									done = true;
									processNextLine = true;
									spanChanged = true;
								}
							}
							else
							{
								spanChanged = true;
								done = true;
								processNextLine = true;
							}
						}
						else
						{
							done = true;
							processNextLine = false;
						}
					}
				}
			else
				processNextLine = false;

			//// Alex: remove old words
			if (CurrentLine.Words != null) CurrentLine.Words.Clear();
			CurrentLine.Words = words;
			CurrentLine.HighlightSpanStack = CurrentSpanStack != null && !CurrentSpanStack.IsEmpty ? CurrentSpanStack : null;

			return processNextLine;
		}

		private void UpdateSpanStateVariables()
		{
			InSpan = CurrentSpanStack != null && !CurrentSpanStack.IsEmpty;
			ActiveSpan = InSpan ? CurrentSpanStack.Peek() : null;
			ActiveRuleSet = GetRuleSet(ActiveSpan);
		}

		private List<TextWord> ParseLine(IDocument document)
		{
			var words = new List<TextWord>();
			HighlightColor markNext = null;

			CurrentOffset = 0;
			CurrentLength = 0;
			UpdateSpanStateVariables();

			var currentLineLength = CurrentLine.Length;
			var currentLineOffset = CurrentLine.Offset;

			for (var i = 0; i < currentLineLength; ++i)
			{
				var ch = document.GetCharAt(currentLineOffset + i);
				switch (ch)
				{
					case '\n':
					case '\r':
						PushCurWord(document, ref markNext, words);
						++CurrentOffset;
						break;
					case ' ':
						PushCurWord(document, ref markNext, words);
						if (ActiveSpan != null && ActiveSpan.Color.HasBackground)
							words.Add(new TextWord.SpaceTextWord(ActiveSpan.Color));
						else
							words.Add(TextWord.Space);
						++CurrentOffset;
						break;
					case '\t':
						PushCurWord(document, ref markNext, words);
						if (ActiveSpan != null && ActiveSpan.Color.HasBackground)
							words.Add(new TextWord.TabTextWord(ActiveSpan.Color));
						else
							words.Add(TextWord.Tab);
						++CurrentOffset;
						break;
					default:
					{
						// handle escape characters
						var escapeCharacter = '\0';
						if (ActiveSpan != null && ActiveSpan.EscapeCharacter != '\0')
							escapeCharacter = ActiveSpan.EscapeCharacter;
						else if (ActiveRuleSet != null)
							escapeCharacter = ActiveRuleSet.EscapeCharacter;
						if (escapeCharacter != '\0' && escapeCharacter == ch)
							if (ActiveSpan != null && ActiveSpan.End != null && ActiveSpan.End.Length == 1
							    && escapeCharacter == ActiveSpan.End[0])
							{
								// the escape character is a end-doubling escape character
								// it may count as escape only when the next character is the escape, too
								if (i + 1 < currentLineLength)
									if (document.GetCharAt(currentLineOffset + i + 1) == escapeCharacter)
									{
										CurrentLength += 2;
										PushCurWord(document, ref markNext, words);
										++i;
										continue;
									}
							}
							else
							{
								// this is a normal \-style escape
								++CurrentLength;
								if (i + 1 < currentLineLength)
									++CurrentLength;
								PushCurWord(document, ref markNext, words);
								++i;
								continue;
							}

						// highlight digits
						if (!InSpan &&
						    (char.IsDigit(ch) ||
						     ch == '.' && i + 1 < currentLineLength && char.IsDigit(document.GetCharAt(currentLineOffset + i + 1))) &&
						    CurrentLength == 0)
						{
							var ishex = false;
							var isfloatingpoint = false;

							if (ch == '0' && i + 1 < currentLineLength && char.ToUpper(document.GetCharAt(currentLineOffset + i + 1)) == 'X')
							{
								// hex digits
								const string hex = "0123456789ABCDEF";
								++CurrentLength;
								++i; // skip 'x'
								++CurrentLength;
								ishex = true;
								while (i + 1 < currentLineLength &&
								       hex.IndexOf(char.ToUpper(document.GetCharAt(currentLineOffset + i + 1))) != -1)
								{
									++i;
									++CurrentLength;
								}
							}
							else
							{
								++CurrentLength;
								while (i + 1 < currentLineLength && char.IsDigit(document.GetCharAt(currentLineOffset + i + 1)))
								{
									++i;
									++CurrentLength;
								}
							}
							if (!ishex && i + 1 < currentLineLength && document.GetCharAt(currentLineOffset + i + 1) == '.')
							{
								isfloatingpoint = true;
								++i;
								++CurrentLength;
								while (i + 1 < currentLineLength && char.IsDigit(document.GetCharAt(currentLineOffset + i + 1)))
								{
									++i;
									++CurrentLength;
								}
							}

							if (i + 1 < currentLineLength && char.ToUpper(document.GetCharAt(currentLineOffset + i + 1)) == 'E')
							{
								isfloatingpoint = true;
								++i;
								++CurrentLength;
								if (i + 1 < currentLineLength &&
								    (document.GetCharAt(currentLineOffset + i + 1) == '+' ||
								     document.GetCharAt(CurrentLine.Offset + i + 1) == '-'))
								{
									++i;
									++CurrentLength;
								}
								while (i + 1 < CurrentLine.Length && char.IsDigit(document.GetCharAt(currentLineOffset + i + 1)))
								{
									++i;
									++CurrentLength;
								}
							}

							if (i + 1 < CurrentLine.Length)
							{
								var nextch = char.ToUpper(document.GetCharAt(currentLineOffset + i + 1));
								if (nextch == 'F' || nextch == 'M' || nextch == 'D')
								{
									isfloatingpoint = true;
									++i;
									++CurrentLength;
								}
							}

							if (!isfloatingpoint)
							{
								var isunsigned = false;
								if (i + 1 < currentLineLength && char.ToUpper(document.GetCharAt(currentLineOffset + i + 1)) == 'U')
								{
									++i;
									++CurrentLength;
									isunsigned = true;
								}
								if (i + 1 < currentLineLength && char.ToUpper(document.GetCharAt(currentLineOffset + i + 1)) == 'L')
								{
									++i;
									++CurrentLength;
									if (!isunsigned && i + 1 < currentLineLength &&
									    char.ToUpper(document.GetCharAt(currentLineOffset + i + 1)) == 'U')
									{
										++i;
										++CurrentLength;
									}
								}
							}

							words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, DigitColor, false));
							CurrentOffset += CurrentLength;
							CurrentLength = 0;
							continue;
						}

						// Check for SPAN ENDs
						if (InSpan)
							if (ActiveSpan.End != null && ActiveSpan.End.Length > 0)
								if (MatchExpr(CurrentLine, ActiveSpan.End, i, document, ActiveSpan.IgnoreCase))
								{
									PushCurWord(document, ref markNext, words);
									var regex = GetRegString(CurrentLine, ActiveSpan.End, i, document);
									CurrentLength += regex.Length;
									words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, ActiveSpan.EndColor, false));
									CurrentOffset += CurrentLength;
									CurrentLength = 0;
									i += regex.Length - 1;
									CurrentSpanStack.Pop();
									UpdateSpanStateVariables();
									continue;
								}

						// check for SPAN BEGIN
						if (ActiveRuleSet != null)
							foreach (Span span in ActiveRuleSet.Spans)
								if ((!span.IsBeginSingleWord || CurrentLength == 0)
								    &&
								    (!span.IsBeginStartOfLine.HasValue ||
								     span.IsBeginStartOfLine.Value ==
								     (CurrentLength == 0 &&
								      words.TrueForAll(delegate(TextWord textWord) { return textWord.Type != TextWordType.Word; })))
								    && MatchExpr(CurrentLine, span.Begin, i, document, ActiveRuleSet.IgnoreCase))
								{
									PushCurWord(document, ref markNext, words);
									var regex = GetRegString(CurrentLine, span.Begin, i, document);

									if (!OverrideSpan(regex, document, words, span, ref i))
									{
										CurrentLength += regex.Length;
										words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, span.BeginColor, false));
										CurrentOffset += CurrentLength;
										CurrentLength = 0;

										i += regex.Length - 1;
										if (CurrentSpanStack == null)
											CurrentSpanStack = new SpanStack();
										CurrentSpanStack.Push(span);
										span.IgnoreCase = ActiveRuleSet.IgnoreCase;

										UpdateSpanStateVariables();
									}

									goto skip;
								}

						// check if the char is a delimiter
						if (ActiveRuleSet != null && ch < 256 && ActiveRuleSet.Delimiters[ch])
						{
							PushCurWord(document, ref markNext, words);
							if (CurrentOffset + CurrentLength + 1 < CurrentLine.Length)
							{
								++CurrentLength;
								PushCurWord(document, ref markNext, words);
								goto skip;
							}
						}

						++CurrentLength;
						skip:
						continue;
					}
				}
			}

			PushCurWord(document, ref markNext, words);

			OnParsedLine(document, CurrentLine, words);

			return words;
		}

		protected virtual void OnParsedLine(IDocument document, LineSegment currentLine, List<TextWord> words)
		{
		}

		protected virtual bool OverrideSpan(string spanBegin, IDocument document, List<TextWord> words, Span span,
			ref int lineOffset)
		{
			return false;
		}

		/// <summary>
		///     pushes the curWord string on the word list, with the
		///     correct color.
		/// </summary>
		private void PushCurWord(IDocument document, ref HighlightColor markNext, List<TextWord> words)
		{
			// Svante Lidman : Need to look through the next prev logic.
			if (CurrentLength > 0)
			{
				if (words.Count > 0 && ActiveRuleSet != null)
				{
					TextWord prevWord = null;
					var pInd = words.Count - 1;
					while (pInd >= 0)
					{
						if (!words[pInd].IsWhiteSpace)
						{
							prevWord = words[pInd];
							if (prevWord.HasDefaultColor)
							{
								var marker = (PrevMarker) ActiveRuleSet.PrevMarkers[document, CurrentLine, CurrentOffset, CurrentLength];
								if (marker != null)
									prevWord.SyntaxColor = marker.Color;
							}
							break;
						}
						pInd--;
					}
				}

				if (InSpan)
				{
					HighlightColor c = null;
					var hasDefaultColor = true;
					if (ActiveSpan.Rule == null)
					{
						c = ActiveSpan.Color;
					}
					else
					{
						c = GetColor(ActiveRuleSet, document, CurrentLine, CurrentOffset, CurrentLength);
						hasDefaultColor = false;
					}

					if (c == null)
					{
						c = ActiveSpan.Color;
						if (c.Color == Color.Transparent)
							c = DefaultTextColor;
						hasDefaultColor = true;
					}
					words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, markNext != null ? markNext : c,
						hasDefaultColor));
				}
				else
				{
					var c = markNext != null
						? markNext
						: GetColor(ActiveRuleSet, document, CurrentLine, CurrentOffset, CurrentLength);
					if (c == null)
						words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, DefaultTextColor, true));
					else
						words.Add(new TextWord(document, CurrentLine, CurrentOffset, CurrentLength, c, false));
				}

				if (ActiveRuleSet != null)
				{
					var nextMarker = (NextMarker) ActiveRuleSet.NextMarkers[document, CurrentLine, CurrentOffset, CurrentLength];
					if (nextMarker != null)
					{
						if (nextMarker.MarkMarker && words.Count > 0)
						{
							var prevword = words[words.Count - 1];
							prevword.SyntaxColor = nextMarker.Color;
						}
						markNext = nextMarker.Color;
					}
					else
					{
						markNext = null;
					}
				}
				CurrentOffset += CurrentLength;
				CurrentLength = 0;
			}
		}

		#region Matching

		/// <summary>
		///     get the string, which matches the regular expression expr,
		///     in string s2 at index
		/// </summary>
		private static string GetRegString(LineSegment lineSegment, char[] expr, int index, IDocument document)
		{
			var j = 0;
			var regexpr = new StringBuilder();

			for (var i = 0; i < expr.Length; ++i, ++j)
			{
				if (index + j >= lineSegment.Length)
					break;

				switch (expr[i])
				{
					case '@': // "special" meaning
						++i;
						if (i == expr.Length)
							throw new HighlightingDefinitionInvalidException("Unexpected end of @ sequence, use @@ to look for a single @.");
						switch (expr[i])
						{
							case '!': // don't match the following expression
								var whatmatch = new StringBuilder();
								++i;
								while (i < expr.Length && expr[i] != '@')
									whatmatch.Append(expr[i++]);
								break;
							case '@': // matches @
								regexpr.Append(document.GetCharAt(lineSegment.Offset + index + j));
								break;
						}
						break;
					default:
						if (expr[i] != document.GetCharAt(lineSegment.Offset + index + j))
							return regexpr.ToString();
						regexpr.Append(document.GetCharAt(lineSegment.Offset + index + j));
						break;
				}
			}
			return regexpr.ToString();
		}

		/// <summary>
		///     returns true, if the get the string s2 at index matches the expression expr
		/// </summary>
		private static bool MatchExpr(LineSegment lineSegment, char[] expr, int index, IDocument document, bool ignoreCase)
		{
			for (int i = 0, j = 0; i < expr.Length; ++i, ++j)
				switch (expr[i])
				{
					case '@': // "special" meaning
						++i;
						if (i == expr.Length)
							throw new HighlightingDefinitionInvalidException("Unexpected end of @ sequence, use @@ to look for a single @.");
						switch (expr[i])
						{
							case 'C': // match whitespace or punctuation
								if (index + j == lineSegment.Offset || index + j >= lineSegment.Offset + lineSegment.Length)
								{
									// nothing (EOL or SOL)
								}
								else
								{
									var ch = document.GetCharAt(lineSegment.Offset + index + j);
									if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch))
										return false;
								}
								break;
							case '!': // don't match the following expression
							{
								var whatmatch = new StringBuilder();
								++i;
								while (i < expr.Length && expr[i] != '@')
									whatmatch.Append(expr[i++]);
								if (lineSegment.Offset + index + j + whatmatch.Length < document.TextLength)
								{
									var k = 0;
									for (; k < whatmatch.Length; ++k)
									{
										var docChar = ignoreCase
											? char.ToUpperInvariant(document.GetCharAt(lineSegment.Offset + index + j + k))
											: document.GetCharAt(lineSegment.Offset + index + j + k);
										var spanChar = ignoreCase ? char.ToUpperInvariant(whatmatch[k]) : whatmatch[k];
										if (docChar != spanChar)
											break;
									}
									if (k >= whatmatch.Length)
										return false;
								}
//									--j;
								break;
							}
							case '-': // don't match the  expression before
							{
								var whatmatch = new StringBuilder();
								++i;
								while (i < expr.Length && expr[i] != '@')
									whatmatch.Append(expr[i++]);
								if (index - whatmatch.Length >= 0)
								{
									var k = 0;
									for (; k < whatmatch.Length; ++k)
									{
										var docChar = ignoreCase
											? char.ToUpperInvariant(document.GetCharAt(lineSegment.Offset + index - whatmatch.Length + k))
											: document.GetCharAt(lineSegment.Offset + index - whatmatch.Length + k);
										var spanChar = ignoreCase ? char.ToUpperInvariant(whatmatch[k]) : whatmatch[k];
										if (docChar != spanChar)
											break;
									}
									if (k >= whatmatch.Length)
										return false;
								}
//									--j;
								break;
							}
							case '@': // matches @
								if (index + j >= lineSegment.Length || '@' != document.GetCharAt(lineSegment.Offset + index + j))
									return false;
								break;
						}
						break;
					default:
					{
						if (index + j >= lineSegment.Length)
							return false;
						var docChar = ignoreCase
							? char.ToUpperInvariant(document.GetCharAt(lineSegment.Offset + index + j))
							: document.GetCharAt(lineSegment.Offset + index + j);
						var spanChar = ignoreCase ? char.ToUpperInvariant(expr[i]) : expr[i];
						if (docChar != spanChar)
							return false;
						break;
					}
				}
			return true;
		}

		#endregion
	}
}