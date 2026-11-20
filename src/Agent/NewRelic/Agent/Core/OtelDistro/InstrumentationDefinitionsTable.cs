// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableFormat;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Reads the generated "NRIDT" instrumentation definitions table into <see cref="DistroCallTargetDefinition"/>s,
/// their <see cref="DistroInstrumentationSites"/> and the <see cref="DistroShippedXmlManifest"/> of shipped XML files.
/// The layout and the reasons for it are in InstrumentationDefinitionsTableFormat.cs; read that first.
///
/// All or nothing: any defect in the table returns false, an empty list, no sites and no manifest, and nothing is
/// thrown, because
/// this runs during OTel start-up. Every offset, count and index is checked against the buffer before it is used.
/// </summary>
public static class InstrumentationDefinitionsTable
{
    private static readonly DistroCallTargetDefinition[] NoDefinitions = new DistroCallTargetDefinition[0];

    /// <summary>
    /// Decodes <paramref name="data"/>. On success <paramref name="definitions"/> is in record order,
    /// <paramref name="sites"/> holds one site per definition at the same index (each on unless its record is
    /// default-off), <paramref name="manifest"/> lists the table's shipped XML files, and <paramref name="failureReason"/>
    /// is null. On failure <paramref name="definitions"/> is empty (never null), <paramref name="sites"/> and
    /// <paramref name="manifest"/> are null and <paramref name="failureReason"/> is a short sentence naming the defect.
    /// </summary>
    public static bool TryRead(byte[] data, out IReadOnlyList<DistroCallTargetDefinition> definitions, out DistroInstrumentationSites sites, out DistroShippedXmlManifest manifest, out string failureReason)
    {
        try
        {
            definitions = ReadDefinitions(data, out sites, out manifest);
            failureReason = null;
            return true;
        }
        catch (InvalidTableException ex)
        {
            definitions = NoDefinitions;
            sites = null;
            manifest = null;
            failureReason = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            // Last line of defence only: every defect we expect is rejected by an explicit check below.
            definitions = NoDefinitions;
            sites = null;
            manifest = null;
            failureReason = "unexpected " + ex.GetType().Name + " while reading the table";
            return false;
        }
    }

