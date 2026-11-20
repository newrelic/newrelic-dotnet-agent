// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The shipped NR instrumentation XML files named by a read NRIDT table (its files section; see
/// InstrumentationDefinitionsTableFormat.cs), by file index: each file's home name (its file name in the agent home's
/// extensions directory), the hash of the content the table was built from, and whether its points are compiled into
/// the table's records or the file is tracked by hash only. Records name their file by this index
/// (<see cref="DistroInstrumentationSites.SourceFile"/>). Built only by <see cref="InstrumentationDefinitionsTable.TryRead"/>,
/// from a table it has fully validated, so home names are non-empty and unique ignoring case. Immutable.
/// </summary>
public sealed class DistroShippedXmlManifest
{
    private readonly string[] _homeNames;
    private readonly ulong[] _contentHashes;
    private readonly bool[] _compiled;
    private readonly Dictionary<string, int> _fileByHomeName;

    public int FileCount => _homeNames.Length;

    // Internal: only the reader may build one. It hands over arrays it owns and never touches again.
    internal DistroShippedXmlManifest(string[] homeNames, ulong[] contentHashes, bool[] compiled)
    {
        _homeNames = homeNames;
        _contentHashes = contentHashes;
        _compiled = compiled;

        // Ordinal ignoring case, as Windows compares file names. The reader has rejected duplicates, so every name adds.
        _fileByHomeName = new Dictionary<string, int>(homeNames.Length, StringComparer.OrdinalIgnoreCase);
        for (var file = 0; file < homeNames.Length; file++)
        {
            _fileByHomeName.Add(homeNames[file], file);
        }
    }

    /// <summary>The home name of <paramref name="file"/>. Throws ArgumentOutOfRangeException for an index not in range.</summary>
    public string HomeName(int file) => _homeNames[CheckFile(file)];

    /// <summary>
    /// The content hash (XmlContentHasher) of <paramref name="file"/> as shipped. Throws ArgumentOutOfRangeException for
    /// an index not in range.
    /// </summary>
    public ulong ContentHash(int file) => _contentHashes[CheckFile(file)];

    /// <summary>
    /// True when the file's points are compiled into the table; false for a hash-only file. Throws
    /// ArgumentOutOfRangeException for an index not in range.
    /// </summary>
    public bool IsCompiled(int file) => _compiled[CheckFile(file)];

    /// <summary>
    /// Finds the file whose home name equals <paramref name="homeName"/> ignoring case (ordinal). Returns false, with
    /// <paramref name="file"/> -1, for an unknown or null name. Never throws.
    /// </summary>
    public bool TryFindFile(string homeName, out int file)
    {
        if (homeName != null && _fileByHomeName.TryGetValue(homeName, out file))
        {
            return true;
        }

        file = -1;
        return false;
    }

    private int CheckFile(int file)
    {
        if ((uint)file >= (uint)_homeNames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(file), file, "file index must be at least 0 and less than FileCount (" + _homeNames.Length + ")");
        }

        return file;
    }
}
