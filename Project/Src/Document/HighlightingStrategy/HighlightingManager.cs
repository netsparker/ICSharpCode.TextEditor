// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ICSharpCode.TextEditor.Document
{
	public class HighlightingManager
	{
		// hash table from extension name to highlighting definition,
		// OR from extension name to Pair SyntaxMode,ISyntaxModeFileProvider

		private readonly Hashtable _extensionsToName = new Hashtable();
		private readonly ArrayList _syntaxModeFileProviders = new ArrayList();

		static HighlightingManager()
		{
			Manager = new HighlightingManager();
			Manager.AddSyntaxModeFileProvider(new ResourceSyntaxModeProvider());
		}

		public HighlightingManager()
		{
			CreateDefaultHighlightingStrategy();
		}

		public Hashtable HighlightingDefinitions { get; } = new Hashtable();

		public static HighlightingManager Manager { get; }

		public DefaultHighlightingStrategy DefaultHighlighting
			=> (DefaultHighlightingStrategy) HighlightingDefinitions["Default"];

		public void AddSyntaxModeFileProvider(ISyntaxModeFileProvider syntaxModeFileProvider)
		{
			foreach (var syntaxMode in syntaxModeFileProvider.SyntaxModes)
			{
				HighlightingDefinitions[syntaxMode.Name] = new DictionaryEntry(syntaxMode, syntaxModeFileProvider);
				foreach (var extension in syntaxMode.Extensions)
					_extensionsToName[extension.ToUpperInvariant()] = syntaxMode.Name;
			}
			if (!_syntaxModeFileProviders.Contains(syntaxModeFileProvider))
				_syntaxModeFileProviders.Add(syntaxModeFileProvider);
		}

		public void AddHighlightingStrategy(IHighlightingStrategy highlightingStrategy)
		{
			HighlightingDefinitions[highlightingStrategy.Name] = highlightingStrategy;
			foreach (var extension in highlightingStrategy.Extensions)
				_extensionsToName[extension.ToUpperInvariant()] = highlightingStrategy.Name;
		}

		public void ReloadSyntaxModes()
		{
			HighlightingDefinitions.Clear();
			_extensionsToName.Clear();
			CreateDefaultHighlightingStrategy();
			foreach (ISyntaxModeFileProvider provider in _syntaxModeFileProviders)
			{
				provider.UpdateSyntaxModeList();
				AddSyntaxModeFileProvider(provider);
			}
			OnReloadSyntaxHighlighting(EventArgs.Empty);
		}

		private void CreateDefaultHighlightingStrategy()
		{
			var defaultHighlightingStrategy = new DefaultHighlightingStrategy();
			defaultHighlightingStrategy.Extensions = new string[] {};
			defaultHighlightingStrategy.Rules.Add(new HighlightRuleSet());
			HighlightingDefinitions["Default"] = defaultHighlightingStrategy;
		}

		private IHighlightingStrategy LoadDefinition(DictionaryEntry entry)
		{
			var syntaxMode = (SyntaxMode) entry.Key;
			var syntaxModeFileProvider = (ISyntaxModeFileProvider) entry.Value;

			DefaultHighlightingStrategy highlightingStrategy = null;
			try
			{
				var reader = syntaxModeFileProvider.GetSyntaxModeFile(syntaxMode);
				if (reader == null)
					throw new HighlightingDefinitionInvalidException("Could not get syntax mode file for " + syntaxMode.Name);
				highlightingStrategy = HighlightingDefinitionParser.Parse(syntaxMode, reader);
				if (highlightingStrategy.Name != syntaxMode.Name)
					throw new HighlightingDefinitionInvalidException("The name specified in the .xshd '" + highlightingStrategy.Name +
					                                                 "' must be equal the syntax mode name '" + syntaxMode.Name + "'");
			}
			finally
			{
				if (highlightingStrategy == null)
					highlightingStrategy = DefaultHighlighting;
				HighlightingDefinitions[syntaxMode.Name] = highlightingStrategy;
				highlightingStrategy.ResolveReferences();
			}
			return highlightingStrategy;
		}

		internal KeyValuePair<SyntaxMode, ISyntaxModeFileProvider> FindHighlighterEntry(string name)
		{
			foreach (ISyntaxModeFileProvider provider in _syntaxModeFileProviders)
			foreach (var mode in provider.SyntaxModes)
				if (mode.Name == name)
					return new KeyValuePair<SyntaxMode, ISyntaxModeFileProvider>(mode, provider);
			return new KeyValuePair<SyntaxMode, ISyntaxModeFileProvider>(null, null);
		}

		public IHighlightingStrategy FindHighlighter(string name)
		{
			var def = HighlightingDefinitions[name];
			if (def is DictionaryEntry)
				return LoadDefinition((DictionaryEntry) def);
			return def == null ? DefaultHighlighting : (IHighlightingStrategy) def;
		}

		public IHighlightingStrategy FindHighlighterForFile(string fileName)
		{
			var highlighterName = (string) _extensionsToName[Path.GetExtension(fileName).ToUpperInvariant()];
			if (highlighterName != null)
			{
				var def = HighlightingDefinitions[highlighterName];
				if (def is DictionaryEntry)
					return LoadDefinition((DictionaryEntry) def);
				return def == null ? DefaultHighlighting : (IHighlightingStrategy) def;
			}
			return DefaultHighlighting;
		}

		protected virtual void OnReloadSyntaxHighlighting(EventArgs e)
		{
			if (ReloadSyntaxHighlighting != null)
				ReloadSyntaxHighlighting(this, e);
		}

		public event EventHandler ReloadSyntaxHighlighting;
	}
}