    private static DistroCallTargetDefinition[] ReadDefinitions(byte[] data, out DistroInstrumentationSites sites, out DistroShippedXmlManifest manifest)
    {
        if (data == null)
        {
            throw new InvalidTableException("table is null");
        }

        // Header: magic, version, total length.
        if (!Fits(data, 0, HeaderSize))
        {
            throw PastTheEnd("header");
        }

        for (var i = 0; i < MagicText.Length; i++)
        {
            if (data[i] != MagicText[i])
            {
                throw new InvalidTableException("bad magic, not an NRIDT table");
            }
        }

        if (data[VersionOffset] != FormatVersion)
        {
            throw new InvalidTableException("unsupported format version " + data[VersionOffset]);
        }

        var totalLength = ReadU32(data, HeaderTotalLength);
        if (totalLength != data.Length)
        {
            throw new InvalidTableException("total length " + totalLength + " does not match the buffer length " + data.Length);
        }

        // Sections follow the header back to back, so "position" is where the next section must start.
        var position = HeaderSize;
        var strings = ReadStrings(data, ref position);
        var signatures = ReadSignatures(data, strings, ref position);

        var recordCount = ReadSectionOfFixedSizeEntries(data, "records", HeaderRecordCount, HeaderRecordsOffset, RecordSize, ref position);
        var recordsStart = position - recordCount * RecordSize;
        var assemblyCount = ReadSectionOfFixedSizeEntries(data, "assemblies", HeaderAssemblyCount, HeaderAssembliesOffset, AssemblyEntrySize, ref position);
        var assembliesStart = position - assemblyCount * AssemblyEntrySize;

        // The file count is limited by the record's u16 source file, whose top value means "the plugin".
        var fileCount = ReadU32(data, HeaderFileCount);
        if (fileCount > MaxFileCount)
        {
            throw new InvalidTableException("file count " + fileCount + " is over the limit of " + MaxFileCount);
        }

        ReadSectionOfFixedSizeEntries(data, "files", HeaderFileCount, HeaderFilesOffset, FileEntrySize, ref position);
        var filesStart = position - (int)fileCount * FileEntrySize;
        if (position != data.Length)
        {
            throw new InvalidTableException("files section ends at " + position + " but the table is " + data.Length + " bytes");
        }

        var readManifest = ReadFiles(data, filesStart, (int)fileCount, strings);

        // The assembly index decides which records belong to which assembly; every record is read through it. Site i is
        // record i, filled in the same pass.
        var definitions = new DistroCallTargetDefinition[recordCount];
        var readSites = new DistroInstrumentationSites(recordCount);
        var nextRecord = 0;
        for (var assembly = 0; assembly < assemblyCount; assembly++)
        {
            var entry = assembliesStart + assembly * AssemblyEntrySize;
            var assemblyString = ReadU32(data, entry + AssemblyEntryString);
            var firstRecord = ReadU32(data, entry + AssemblyEntryFirstRecord);
            var runLength = ReadU32(data, entry + AssemblyEntryRecordCount);

            // Checked on the entry itself, not only through its records, so an entry with a run of length 0 is checked too.
            if (assemblyString >= strings.Length)
            {
                throw new InvalidTableException("string index " + assemblyString + " out of range in assembly entry " + assembly);
            }

            if (firstRecord != nextRecord)
            {
                throw new InvalidTableException("assembly entry " + assembly + " starts at record " + firstRecord + ", expected " + nextRecord);
            }

            if (firstRecord + runLength > recordCount)
            {
                throw new InvalidTableException("assembly entry " + assembly + " covers " + runLength + " records from " + firstRecord + " but the table has " + recordCount);
            }

            for (var record = nextRecord; record < nextRecord + runLength; record++)
            {
                definitions[record] = ReadRecord(data, recordsStart + record * RecordSize, record, assemblyString, strings, signatures, (int)fileCount, readSites);
            }

            nextRecord += (int)runLength;
        }

        if (nextRecord != recordCount)
        {
            throw new InvalidTableException("assembly entries cover " + nextRecord + " of " + recordCount + " records");
        }

        sites = readSites;
        manifest = readManifest;
        return definitions;
    }

    // Files: each entry is a u32 home name string index, a u64 content hash and a u8 flags byte. Home names must be
    // non-empty and unique ignoring case, because the manifest finds a file by the name it has on disk.
    private static DistroShippedXmlManifest ReadFiles(byte[] data, int start, int count, string[] strings)
    {
        var homeNames = new string[count];
        var contentHashes = new ulong[count];
        var compiled = new bool[count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var file = 0; file < count; file++)
        {
            var entry = start + file * FileEntrySize;
            var nameIndex = ReadU32(data, entry + FileEntryHomeName);
            if (nameIndex >= strings.Length)
            {
                throw new InvalidTableException("string index " + nameIndex + " out of range in file " + file);
            }

            var homeName = strings[nameIndex];
            if (homeName.Length == 0)
            {
                throw new InvalidTableException("empty home name in file " + file);
            }

            if (!seen.Add(homeName))
            {
                throw new InvalidTableException("duplicate home name \"" + homeName + "\" in file " + file);
            }

            homeNames[file] = homeName;
            contentHashes[file] = ReadU64(data, entry + FileEntryContentHash);
            compiled[file] = (data[entry + FileEntryFlags] & FileFlagCompiled) != 0;
        }

        return new DistroShippedXmlManifest(homeNames, contentHashes, compiled);
    }

