﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Taken from https://github.com/aspnet/Entropy/tree/7c027069b715a4b2ffd126f58def04c6111925c3
// ILogger replaced for IConsole

using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.ConfigFile;

/// <summary>
/// This ConfigurationProvider tries to parse .config files from System.Configuration
/// https://msdn.microsoft.com/en-us/library/system.configuration.configuration(v=vs.110).aspx
/// It can process configurations in the form:
///     <list type="number">
///         <item>
///             <description>&lt;add key=&quot;&quot; value=&quot;&quot; /&gt;</description>
///         </item>
///         <item>
///             <description>&lt;remove key=&quot;&quot; value=&quot;&quot; /&gt;</description>
///         </item>
///         <item>
///             <description>&lt;connectionStrings /&gt; (https://msdn.microsoft.com/en-us/library/ms178411.aspx)</description>
///         </item>
///     </list>
/// </summary>
/// <example>
/// Given the following Web.config:
///   &lt;configuration&gt;
///     &lt;appSettings&gt;
///       &lt;add key=&quot;Application Name&quot; value=&quot;MyApplication&quot; /&gt;
///       &lt;add key=&quot;ConfigurationType&quot; value=&quot;TestConfigurationType&quot; /&gt;
///     &lt;/appSettings&gt;
///     &lt;connectionStrings&gt;
///       &lt;add name=&quot;MyJetConn&quot; connectionString=&quot;Provider=Microsoft.Jet.OLEDB.4.0; Data Source=C:\\testdatasource.accdb; Persist Security Info=False;&quot; providerName=&quot;System.Data.OleDb&quot; /&gt;
///       &lt;add name=&quot;MyExcelConn&quot; connectionString=&quot;Dsn=Excel Files;dbq=data.xlsx;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5&quot; providerName=&quot;System.Data.Odbc&quot; /&gt;
///     &lt;/connectionStrings&gt;
///     &lt;sampleSection&gt;
///       &lt;add key=&quot;setting1&quot; value=&quot;This is the setting1 value&quot; /&gt;
///       &lt;add key=&quot;setting2&quot; value=&quot;This is the setting2 value&quot; /&gt;
///     &lt;/sampleSection&gt;
///   &lt;/configuration&gt;
///
/// Code:
///   var configuration = new ConfigurationBuilder().AddConfigFile(&quot;Web.config&quot;).Build();
///   var setting1 = configuration[&quot;sampleSection:setting1&quot;];
///   var allAppSettings = configuration.GetConfigAsJObject(&quot;appSettings&quot;).GetChildren();
///   var myJetConnection = configuration[&quot;connectionStrings:MyJetConn&quot;];
/// </example>
public class ConfigFileConfigurationProvider : ConfigurationProvider, IConfigurationSource
{
    private readonly string _configuration;
    private readonly bool _loadFromFile;
    private readonly bool _isOptional;
    private readonly IConsole _logger;

    private readonly IEnumerable<IConfigurationParser> _parsers;

    public ConfigFileConfigurationProvider(string configuration, bool loadFromFile, bool optional,
        IConsole logger, params IConfigurationParser[] parsers)
    {
        _loadFromFile = loadFromFile;
        _configuration = configuration;
        _isOptional = optional;
        _logger = logger;
        _parsers = parsers;
    }

    public IEnumerable<string> Keys => Data?.Keys ?? Enumerable.Empty<string>();

    public override void Load()
    {
        if (_loadFromFile && !_isOptional && !File.Exists(_configuration))
        {
            throw new FileNotFoundException("Could not find configuration file to load.", _configuration);
        }

        var document = _loadFromFile ? XDocument.Load(_configuration) : XDocument.Parse(_configuration);

        var context = new Stack<string>();
        var dictionary = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (document.Root != null)
            foreach (var child in document.Root.Elements())
            {
                ParseElement(child, context, dictionary);
            }

        Data = dictionary;
    }

    /// <summary>
    /// Given an XElement tries to parse that element using any of the KeyValueParsers
    /// and adds it to the results dictionary
    /// </summary>
    private void ParseElement(XElement element, Stack<string> context, SortedDictionary<string, string> results) =>
        ParseElement(element, element.Name.LocalName, context, results);

    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

    private void ParseElement(XElement element, string name, Stack<string> context, SortedDictionary<string, string> results)
    {
        context.Push(name);

        foreach (var attr in element.Attributes())
        {
            KeyValueParser.Add(results, _logger, KeyValueParser.GetKey(context, attr.Name.LocalName), attr.Value);
        }

        var parsed = false;

        foreach (var parser in _parsers)
        {
            if (!parser.CanParseElement(element)) continue;

            parsed = true;

            parser.ParseElement(element, context, results);

            break;
        }

        if (!parsed)
        {
            foreach (var (key, elements) in element.Elements()
                         .GroupBy(e => e.Name.LocalName)
                         .ToDictionary(e => e.Key, e => e.ToArray()))
            {
                if (elements.Length == 1)
                {
                    ParseElement(elements[0], elements[0].Name.LocalName, context, results);
                }
                else
                {
                    context.Push(key);

                    for (var index = 0; index < elements.Length; index++)
                    {
                        ParseElement(elements[index], index.ToString(), context, results);
                    }

                    context.Pop();
                }
            }
        }

        context.Pop();
    }
}