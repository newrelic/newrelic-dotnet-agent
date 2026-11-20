// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableFormat;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// Writes the "NRIDT" instrumentation definitions table that the generated plugin code embeds as a byte[] and Core's
/// InstrumentationDefinitionsTable reads at start-up. The layout, the validity rules and the reasons for both are in
/// InstrumentationDefinitionsTableFormat.cs (Core's file, linked into this project); this writer only follows it.
///
/// Deterministic: the same entries in the same order always give the same bytes. Distinct strings and signatures are
/// numbered in order of first use (lists, never dictionary iteration order), and the sort by assembly is stable.
/// </summary>
public static class InstrumentationDefinitionsTableWriter
{
    /// <summary>
    /// Writes <paramref name="entries"/> and the shipped XML <paramref name="files"/> (in that order in the files
    /// section; an entry's SourceFile is an index into it) as a table. <paramref name="recordIndexOfEntry"/>[i] is the
    /// record index (and so the site index) of entries[i] in the written table, which differs from i when the sort by
    /// assembly moves it. Returns false, with a null <paramref name="table"/> and <paramref name="recordIndexOfEntry"/>
    /// and a <paramref name="failureReason"/> naming the entry or file and the rule, when something cannot be written: null
    /// entries or files, an empty required string, a null argument signature, an empty signature, a null file, an empty
    /// or duplicate (ignoring case) home name, a source file that is not 0xFFFF and not a file index, or a value over one
    /// of the format's limits.
    /// </summary>
    public static bool TryWrite(IReadOnlyList<TableEntry> entries, IReadOnlyList<ShippedXmlFile> files, out byte[] table, out int[] recordIndexOfEntry, out string failureReason)
    {
        table = null;
        recordIndexOfEntry = null;
        if (entries == null || files == null)
        {
            failureReason = "entries or files is null";
            return false;
        }

        failureReason = FindFileDefect(files) ?? FindDefect(entries, files.Count);
        if (failureReason != null)
        {
            return false;
        }

        // Records grouped by assembly (ordinal). OrderBy is stable, so each assembly keeps its input order. Sorting the
        // entry positions (not the entries) gives the record-to-entry map; inverting it gives each entry's record index.
        var entryOfRecord = Enumerable.Range(0, entries.Count).OrderBy(i => entries[i].Assembly, StringComparer.Ordinal).ToList();
        var sorted = entryOfRecord.Select(i => entries[i]).ToList();
        var recordOfEntry = new int[entries.Count];
        for (var record = 0; record < entryOfRecord.Count; record++)
        {
            recordOfEntry[entryOfRecord[record]] = record;
        }

        // Number every distinct string and signature in order of first use, visiting each record's fields in the
        // order the format specifies (assembly, type, method, signature strings, integration type, tracer factory, metric
        // name unless null, argument signature), then each file's home name.
        var strings = new List<string>();
        var stringIndexes = new Dictionary<string, uint>(StringComparer.Ordinal);
        var signatures = new List<uint[]>();
        var signatureIndexes = new Dictionary<string, uint>(StringComparer.Ordinal);
        var records = new List<Record>();

        uint StringIndex(string value)
        {
            if (!stringIndexes.TryGetValue(value, out var index))
            {
                index = (uint)strings.Count;
                strings.Add(value);
                stringIndexes.Add(value, index);
            }

            return index;
        }

        uint SignatureIndex(uint[] elements)
        {
            var key = string.Join(",", elements);
            if (!signatureIndexes.TryGetValue(key, out var index))
            {
                index = (uint)signatures.Count;
                signatures.Add(elements);
                signatureIndexes.Add(key, index);
            }

            return index;
        }

        foreach (var entry in sorted)
        {
            var assembly = StringIndex(entry.Assembly);
            var type = StringIndex(entry.Type);
            var method = StringIndex(entry.Method);
            var signature = SignatureIndex(entry.Signature.Select(StringIndex).ToArray());
            var integrationType = StringIndex(entry.IntegrationType);
            var tracerFactory = StringIndex(entry.TracerFactoryName);
            var metricName = entry.MetricName == null ? NoString : StringIndex(entry.MetricName);
            var argumentSignature = StringIndex(entry.ArgumentSignature);
            records.Add(new Record(entry, assembly, type, method, integrationType, signature, tracerFactory, metricName, argumentSignature));
        }

        var fileNames = files.Select(file => StringIndex(file.HomeName)).ToList();

        // One assembly entry per run of records with the same assembly.
        var assemblies = new List<AssemblyRun>();
        for (var i = 0; i < records.Count; i++)
        {
            if (assemblies.Count == 0 || assemblies[assemblies.Count - 1].AssemblyString != records[i].Assembly)
            {
                assemblies.Add(new AssemblyRun(records[i].Assembly, (uint)i));
            }

            assemblies[assemblies.Count - 1].RecordCount++;
        }

        // Section sizes, then offsets: the sections follow the header back to back.
        var encodedStrings = strings.Select(Encoding.UTF8.GetBytes).ToList();
        var stringsOffset = HeaderSize;
        var signaturesOffset = stringsOffset + encodedStrings.Sum(bytes => StringLengthSize + bytes.Length);
        var recordsOffset = signaturesOffset + signatures.Sum(elements => SignatureCountSize + elements.Length * SignatureElementSize);
        var assembliesOffset = recordsOffset + records.Count * RecordSize;
        var filesOffset = assembliesOffset + assemblies.Count * AssemblyEntrySize;
        var totalLength = filesOffset + files.Count * FileEntrySize;

        var data = new byte[totalLength];

        // Header.
        Encoding.ASCII.GetBytes(MagicText, 0, MagicText.Length, data, 0);
        data[VersionOffset] = FormatVersion;
        WriteU32(data, HeaderStringCount, (uint)strings.Count);
        WriteU32(data, HeaderStringsOffset, (uint)stringsOffset);
        WriteU32(data, HeaderSignatureCount, (uint)signatures.Count);
        WriteU32(data, HeaderSignaturesOffset, (uint)signaturesOffset);
        WriteU32(data, HeaderRecordCount, (uint)records.Count);
        WriteU32(data, HeaderRecordsOffset, (uint)recordsOffset);
        WriteU32(data, HeaderAssemblyCount, (uint)assemblies.Count);
        WriteU32(data, HeaderAssembliesOffset, (uint)assembliesOffset);
        WriteU32(data, HeaderTotalLength, (uint)totalLength);
        WriteU32(data, HeaderFileCount, (uint)files.Count);
        WriteU32(data, HeaderFilesOffset, (uint)filesOffset);

        // Strings.
        var position = stringsOffset;
        foreach (var bytes in encodedStrings)
        {
            WriteU16(data, position, (ushort)bytes.Length);
            position += StringLengthSize;
            Array.Copy(bytes, 0, data, position, bytes.Length);
            position += bytes.Length;
        }

        // Signatures.
        foreach (var elements in signatures)
        {
            data[position] = (byte)elements.Length;
            position += SignatureCountSize;
            foreach (var element in elements)
            {
                WriteU32(data, position, element);
                position += SignatureElementSize;
            }
        }

        // Records.
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var at = recordsOffset + i * RecordSize;
            WriteU32(data, at + RecordAssembly, record.Assembly);
            WriteU32(data, at + RecordType, record.Type);
            WriteU32(data, at + RecordMethod, record.Method);
            WriteU32(data, at + RecordIntegrationType, record.IntegrationType);
            WriteU32(data, at + RecordSignature, record.Signature);
            WriteU16(data, at + RecordMinMajor, record.Entry.MinMajor);
            WriteU16(data, at + RecordMinMinor, record.Entry.MinMinor);
            WriteU16(data, at + RecordMinPatch, record.Entry.MinPatch);
            WriteU16(data, at + RecordMaxMajor, record.Entry.MaxMajor);
            WriteU16(data, at + RecordMaxMinor, record.Entry.MaxMinor);
            WriteU16(data, at + RecordMaxPatch, record.Entry.MaxPatch);
            WriteU32(data, at + RecordTracerFactory, record.TracerFactory);
            WriteU32(data, at + RecordMetricName, record.MetricName);
            WriteU32(data, at + RecordArgumentSignature, record.ArgumentSignature);
            WriteU32(data, at + RecordTracerArguments, record.Entry.TracerArguments);
            WriteU64(data, at + RecordFunctionId, record.Entry.FunctionId);
            WriteU64(data, at + RecordPointKey, record.Entry.PointKey);
            WriteU16(data, at + RecordSourceFile, record.Entry.SourceFile);
            WriteU16(data, at + RecordFlags, (ushort)((record.Entry.DefaultOff ? FlagDefaultOff : 0) | (record.Entry.LogForwarding ? FlagLogForwarding : 0)));
        }

