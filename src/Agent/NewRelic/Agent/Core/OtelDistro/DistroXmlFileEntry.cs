// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// One listed NR instrumentation XML file: its name, full path, and the stamp (last write time and length) the listing saw,
/// which is enough to tell that a file changed without opening it.
/// </summary>
public sealed class DistroXmlFileEntry
{
    public string HomeName { get; }

    public string Path { get; }

    public DateTime LastWriteTimeUtc { get; }

    public long Length { get; }

    public DistroXmlFileEntry(string homeName, string path, DateTime lastWriteTimeUtc, long length)
    {
        HomeName = homeName;
        Path = path;
        LastWriteTimeUtc = lastWriteTimeUtc;
        Length = length;
    }

    /// <summary>True when both name the same path (ignoring case) with the same stamp.</summary>
    public bool SameAs(DistroXmlFileEntry other) =>
        other != null && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) && LastWriteTimeUtc == other.LastWriteTimeUtc && Length == other.Length;
}
