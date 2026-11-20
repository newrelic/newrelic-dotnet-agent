// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>
/// Reads NR's extension instrumentation XML (namespace-agnostic -- the elements are matched by local name only,
/// so <c>xmlns="urn:newrelic-extension"</c> or no namespace at all both parse the same way):
/// <c>extension/instrumentation/tracerFactory[@name,@metricName]/match[@assemblyName,@className]/exactMethodMatcher[@methodName,@parameters]</c>.
/// </summary>
public static class InstrumentationXmlParser
{
    public const string DefaultTracerFactoryName = "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory";

    public static ParseResult Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            return new ParseResult(Array.Empty<XmlInstrumentationPoint>(), new[] { $"Malformed instrumentation XML: {ex.Message}" });
        }

        var points = new List<XmlInstrumentationPoint>();

        foreach (var tracerFactory in document.Descendants().Where(IsTracerFactory))
        {
            // InstrumentationConfiguration.h:264-268: a factory is disabled only when `enabled` equals "false",
            // case-insensitively and untrimmed. Absent, empty, "0", "true" and anything else leave it enabled.
            // The profiler drops a disabled factory; here its points are kept with Enabled == false so a later edit
            // that turns the factory on can be told apart from a point that never existed.
            var enabled = !string.Equals((string)tracerFactory.Attribute("enabled"), "false", StringComparison.OrdinalIgnoreCase);

            var tracerFactoryName = (string)tracerFactory.Attribute("name");
            if (string.IsNullOrEmpty(tracerFactoryName))
            {
                tracerFactoryName = DefaultTracerFactoryName;
            }

            var metricName = (string)tracerFactory.Attribute("metricName");

            foreach (var match in tracerFactory.Elements().Where(IsMatch))
            {
                var assemblyName = (string)match.Attribute("assemblyName");
                var className = (string)match.Attribute("className");
                var minVersion = (string)match.Attribute("minVersion");
                var maxVersion = (string)match.Attribute("maxVersion");

                foreach (var methodMatcher in match.Elements().Where(IsExactMethodMatcher))
                {
                    var methodName = (string)methodMatcher.Attribute("methodName");
                    var parameterTypes = ParseParameterTypes(methodMatcher.Attribute("parameters"));

                    foreach (var singleClassName in SplitClassNames(className))
                    {
                        points.Add(new XmlInstrumentationPoint(tracerFactoryName, metricName, assemblyName, singleClassName, methodName, parameterTypes, minVersion, maxVersion, enabled));
                    }
                }
            }
        }

        return new ParseResult(points, Array.Empty<string>());
    }

    /// <summary>
    /// Mirrors <c>SplitInstrumentationPointsOnClassNames</c> (InstrumentationConfiguration.h:430-464): splits on commas
    /// that sit outside <c>&lt;&gt;</c>/<c>[]</c> brackets, with no trimming and no dropping of empty segments
    /// (<c>"A, B,"</c> gives <c>"A"</c>, <c>" B"</c>, <c>""</c>). The profiler's bracket level is a <c>uint32_t</c>, so
    /// a stray closing bracket wraps it and suppresses every later split; that is kept. A null class name stays one
    /// null entry (the profiler sees an absent attribute as an empty string).
    /// </summary>
    private static IEnumerable<string> SplitClassNames(string className)
    {
        if (className == null)
        {
            yield return null;
            yield break;
        }

        uint bracketLevel = 0;
        var segmentStart = 0;

        for (var i = 0; i < className.Length; i++)
        {
            var character = className[i];

            if (character == '<' || character == '[')
            {
                unchecked { bracketLevel++; }
            }

            if (character == '>' || character == ']')
            {
                unchecked { bracketLevel--; }
            }

            if (bracketLevel == 0 && character == ',')
            {
                yield return className.Substring(segmentStart, i - segmentStart);
                segmentStart = i + 1;
            }
        }

        yield return className.Substring(segmentStart);
    }

    private static IReadOnlyList<string> ParseParameterTypes(XAttribute parametersAttribute)
    {
        if (parametersAttribute == null)
        {
            return null;
        }

        var value = parametersAttribute.Value;
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<string>();
        }

        return SplitTopLevel(value);
    }

    // Splits on commas outside square brackets only: a generic instance (Name`N[A,B], possibly nested) is one
    // parameter. OTel's native matcher renders parameter types the same way, so each piece is passed through as is.
    private static List<string> SplitTopLevel(string value)
    {
        var parameters = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
                case ',':
                    if (depth == 0)
                    {
                        parameters.Add(value.Substring(start, i - start).Trim());
                        start = i + 1;
                    }

                    break;
            }
        }

        parameters.Add(value.Substring(start).Trim());
        return parameters;
    }

    private static bool IsTracerFactory(XElement element) => element.Name.LocalName == "tracerFactory";

    private static bool IsMatch(XElement element) => element.Name.LocalName == "match";

    private static bool IsExactMethodMatcher(XElement element) => element.Name.LocalName == "exactMethodMatcher";
}

/// <summary>
/// The result of <see cref="InstrumentationXmlParser.Parse"/>: either the parsed points, or (for malformed XML)
/// a single error and no points.
/// </summary>
public sealed class ParseResult
{
    public IReadOnlyList<XmlInstrumentationPoint> Points { get; }

    public IReadOnlyList<string> Errors { get; }

    public ParseResult(IReadOnlyList<XmlInstrumentationPoint> points, IReadOnlyList<string> errors)
    {
        Points = points;
        Errors = errors;
    }
}
