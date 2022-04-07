// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Taken from https://github.com/aspnet/Entropy/tree/7c027069b715a4b2ffd126f58def04c6111925c3
// ILogger replaced for IConsole

using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.ConfigFile;

/// <summary>
/// ConfigurationProvider for *.config files.  Only elements that contain
/// &lt;add KeyName=&quot;A&quot; ValueName=&quot;B&quot;/&gt; or &lt;remove KeyName=&quot;A&quot; ValueName=&quot;B&quot;/&gt; (value is not
/// considered for a remove action) as their descendents are used.
/// All others are skipped.
/// KeyName/ValueName can be configured. Default is &quot;key&quot; and &quot;value&quot;, respectively.
/// </summary>
/// <example>
/// The following configuration file will result in the following key-value
/// pairs in the dictionary:
/// @{
///     { &quot;nodea:TheKey&quot; : &quot;TheValue&quot; },
///     { &quot;nodeb:nested:NestedKey&quot; : &quot;ValueA&quot; },
///     { &quot;nodeb:nested:NestedKey2&quot; : &quot;ValueB&quot; },
/// }
///
/// &lt;configuration&gt;
///     &lt;nodea&gt;
///         &lt;add key=&quot;TheKey&quot; value=&quot;TheValue&quot; /&gt;
///     &lt;/nodea&gt;
///     &lt;nodeb&gt;
///         &lt;nested&gt;
///             &lt;add key=&quot;NestedKey&quot; value=&quot;ValueA&quot; /&gt;
///             &lt;add key=&quot;NestedKey2&quot; value=&quot;ValueB&quot; /&gt;
///             &lt;remove key=&quot;SomeTestKey&quot; /&gt;
///         &lt;/nested&gt;
///     &lt;/nodeb&gt;
/// &lt;/configuration&gt;
///
/// </example>
internal class KeyValueParser : IConfigurationParser
{
    private readonly IConsole _logger;
    private readonly string _addElement;
    private readonly string _removeElement;
    private readonly string _clearElement;
    private readonly string _keyName;
    private readonly string _valueName;

    public KeyValueParser(string key = "key", string value = "value", IConsole logger = null,
        string addElement = "add", string removeElement = "remove", string clearElement = "clear")
    {
        _keyName = key;
        _valueName = value;

        _addElement = addElement;
        _removeElement = removeElement;
        _clearElement = clearElement;
        _logger = logger;
    }

    public bool CanParseElement(XElement element) =>
        element.Elements().All(node =>
            node.Name.LocalName == _addElement || node.Name.LocalName == _removeElement
                ? node.Attribute(_keyName) != null
                : node.Name.LocalName == _clearElement);

    public void ParseElement(XElement element, Stack<string> context, SortedDictionary<string, string> results)
    {
        foreach (var node in element.Elements())
        {
            var action = GetAction(node.Name.ToString());

            var key = node.Attribute(_keyName);

            switch (action)
            {
                case ConfigurationAction.Add:
                    if (key == null)
                    {
                        _logger?.WriteLine($"[{node}] is not supported because it does not have an attribute with {_keyName}");

                        continue;
                    }

                    context.Push(key.Value);

                    AddValueToDictionary(node, context, results);

                    context.Pop();
                    break;
                case ConfigurationAction.Remove:
                    if (key == null)
                    {
                        _logger?.WriteLine($"[{node}] is not supported because it does not have an attribute with {_keyName}");

                        continue;
                    }

                    var fullKey = GetKey(context, key.Value);

                    results.Remove(fullKey);

                    Clear(results, fullKey + ConfigurationPath.KeyDelimiter);
                    break;
                case ConfigurationAction.Clear:
                    Clear(results, GetKey(context, ""));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported action: [{action}]");
            }
        }
    }

    protected virtual ConfigurationAction GetAction(string elementName) =>
        elementName == _addElement
            ? ConfigurationAction.Add
            : elementName == _removeElement
                ? ConfigurationAction.Remove
                : elementName == _clearElement
                    ? ConfigurationAction.Clear
                    : throw new NotSupportedException($"Unsupported action: [{elementName}]");

    private static string GetKey(IEnumerable<string> context)
    {
        return string.Join(ConfigurationPath.KeyDelimiter, context.Reverse());
    }

    public static string GetKey(IEnumerable<string> context, string name)
    {
        return string.Join(ConfigurationPath.KeyDelimiter, context.Reverse().Concat(new[] { name }));
    }

    public static void Add(SortedDictionary<string, string> results, IConsole logger, string key, string value)
    {
        if (value == null) return;

        if (results.ContainsKey(key))
        {
            logger?.WriteLine($"{key} exists. Replacing existing value [{results[key]}] with {value}");

            results[key] = value;
        }
        else
        {
            results.Add(key, value);
        }
    }

    private static void Clear(SortedDictionary<string, string> results, string keyPrefix)
    {
        foreach (var key in results.Keys.ToArray())
        {
            if (key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                results.Remove(key);
        }
    }

    protected void AddValueToDictionary(XElement element, Stack<string> context, SortedDictionary<string, string> results)
    {
        var hasMore = false;
        string value = null;

        foreach (var attribute in element.Attributes())
        {
            if (attribute.Name.LocalName == _keyName) continue;

            if (attribute.Name.LocalName == _valueName) value = attribute.Value;
            else
            {
                hasMore = true;

                Add(results, _logger, GetKey(context, attribute.Name.LocalName), attribute.Value);
            }
        }

        if (hasMore) Add(results, _logger, GetKey(context, _valueName), value);

        else if (value != null) Add(results, _logger, GetKey(context), value);
    }
}