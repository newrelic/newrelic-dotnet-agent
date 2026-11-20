// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.OtelDistro.Xml;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableTestData;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>NR instrumentation XML text builders and a shipped-table builder shared by the XML toggle tests.</summary>
public static class DistroShippedXmlTestData
{
    public static string Factory(string name, string metricName, string assembly, string className, string method, string parameters, string enabled = null, string min = null, string max = null)
    {
        var enabledAttribute = enabled == null ? "" : " enabled=\"" + enabled + "\"";
        var classAttribute = className == null ? "" : " className=\"" + className + "\"";
        var minAttribute = min == null ? "" : " minVersion=\"" + min + "\"";
        var maxAttribute = max == null ? "" : " maxVersion=\"" + max + "\"";
        var parametersAttribute = parameters == null ? "" : " parameters=\"" + parameters + "\"";
        return "  <tracerFactory name=\"" + name + "\" metricName=\"" + metricName + "\"" + enabledAttribute + ">\n"
            + "    <match assemblyName=\"" + assembly + "\"" + classAttribute + minAttribute + maxAttribute + ">\n"
            + "      <exactMethodMatcher methodName=\"" + method + "\"" + parametersAttribute + " />\n"
            + "    </match>\n"
            + "  </tracerFactory>\n";
    }

    public static string File(params string[] factories) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<extension xmlns=\"urn:newrelic-extension\">\n<instrumentation>\n" + string.Concat(factories) + "</instrumentation>\n</extension>\n";

    // Shipped-file builder: derives each record the way the generator does (one record per point, nine for an overload-wide
    // point, all sharing the point key PointKeyHasher gives them).
    public sealed class Shipped
    {
        public DistroInstrumentationSites Sites { get; }
        public DistroShippedXmlManifest Manifest { get; }

        public Shipped((string Name, string Text, bool Compiled)[] files)
        {
            var shippedFiles = files.Select(f => new ShippedFile { HomeName = f.Name, ContentHash = XmlContentHasher.Compute(f.Text), Compiled = f.Compiled }).ToArray();
            var rows = new List<Row> { new Row { Assembly = "Plugin", Type = "NS.Plugin", Method = "Own", TracerFactory = "FP", PointKey = 99, FunctionId = 1000 } };
            ulong functionId = 1;
            for (var file = 0; file < files.Length; file++)
            {
                if (!files[file].Compiled)
                {
                    continue;
                }

                foreach (var point in InstrumentationXmlParser.Parse(files[file].Text).Points)
                {
                    var key = PointKeyHasher.Compute(point, point.ClassName, VersionRangeConverter.Convert(point.MinVersion, point.MaxVersion).RangeKey);
                    var arities = point.ParameterTypes == null ? Enumerable.Range(0, 9).ToArray() : new[] { point.ParameterTypes.Count };
                    foreach (var arity in arities)
                    {
                        rows.Add(new Row
                        {
                            Assembly = point.AssemblyName,
                            Type = point.ClassName,
                            Method = point.MethodName,
                            Signature = Enumerable.Repeat("_", arity + 1).ToArray(),
                            TracerFactory = point.TracerFactoryName,
                            MetricName = point.MetricName,
                            ArgumentSignature = point.ParameterTypes == null ? "" : string.Join(",", point.ParameterTypes),
                            FunctionId = functionId++,
                            PointKey = key,
                            SourceFile = (ushort)file,
                            DefaultOff = !point.Enabled,
                        });
                    }
                }
            }

            var table = Build(shippedFiles, rows.ToArray());
            var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out var manifest, out var failure);
            Assert.That(ok, Is.True, () => failure + System.Environment.NewLine + Dump(table));
            Sites = sites;
            Manifest = manifest;
        }
    }
}
