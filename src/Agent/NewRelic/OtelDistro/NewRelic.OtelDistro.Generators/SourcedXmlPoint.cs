// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// An XML point to emit, with the index of the shipped XML file it came from in the file list given to
/// <see cref="IntegrationSourceEmitter"/> (<see cref="PluginSourceFile"/> for the plugin's own XML, which ships in no home),
/// and whether its function id folds in its tracer factory (see <see cref="DistinguishByTracerFactory"/>).
/// </summary>
public sealed class SourcedXmlPoint
{
    /// <summary>The source file of a point from the plugin's own XML: no shipped file.</summary>
    public const ushort PluginSourceFile = 0xFFFF;

    public XmlInstrumentationPoint Point { get; }

    public ushort SourceFile { get; }

    /// <summary>
    /// True for a point kept beside another point with the same assembly, class, method, parameters and version range
    /// but a different tracer factory (at least one of them default-off). Its function id, and so its integration class
    /// name, then folds in the tracer factory so the two stay distinct; the group's primary point keeps the plain id.
    /// </summary>
    public bool DistinguishByTracerFactory { get; }

    public SourcedXmlPoint(XmlInstrumentationPoint point, ushort sourceFile, bool distinguishByTracerFactory = false)
    {
        Point = point;
        SourceFile = sourceFile;
        DistinguishByTracerFactory = distinguishByTracerFactory;
    }
}
