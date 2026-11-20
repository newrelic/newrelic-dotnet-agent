// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Text;
using NewRelic.Agent.Core.OtelDistro.Xml;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// One NR instrumentation XML file found in the distro's extensions directories: read (its bytes), unreadable (a read
/// error), or not read at all (a hash-only shipped file, which nothing looks at).
/// </summary>
public sealed class DistroXmlFile
{
    /// <summary>The file's name in its directory.</summary>
    public string HomeName { get; }

    /// <summary>The file's full path; identifies the file between runs (two directories may hold the same name).</summary>
    public string Path { get; }

    /// <summary>The file's bytes; null when it was not read or could not be read.</summary>
    public byte[] Bytes { get; }

    /// <summary>Why the file could not be read; null when it was read or not read at all.</summary>
    public string ReadError { get; }

    /// <summary>True when the file was opened (successfully or not); false for a file that was only listed.</summary>
    public bool IsRead => Bytes != null || ReadError != null;

    public DistroXmlFile(string homeName, string path, byte[] bytes, string readError)
    {
        HomeName = homeName;
        Path = path;
        Bytes = bytes;
        ReadError = readError;
    }

    /// <summary>A listed file that was deliberately not read.</summary>
    public static DistroXmlFile NotRead(string homeName, string path) => new DistroXmlFile(homeName, path, null, null);
}

/// <summary>An enabled XML point that is not part of the distro's compiled instrumentation.</summary>
public sealed class DistroXmlUnsupportedPoint
{
    public string File { get; }

    public string TypeName { get; }

    public string MethodName { get; }

    /// <summary>The comma-separated parameter types as written; "*" when the point has no parameters attribute (every overload).</summary>
    public string Parameters { get; }

    public string TracerFactory { get; }

    /// <summary>The point's key (PointKeyHasher), the same for every copy of the point.</summary>
    public ulong PointKey { get; }

    public DistroXmlUnsupportedPoint(string file, string typeName, string methodName, string parameters, string tracerFactory, ulong pointKey)
    {
        File = file;
        TypeName = typeName;
        MethodName = methodName;
        Parameters = parameters;
        TracerFactory = tracerFactory;
        PointKey = pointKey;
    }
}

/// <summary>A file that failed to parse at start-up and was skipped.</summary>
public sealed class DistroXmlInvalidFile
{
    public string HomeName { get; }

    public string Reason { get; }

    public DistroXmlInvalidFile(string homeName, string reason)
    {
        HomeName = homeName;
        Reason = reason;
    }
}

/// <summary>The content hash of one read file (see <see cref="DistroXmlEffectiveSetResult.FileHashes"/>).</summary>
public sealed class DistroXmlFileHash
{
    public string HomeName { get; }

    public string Path { get; }

    public ulong Hash { get; }

    public DistroXmlFileHash(string homeName, string path, ulong hash)
    {
        HomeName = homeName;
        Path = path;
        Hash = hash;
    }
}

/// <summary>The outcome of <see cref="DistroXmlEffectiveSet.Compute"/>.</summary>
public sealed class DistroXmlEffectiveSetResult
{
    private static readonly string[] NoNames = new string[0];

    /// <summary>
    /// True when a parsed file had parse errors and invalid files were not to be skipped: nothing was computed, <see cref="On"/>
    /// is null and the other lists are partial.
    /// </summary>
    public bool Rejected { get; }

    public string RejectedFile { get; }

    public string RejectedReason { get; }

    /// <summary>The on-state of every site (index = site index); null when <see cref="Rejected"/>.</summary>
    public bool[] On { get; }

    /// <summary>Home names of the files that were parsed without errors (changed or unknown to the manifest), in input order.</summary>
    public IReadOnlyList<string> ParsedFiles { get; }

    /// <summary>Home names of the files that could not be read (hash-only files are never read, so never listed).</summary>
    public IReadOnlyList<string> UnreadableFiles { get; }

    /// <summary>Files skipped because they failed to parse (only when invalid files are skipped).</summary>
    public IReadOnlyList<DistroXmlInvalidFile> InvalidFiles { get; }

    /// <summary>Home names of compiled shipped files that are not on disk (their points are off).</summary>
    public IReadOnlyList<string> MissingFiles { get; }

    public IReadOnlyList<DistroXmlUnsupportedPoint> Unsupported { get; }

    /// <summary>The content hash of every file that was read and could be read, except hash-only files.</summary>
    public IReadOnlyList<DistroXmlFileHash> FileHashes { get; }

    /// <summary>How many present shipped files are tracked by hash only (not compiled into the distro yet).</summary>
    public int HashOnlyFileCount { get; }