    // Strings: each entry is a u16 byte length then UTF-8 bytes. Each distinct string is decoded once here.
    private static string[] ReadStrings(byte[] data, ref int position)
    {
        var count = ReadU32(data, HeaderStringCount);
        ExpectSectionStart(data, "strings", HeaderStringsOffset, position);

        // Every entry is at least its length prefix; this also stops a huge count from allocating a huge array.
        if (!Fits(data, position, count * StringLengthSize))
        {
            throw PastTheEnd("strings section");
        }

        var strings = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (!Fits(data, position, StringLengthSize))
            {
                throw PastTheEnd("string " + i + " length");
            }

            var byteLength = ReadU16(data, position);
            position += StringLengthSize;

            if (!Fits(data, position, byteLength))
            {
                throw PastTheEnd("string " + i);
            }

            strings[i] = Encoding.UTF8.GetString(data, position, byteLength);
            position += byteLength;
        }

        return strings;
    }

    // Signatures: each entry is a u8 element count then that many u32 string indices. Each distinct list is turned into
    // one string[] here, and every record with that signature shares the array.
    private static string[][] ReadSignatures(byte[] data, string[] strings, ref int position)
    {
        var count = ReadU32(data, HeaderSignatureCount);
        ExpectSectionStart(data, "signatures", HeaderSignaturesOffset, position);

        if (!Fits(data, position, count * SignatureCountSize))
        {
            throw PastTheEnd("signatures section");
        }

        var signatures = new string[count][];
        for (var i = 0; i < count; i++)
        {
            if (!Fits(data, position, SignatureCountSize))
            {
                throw PastTheEnd("signature " + i + " count");
            }

            var elementCount = data[position];
            position += SignatureCountSize;

            // The definition constructor requires at least the return type, so an empty list is a defect.
            if (elementCount == 0)
            {
                throw new InvalidTableException("signature " + i + " is empty");
            }

            if (!Fits(data, position, elementCount * SignatureElementSize))
            {
                throw PastTheEnd("signature " + i);
            }

            // Shared across definitions: every record with this signature gets this same array. Never mutate it.
            var elements = new string[elementCount];
            for (var element = 0; element < elementCount; element++)
            {
                var index = ReadU32(data, position);
                if (index >= strings.Length)
                {
                    throw new InvalidTableException("string index " + index + " out of range in signature " + i);
                }

                // An empty element is accepted: the definition constructor only requires the list itself to be non-empty.
                elements[element] = strings[index];
                position += SignatureElementSize;
            }

            signatures[i] = elements;
        }

        return signatures;
    }

    // Records and assemblies are fixed-width, so the section is just its count times the entry size.
    // Returns the entry count (an int, because Fits has proved count * entrySize fits in the buffer).
    private static int ReadSectionOfFixedSizeEntries(byte[] data, string name, int countField, int offsetField, int entrySize, ref int position)
    {
        var count = ReadU32(data, countField);
        ExpectSectionStart(data, name, offsetField, position);

        if (!Fits(data, position, count * entrySize))
        {
            throw PastTheEnd(name + " section");
        }

        position += (int)(count * entrySize);
        return (int)count;
    }

    private static DistroCallTargetDefinition ReadRecord(byte[] data, int start, int record, long assemblyString, string[] strings, string[][] signatures, int fileCount, DistroInstrumentationSites sites)
    {
        var recordAssembly = ReadU32(data, start + RecordAssembly);
        if (recordAssembly != assemblyString)
        {
            throw new InvalidTableException("record " + record + " names assembly string " + recordAssembly + " but its assembly entry names " + assemblyString);
        }

        var assembly = RequiredString(strings, recordAssembly, "assembly", record);
        var type = RequiredString(strings, ReadU32(data, start + RecordType), "type", record);
        var method = RequiredString(strings, ReadU32(data, start + RecordMethod), "method", record);
        var integrationType = RequiredString(strings, ReadU32(data, start + RecordIntegrationType), "integration type", record);

        var signatureIndex = ReadU32(data, start + RecordSignature);
        if (signatureIndex >= signatures.Length)
        {
            throw new InvalidTableException("signature index " + signatureIndex + " out of range in record " + record);
        }

        // Site fields, checked before anything is built.
        var tracerFactory = RequiredString(strings, ReadU32(data, start + RecordTracerFactory), "tracer factory", record);
        var metricIndex = ReadU32(data, start + RecordMetricName);
        var metricName = metricIndex == NoString ? null : StringInRange(strings, metricIndex, "metric name", record);
        var argumentSignature = StringInRange(strings, ReadU32(data, start + RecordArgumentSignature), "argument signature", record);
        var sourceFile = ReadU16(data, start + RecordSourceFile);
        if (sourceFile != PluginSourceFile && sourceFile >= fileCount)
        {
            throw new InvalidTableException("source file " + sourceFile + " out of range in record " + record + " (the table has " + fileCount + " files)");
        }

        var flags = ReadU16(data, start + RecordFlags);

        sites.Set(
            record,
            tracerFactory,
            metricName,
            (uint)ReadU32(data, start + RecordTracerArguments),
            assembly,
            type,
            method,
            argumentSignature,
            ReadU64(data, start + RecordFunctionId),
            ReadU64(data, start + RecordPointKey),
            sourceFile == PluginSourceFile ? -1 : sourceFile,
            (flags & FlagDefaultOff) != 0,
            (flags & FlagLogForwarding) != 0);

        return new DistroCallTargetDefinition(
            assembly,
            type,
            method,
            signatures[signatureIndex],
            integrationType,
            ReadU16(data, start + RecordMinMajor),
            ReadU16(data, start + RecordMinMinor),
            ReadU16(data, start + RecordMinPatch),
            ReadU16(data, start + RecordMaxMajor),
            ReadU16(data, start + RecordMaxMinor),
            ReadU16(data, start + RecordMaxPatch));
    }

    // A record's string field must be in range and, because the definition constructor throws on empty values,
    // non-empty. "field" names the field in the reason ("type", "method", ...).
    private static string RequiredString(string[] strings, long index, string field, int record)
    {
        var value = StringInRange(strings, index, field, record);
        if (value.Length == 0)
        {
            throw new InvalidTableException("empty " + field + " in record " + record);
        }

        return value;
    }

    // A record's string field that may be empty (argument signature, metric name) only has to be in range.
    private static string StringInRange(string[] strings, long index, string field, int record)
    {
        if (index >= strings.Length)
        {
            throw new InvalidTableException("string index " + index + " out of range in " + field + " of record " + record);
        }

        return strings[index];
    }

    // A section must start exactly where the previous one ended. This rejects offsets that point past the end of the
    // table, back into an earlier section, or leave a gap.
    private static void ExpectSectionStart(byte[] data, string name, int offsetField, int expected)
    {
        var declared = ReadU32(data, offsetField);
        if (declared != expected)
        {
            throw new InvalidTableException(name + " section starts at " + declared + ", expected " + expected);
        }
    }

    // Every read of table bytes is preceded by one of these. It is the single bounds check of the reader. Callers
    // build their failure reason (with PastTheEnd) only when it returns false, so a valid table builds no messages.
    private static bool Fits(byte[] data, int position, long size) => size <= data.Length - position;

    private static InvalidTableException PastTheEnd(string what) => new InvalidTableException(what + " runs past the end of the table");

    // Little-endian, spelled out so the byte order is visible and never depends on the machine.
    private static ushort ReadU16(byte[] data, int position) => (ushort)(data[position] | (data[position + 1] << 8));

    // Returns long (not uint) so results mix with int lengths and entry-size multiplications without overflow or
    // uint/int conversion noise.
    private static long ReadU32(byte[] data, int position) =>
        data[position] | ((long)data[position + 1] << 8) | ((long)data[position + 2] << 16) | ((long)data[position + 3] << 24);

    // Low half first (little-endian). Built from two non-negative halves, so the high bit of the u64 needs no sign care.
    private static ulong ReadU64(byte[] data, int position) => (ulong)ReadU32(data, position) | ((ulong)ReadU32(data, position + 4) << 32);

    // Control flow for "this table is defective". Private, so it cannot escape TryRead.
    private sealed class InvalidTableException : Exception
    {
        public InvalidTableException(string reason) : base(reason)
        {
        }
    }
}
