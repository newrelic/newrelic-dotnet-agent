// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// One shipped NR instrumentation XML file to list in the table's files section: its home name (the file name in the
/// agent home's extensions directory), the XmlContentHasher hash of the content the table is built from, and whether its
/// points are compiled into the table (false = hash-only). A <see cref="TableEntry"/> names its file by index in the
/// list passed to <see cref="InstrumentationDefinitionsTableWriter.TryWrite"/>.
/// </summary>
public sealed class ShippedXmlFile
{
    public string HomeName { get; }

    public ulong ContentHash { get; }

    public bool Compiled { get; }

    public ShippedXmlFile(string homeName, ulong contentHash, bool compiled)
    {
        HomeName = homeName;
        ContentHash = contentHash;
        Compiled = compiled;
    }
}