    public DistroXmlEffectiveSetResult(bool rejected, string rejectedFile, string rejectedReason, bool[] on, IReadOnlyList<string> parsedFiles, IReadOnlyList<string> unreadableFiles, IReadOnlyList<DistroXmlInvalidFile> invalidFiles, IReadOnlyList<string> missingFiles, IReadOnlyList<DistroXmlUnsupportedPoint> unsupported, IReadOnlyList<DistroXmlFileHash> fileHashes, int hashOnlyFileCount)
    {
        Rejected = rejected;
        RejectedFile = rejectedFile;
        RejectedReason = rejectedReason;
        On = on;
        ParsedFiles = parsedFiles ?? NoNames;
        UnreadableFiles = unreadableFiles ?? NoNames;
        InvalidFiles = invalidFiles ?? new DistroXmlInvalidFile[0];
        MissingFiles = missingFiles ?? NoNames;
        Unsupported = unsupported ?? new DistroXmlUnsupportedPoint[0];
        FileHashes = fileHashes ?? new DistroXmlFileHash[0];
        HashOnlyFileCount = hashOnlyFileCount;
    }
}

/// <summary>
/// Works out which of the distro's compiled instrumentation sites should be on, from the NR instrumentation XML on disk.
/// The enabled keys of all files are unioned: a shipped compiled file that is unchanged (same content hash of its bytes)
/// or unreadable contributes its records' default-on point keys without being parsed; a compiled file that was edited,
/// and a file the table does not know, is decoded, parsed and contributes the keys of its enabled points; a hash-only
/// shipped file is ignored whatever it holds (and need not have been read); a compiled file missing from disk contributes
/// nothing. A site is on when it came from the plugin's own XML or its point key is in the union. Enabled points no record
/// has the key of are reported as unsupported (the distro cannot instrument what it did not compile), except points the
/// generator never emits (see XmlPointEligibility).
///
/// A parse error rejects the whole result (the caller keeps its previous state, as agent mode's refresh does), unless
/// invalid files are skipped (agent mode's start-up rule): then an invalid compiled file contributes its defaults, as an
/// unreadable one does, and an invalid extra file contributes nothing.
///
/// When every compiled file is present and unchanged (or unreadable) and there is no other file, the result is the
/// defaults, computed without building any key set. Pure: no file access, no logging.
/// </summary>
public static class DistroXmlEffectiveSet
{
    private const int PluginSourceFile = -1;
    private const char ByteOrderMark = (char)0xFEFF;

    // What pass 1 decided for each input file.
    private const byte StateSkip = 0;
    private const byte StateDefaults = 1;
    private const byte StateParse = 2;