        // Assemblies.
        for (var i = 0; i < assemblies.Count; i++)
        {
            var at = assembliesOffset + i * AssemblyEntrySize;
            WriteU32(data, at + AssemblyEntryString, assemblies[i].AssemblyString);
            WriteU32(data, at + AssemblyEntryFirstRecord, assemblies[i].FirstRecord);
            WriteU32(data, at + AssemblyEntryRecordCount, assemblies[i].RecordCount);
        }

        // Files.
        for (var i = 0; i < files.Count; i++)
        {
            var at = filesOffset + i * FileEntrySize;
            WriteU32(data, at + FileEntryHomeName, fileNames[i]);
            WriteU64(data, at + FileEntryContentHash, files[i].ContentHash);
            data[at + FileEntryFlags] = files[i].Compiled ? FileFlagCompiled : (byte)0;
        }

        table = data;
        recordIndexOfEntry = recordOfEntry;
        return true;
    }

    // The files' share of the validity rules: a home name is required, within the string limit and unique ignoring
    // case (the reader finds a file by the name it has on disk), and there are at most MaxFileCount files.
    private static string FindFileDefect(IReadOnlyList<ShippedXmlFile> files)
    {
        if (files.Count > MaxFileCount)
        {
            return files.Count + " files is over the limit of " + MaxFileCount;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file == null)
            {
                return "null file " + i;
            }

            var defect = RequiredStringDefect(file.HomeName, "home name", "file " + i);
            if (defect != null)
            {
                return defect;
            }

            if (!seen.Add(file.HomeName))
            {
                return "duplicate home name \"" + file.HomeName + "\" in file " + i;
            }
        }

        return null;
    }

    // Everything the format's validity rules and limits forbid, checked before anything is written, so the writing
    // above has no failure paths. Returns null when every entry can be written.
    private static string FindDefect(IReadOnlyList<TableEntry> entries, int fileCount)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var where = "definition " + i + " (" + Shorten(entry.Assembly) + "/" + Shorten(entry.Type) + "." + Shorten(entry.Method) + ")";

            var defect = RequiredStringDefect(entry.Assembly, "assembly", where)
                ?? RequiredStringDefect(entry.Type, "type", where)
                ?? RequiredStringDefect(entry.Method, "method", where)
                ?? RequiredStringDefect(entry.IntegrationType, "integration type", where)
                ?? RequiredStringDefect(entry.TracerFactoryName, "tracer factory", where)
                ?? (entry.MetricName == null ? null : StringLengthDefect(entry.MetricName, "metric name", where))
                ?? (entry.ArgumentSignature == null ? "null argument signature in " + where : StringLengthDefect(entry.ArgumentSignature, "argument signature", where));
            if (defect != null)
            {
                return defect;
            }

            if (entry.SourceFile != PluginSourceFile && entry.SourceFile >= fileCount)
            {
                return "source file " + entry.SourceFile + " out of range (" + fileCount + " files) in " + where;
            }

            if (entry.Signature.Count == 0)
            {
                return "signature has no elements in " + where;
            }

            if (entry.Signature.Count > MaxSignatureElements)
            {
                return "signature has " + entry.Signature.Count + " elements, over the limit of " + MaxSignatureElements + ", in " + where;
            }

            foreach (var element in entry.Signature)
            {
                if (element == null)
                {
                    return "null signature element in " + where;
                }

                defect = StringLengthDefect(element, "signature element", where);
                if (defect != null)
                {
                    return defect;
                }
            }
        }

        return null;
    }

    // Keeps a diagnostic readable when the offending value is the over-long string itself.
    private static string Shorten(string value) => value != null && value.Length > 100 ? value.Substring(0, 100) + "..." : value;

    private static string RequiredStringDefect(string value, string field, string where) =>
        string.IsNullOrEmpty(value) ? "empty " + field + " in " + where : StringLengthDefect(value, field, where);

    private static string StringLengthDefect(string value, string field, string where)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        return byteCount > MaxStringBytes
            ? "the " + field + " of " + byteCount + " UTF-8 bytes is over the limit of " + MaxStringBytes + " in " + where
            : null;
    }

    // Little-endian, spelled out so the byte order never depends on the machine (same as the reader).
    private static void WriteU16(byte[] data, int position, ushort value)
    {
        data[position] = (byte)value;
        data[position + 1] = (byte)(value >> 8);
    }

    private static void WriteU32(byte[] data, int position, uint value)
    {
        data[position] = (byte)value;
        data[position + 1] = (byte)(value >> 8);
        data[position + 2] = (byte)(value >> 16);
        data[position + 3] = (byte)(value >> 24);
    }

    // Low half first, matching the reader.
    private static void WriteU64(byte[] data, int position, ulong value)
    {
        WriteU32(data, position, (uint)value);
        WriteU32(data, position + 4, (uint)(value >> 32));
    }

    // One record's string and signature indices, plus its entry for the version bounds and the site numbers.
    private sealed class Record
    {
        public TableEntry Entry { get; }
        public uint Assembly { get; }
        public uint Type { get; }
        public uint Method { get; }
        public uint IntegrationType { get; }
        public uint Signature { get; }
        public uint TracerFactory { get; }
        public uint MetricName { get; }
        public uint ArgumentSignature { get; }

        public Record(TableEntry entry, uint assembly, uint type, uint method, uint integrationType, uint signature, uint tracerFactory, uint metricName, uint argumentSignature)
        {
            Entry = entry;
            Assembly = assembly;
            Type = type;
            Method = method;
            IntegrationType = integrationType;
            Signature = signature;
            TracerFactory = tracerFactory;
            MetricName = metricName;
            ArgumentSignature = argumentSignature;
        }
    }

    private sealed class AssemblyRun
    {
        public uint AssemblyString { get; }
        public uint FirstRecord { get; }
        public uint RecordCount { get; set; }

        public AssemblyRun(uint assemblyString, uint firstRecord)
        {
            AssemblyString = assemblyString;
            FirstRecord = firstRecord;
        }
    }
}