    public static DistroXmlEffectiveSetResult Compute(DistroInstrumentationSites sites, DistroShippedXmlManifest manifest, IReadOnlyList<DistroXmlFile> onDisk, bool skipInvalid)
    {
        var unreadableFiles = new List<string>();
        var fileHashes = new List<DistroXmlFileHash>(onDisk.Count);
        var states = new byte[onDisk.Count];
        var manifestIndexes = new int[onDisk.Count];
        var seenCompiled = new bool[manifest.FileCount];
        var hashOnlyFileCount = 0;
        var anyParse = false;

        for (var i = 0; i < onDisk.Count; i++)
        {
            var file = onDisk[i];
            var known = manifest.TryFindFile(file.HomeName, out var fileIndex);
            manifestIndexes[i] = fileIndex;
            if (known && !manifest.IsCompiled(fileIndex))
            {
                hashOnlyFileCount++;
                continue;
            }

            if (known)
            {
                seenCompiled[fileIndex] = true;
            }

            if (file.Bytes == null)
            {
                // Unreadable (or, defensively, not read): a compiled file keeps its defaults, an unknown one adds nothing.
                unreadableFiles.Add(file.HomeName);
                states[i] = known ? StateDefaults : StateSkip;
                continue;
            }

            var hash = XmlContentHasher.Compute(file.Bytes);
            fileHashes.Add(new DistroXmlFileHash(file.HomeName, file.Path, hash));
            if (known && hash == manifest.ContentHash(fileIndex))
            {
                states[i] = StateDefaults;
                continue;
            }

            states[i] = StateParse;
            anyParse = true;
        }

        var missingFiles = new List<string>();
        for (var file = 0; file < seenCompiled.Length; file++)
        {
            if (!seenCompiled[file] && manifest.IsCompiled(file))
            {
                missingFiles.Add(manifest.HomeName(file));
            }
        }

        if (!anyParse && missingFiles.Count == 0)
        {
            return new DistroXmlEffectiveSetResult(false, null, null, Defaults(sites), null, unreadableFiles, null, missingFiles, null, fileHashes, hashOnlyFileCount);
        }

        var recordKeys = new HashSet<ulong>();
        var defaultOnKeysByFile = new List<ulong>[manifest.FileCount];
        for (var i = 0; i < sites.Count; i++)
        {
            var key = sites.PointKey(i);
            recordKeys.Add(key);

            var file = sites.SourceFile(i);
            if (file != PluginSourceFile && !sites.IsDefaultOff(i))
            {
                (defaultOnKeysByFile[file] ?? (defaultOnKeysByFile[file] = new List<ulong>())).Add(key);
            }
        }

        var enabledKeys = new HashSet<ulong>();
        var parsedFiles = new List<string>();
        var invalidFiles = new List<DistroXmlInvalidFile>();
        var unsupported = new List<DistroXmlUnsupportedPoint>();

        for (var i = 0; i < onDisk.Count; i++)
        {
            var file = onDisk[i];
            if (states[i] == StateParse)
            {
                var parsed = InstrumentationXmlParser.Parse(Decode(file.Bytes).TrimStart(ByteOrderMark));
                if (parsed.Errors.Count == 0)
                {
                    parsedFiles.Add(file.HomeName);
                    foreach (var point in parsed.Points)
                    {
                        AddPoint(file.HomeName, point, recordKeys, enabledKeys, unsupported);
                    }

                    continue;
                }

                if (!skipInvalid)
                {
                    return new DistroXmlEffectiveSetResult(true, file.HomeName, parsed.Errors[0], null, parsedFiles, unreadableFiles, invalidFiles, missingFiles, unsupported, fileHashes, hashOnlyFileCount);
                }

                invalidFiles.Add(new DistroXmlInvalidFile(file.HomeName, parsed.Errors[0]));
            }
            else if (states[i] != StateDefaults)
            {
                continue;
            }

            // Unchanged, unreadable or (skipped) invalid compiled file: its shipped defaults. An unknown file never gets here
            // with a defaults state, and an invalid unknown file has no manifest index.
            var index = manifestIndexes[i];
            var keys = index >= 0 ? defaultOnKeysByFile[index] : null;
            if (keys != null)
            {
                enabledKeys.UnionWith(keys);
            }
        }

        var on = new bool[sites.Count];
        for (var i = 0; i < on.Length; i++)
        {
            on[i] = sites.SourceFile(i) == PluginSourceFile || enabledKeys.Contains(sites.PointKey(i));
        }

        return new DistroXmlEffectiveSetResult(false, null, null, on, parsedFiles, unreadableFiles, invalidFiles, missingFiles, unsupported, fileHashes, hashOnlyFileCount);
    }

    /// <summary>The shipped defaults: every plugin-source site, and every site whose point is not shipped disabled.</summary>
    public static bool[] Defaults(DistroInstrumentationSites sites)
    {
        var on = new bool[sites.Count];
        for (var i = 0; i < on.Length; i++)
        {
            on[i] = sites.SourceFile(i) == PluginSourceFile || !sites.IsDefaultOff(i);
        }

        return on;
    }

    // As File.ReadAllText decodes: UTF-8 unless a byte order mark says otherwise.
    private static string Decode(byte[] bytes)
    {
        using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true))
        {
            return reader.ReadToEnd();
        }
    }

    // The key is computed exactly as the generator does for the records (comma-split class names are already one point
    // each, overload-wide points hash "*", the version range goes in as its range key).
    private static void AddPoint(string file, XmlInstrumentationPoint point, HashSet<ulong> recordKeys, HashSet<ulong> enabledKeys, List<DistroXmlUnsupportedPoint> unsupported)
    {
        // Disabled points turn nothing on. A point the generator emits no record for (blank or padded class name, more
        // parameters than OTel takes, empty version range) is not "unsupported": the generator already reported it at
        // build time. A point with no className attribute has a null class name; the generator emits it as it is, so it
        // is hashed as it is.
        if (!point.Enabled || XmlPointEligibility.Check(point, out var range) != XmlPointEligibilityResult.Emittable)
        {
            return;
        }

        var key = PointKeyHasher.Compute(point, point.ClassName, range.RangeKey);
        if (recordKeys.Contains(key))
        {
            enabledKeys.Add(key);
            return;
        }

        var parameters = point.ParameterTypes == null ? "*" : string.Join(",", point.ParameterTypes);
        unsupported.Add(new DistroXmlUnsupportedPoint(file, point.ClassName, point.MethodName, parameters, point.TracerFactoryName, key));
    }
}
