// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Test-side helpers for NRIDT tables: one hand-written golden table, a small builder for variations, helpers that
/// corrupt a copy of a table, and a dump that renders a table as readable lines for assertion messages.
/// The layout is spelled out again here on purpose. The golden bytes (not the production constants) pin the format,
/// so a change to the production layout fails a test instead of silently changing both sides.
/// </summary>
public static class InstrumentationDefinitionsTableTestData
{
    // Test-side copy of the header field positions (see GoldenTwoAssemblies).
    public const int HeaderStringCount = 6;
    public const int HeaderStringsOffset = 10;
    public const int HeaderSignatureCount = 14;
    public const int HeaderSignaturesOffset = 18;
    public const int HeaderRecordCount = 22;
    public const int HeaderRecordsOffset = 26;
    public const int HeaderAssemblyCount = 30;
    public const int HeaderAssembliesOffset = 34;
    public const int HeaderTotalLength = 38;
    public const int HeaderFileCount = 42;
    public const int HeaderFilesOffset = 46;
    public const int HeaderSize = 50;
    public const int RecordSize = 68;
    public const int RecordPointKey = 56;
    public const int RecordSourceFile = 64;
    public const int RecordFlags = 66;
    public const uint NoString = 0xFFFFFFFF;
    public const ushort PluginSourceFile = 0xFFFF;
    public const int AssemblyEntrySize = 12;
    public const int FileEntrySize = 13;
    public const int FileEntryContentHash = 4;
    public const int FileEntryFlags = 12;

    /// <summary>
    /// Hand-written, never produced by the builder. 2 assemblies, 3 records, 3 shipped files:
    ///   AsmA: record 0 and 1 share type, signature [_, System.String] and tracer factory "TracerA"; record 1 has
    ///         another method and integration type. Record 0 has no metric (NoString), record 1 an empty metric and an
    ///         empty argument signature, tracer arguments 7 and function id 0xFFFFFFFFFFFFFFFF (every high bit set).
    ///         Record 0: point key 0x1112131415161718 from file 0. Record 1: point key 0x8000000000000001 from the
    ///         plugin (source file 0xFFFF).
    ///   AsmB: record 2, signature [_, System.Int32, System.String], version range 1.2.3 - 4.5.6, metric "Custom/B",
    ///         point key 2 from file 1, default-off.
    ///   Files: 0 "A.xml" hash 0xA1A2A3A4A5A6A7A8 compiled, 1 "B.xml" hash 0xFFFFFFFFFFFFFFFF compiled, 2 "C.xml" hash 3
    ///         hash-only.
    /// 21 distinct strings, 2 distinct signatures, 533 bytes in total.
    /// Copy before changing; the helpers below always return copies.
    /// </summary>
    public static readonly byte[] GoldenTwoAssemblies =
    {
        // ---- Header (50 bytes): magic, version, then count + offset for each section, total length, then files
        0x4E, 0x52, 0x49, 0x44, 0x54, // "NRIDT"
        0x03, // format version 3
        0x15, 0x00, 0x00, 0x00, // string count = 21
        0x32, 0x00, 0x00, 0x00, // strings offset = 50
        0x02, 0x00, 0x00, 0x00, // signature count = 2
        0xF4, 0x00, 0x00, 0x00, // signatures offset = 244
        0x03, 0x00, 0x00, 0x00, // record count = 3
        0x0A, 0x01, 0x00, 0x00, // records offset = 266
        0x02, 0x00, 0x00, 0x00, // assembly count = 2
        0xD6, 0x01, 0x00, 0x00, // assemblies offset = 470
        0x15, 0x02, 0x00, 0x00, // total length = 533
        0x03, 0x00, 0x00, 0x00, // file count = 3
        0xEE, 0x01, 0x00, 0x00, // files offset = 494

        // ---- Strings (194 bytes): u16 byte length + UTF-8, in order of first use (records, then file home names)
        0x04, 0x00, 0x41, 0x73, 0x6D, 0x41, // 0 "AsmA"
        0x08, 0x00, 0x4E, 0x53, 0x2E, 0x54, 0x79, 0x70, 0x65, 0x41, // 1 "NS.TypeA"
        0x07, 0x00, 0x4D, 0x65, 0x74, 0x68, 0x6F, 0x64, 0x41, // 2 "MethodA"
        0x01, 0x00, 0x5F, // 3 "_"
        0x0D, 0x00, 0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, // 4 "System.String"
        0x06, 0x00, 0x49, 0x6E, 0x74, 0x65, 0x67, 0x31, // 5 "Integ1"
        0x07, 0x00, 0x54, 0x72, 0x61, 0x63, 0x65, 0x72, 0x41, // 6 "TracerA"
        0x07, 0x00, 0x4D, 0x65, 0x74, 0x68, 0x6F, 0x64, 0x42, // 7 "MethodB"
        0x06, 0x00, 0x49, 0x6E, 0x74, 0x65, 0x67, 0x32, // 8 "Integ2"
        0x00, 0x00, // 9 ""
        0x04, 0x00, 0x41, 0x73, 0x6D, 0x42, // 10 "AsmB"
        0x08, 0x00, 0x4E, 0x53, 0x2E, 0x54, 0x79, 0x70, 0x65, 0x42, // 11 "NS.TypeB"
        0x07, 0x00, 0x4D, 0x65, 0x74, 0x68, 0x6F, 0x64, 0x43, // 12 "MethodC"
        0x0C, 0x00, 0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2E, 0x49, 0x6E, 0x74, 0x33, 0x32, // 13 "System.Int32"
        0x06, 0x00, 0x49, 0x6E, 0x74, 0x65, 0x67, 0x33, // 14 "Integ3"
        0x07, 0x00, 0x54, 0x72, 0x61, 0x63, 0x65, 0x72, 0x42, // 15 "TracerB"
        0x08, 0x00, 0x43, 0x75, 0x73, 0x74, 0x6F, 0x6D, 0x2F, 0x42, // 16 "Custom/B"
        0x1A, 0x00, 0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2E, 0x49, 0x6E, 0x74, 0x33, 0x32, 0x2C,
        0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, // 17 "System.Int32,System.String"
        0x05, 0x00, 0x41, 0x2E, 0x78, 0x6D, 0x6C, // 18 "A.xml"
        0x05, 0x00, 0x42, 0x2E, 0x78, 0x6D, 0x6C, // 19 "B.xml"
        0x05, 0x00, 0x43, 0x2E, 0x78, 0x6D, 0x6C, // 20 "C.xml"

        // ---- Signatures (22 bytes): u8 count + u32 string indices
        0x02, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, // 0: [3 "_", 4 "System.String"]
        0x03, 0x03, 0x00, 0x00, 0x00, 0x0D, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, // 1: [3 "_", 13 "System.Int32", 4 "System.String"]

        // ---- Records (3 x 68 bytes): assembly, type, method, integration type, signature (u32 each), six u16 versions,
        //      then tracer factory, metric name, argument signature, tracer arguments (u32 each), function id (u64),
        //      then point key (u64), source file (u16), flags (u16)
        0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0: AsmA, NS.TypeA, MethodA, Integ1, sig 0, 0.0.0 - 65535.65535.65535
        0x06, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, //    TracerA, no metric, "System.String", 0, 0x0102030405060708
        0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11, 0x00, 0x00, 0x00, 0x00, //    point key 0x1112131415161718, file 0, flags 0
        0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 1: AsmA, NS.TypeA, MethodB, Integ2, sig 0, 0.0.0 - 65535.65535.65535
        0x06, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //    TracerA, metric "", "", 7, 0xFFFFFFFFFFFFFFFF
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0xFF, 0xFF, 0x00, 0x00, //    point key 0x8000000000000001, plugin, flags 0
        0x0A, 0x00, 0x00, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x0E, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04, 0x00, 0x05, 0x00, 0x06, 0x00, // 2: AsmB, NS.TypeB, MethodC, Integ3, sig 1, 1.2.3 - 4.5.6
        0x0F, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //    TracerB, "Custom/B", "System.Int32,System.String", 0, 1
        0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, //    point key 2, file 1, flags 1 (default-off)

        // ---- Assemblies (2 x 12 bytes): assembly string index, first record, record count
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, // AsmA: records 0..1
        0x0A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, // AsmB: record 2

        // ---- Files (3 x 13 bytes): home name string index (u32), content hash (u64), flags (u8)
        0x12, 0x00, 0x00, 0x00, 0xA8, 0xA7, 0xA6, 0xA5, 0xA4, 0xA3, 0xA2, 0xA1, 0x01, // 0: "A.xml", 0xA1A2A3A4A5A6A7A8, compiled
        0x13, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, // 1: "B.xml", 0xFFFFFFFFFFFFFFFF, compiled
        0x14, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 2: "C.xml", 3, hash-only
    };

    /// <summary>One record to build; versions default to the full range like the generated definitions.</summary>
    public sealed class Row
    {
        public string Assembly { get; set; } = "Asm";
        public string Type { get; set; } = "Type";
        public string Method { get; set; } = "Method";
        public string[] Signature { get; set; } = { "_" };
        public string IntegrationType { get; set; } = "Integration";
        public ushort[] Versions { get; set; } = { 0, 0, 0, 65535, 65535, 65535 };
        public string TracerFactory { get; set; } = "Tracer";

        /// <summary>Null is written as NoString; "" is written as a string index.</summary>
        public string MetricName { get; set; }

        public string ArgumentSignature { get; set; } = "";
        public uint TracerArguments { get; set; }
        public ulong FunctionId { get; set; }
        public ulong PointKey { get; set; }

        /// <summary>Index into the files list; <see cref="PluginSourceFile"/> (the default) for a plugin point.</summary>
        public ushort SourceFile { get; set; } = PluginSourceFile;

        /// <summary>Written as flag bit 0.</summary>
        public bool DefaultOff { get; set; }

        /// <summary>Written as flag bit 1 (FlagLogForwarding).</summary>
        public bool LogForwarding { get; set; }
    }

    /// <summary>One shipped XML file to list in the files section.</summary>
    public sealed class ShippedFile
    {
        public string HomeName { get; set; } = "File.xml";
        public ulong ContentHash { get; set; }

        /// <summary>Written as file flag bit 0; false = hash-only.</summary>
        public bool Compiled { get; set; } = true;
    }

    /// <summary>The rows of <see cref="GoldenTwoAssemblies"/>.</summary>
    public static Row[] GoldenRows() => new[]
    {
        new Row
        {
            Assembly = "AsmA", Type = "NS.TypeA", Method = "MethodA", Signature = new[] { "_", "System.String" }, IntegrationType = "Integ1",
            TracerFactory = "TracerA", MetricName = null, ArgumentSignature = "System.String", TracerArguments = 0, FunctionId = 0x0102030405060708,
            PointKey = 0x1112131415161718, SourceFile = 0,
        },
        new Row
        {
            Assembly = "AsmA", Type = "NS.TypeA", Method = "MethodB", Signature = new[] { "_", "System.String" }, IntegrationType = "Integ2",
            TracerFactory = "TracerA", MetricName = "", ArgumentSignature = "", TracerArguments = 7, FunctionId = 0xFFFFFFFFFFFFFFFF,
            PointKey = 0x8000000000000001, SourceFile = PluginSourceFile,
        },
        new Row
        {
            Assembly = "AsmB", Type = "NS.TypeB", Method = "MethodC", Signature = new[] { "_", "System.Int32", "System.String" }, IntegrationType = "Integ3", Versions = new ushort[] { 1, 2, 3, 4, 5, 6 },
            TracerFactory = "TracerB", MetricName = "Custom/B", ArgumentSignature = "System.Int32,System.String", TracerArguments = 0, FunctionId = 1,
            PointKey = 2, SourceFile = 1, DefaultOff = true,
        },
    };

    /// <summary>The files of <see cref="GoldenTwoAssemblies"/>.</summary>
    public static ShippedFile[] GoldenFiles() => new[]
    {
        new ShippedFile { HomeName = "A.xml", ContentHash = 0xA1A2A3A4A5A6A7A8, Compiled = true },
        new ShippedFile { HomeName = "B.xml", ContentHash = 0xFFFFFFFFFFFFFFFF, Compiled = true },
        new ShippedFile { HomeName = "C.xml", ContentHash = 3, Compiled = false },
    };

    /// <summary>A table with no files (every row should then keep the default plugin source file).</summary>
    public static byte[] Build(params Row[] rows) => Build(new ShippedFile[0], rows);

    /// <summary>
    /// Builds a table the way the format describes: records stable-sorted by assembly (ordinal), distinct strings and
    /// signatures in order of first use, file home names after every record string. Rows and files may hold values the
    /// reader must reject (e.g. empty strings, a source file out of range, duplicate home names).
    /// </summary>
    public static byte[] Build(ShippedFile[] files, params Row[] rows)
    {
        var sorted = rows.OrderBy(r => r.Assembly, StringComparer.Ordinal).ToList();

        var strings = new List<string>();
        var stringIndexes = new Dictionary<string, uint>();
        var signatures = new List<string[]>();
        var signatureIndexes = new Dictionary<string, uint>();

        uint StringIndex(string value)
        {
            if (!stringIndexes.TryGetValue(value, out var index))
            {
                index = (uint)strings.Count;
                strings.Add(value);
                stringIndexes[value] = index;
            }

            return index;
        }

        uint SignatureIndex(string[] value)
        {
            var key = string.Join("\u0001", value);
            if (!signatureIndexes.TryGetValue(key, out var index))
            {
                index = (uint)signatures.Count;
                signatures.Add(value);
                signatureIndexes[key] = index;
            }

            return index;
        }

        var records = new MemoryStream();
        var recordWriter = new BinaryWriter(records);
        var assemblies = new List<uint[]>(); // [assembly string, first record, record count]
        foreach (var row in sorted)
        {
            var assembly = StringIndex(row.Assembly);
            var type = StringIndex(row.Type);
            var method = StringIndex(row.Method);
            foreach (var parameterType in row.Signature)
            {
                StringIndex(parameterType);
            }

            var signature = SignatureIndex(row.Signature);
            var integrationType = StringIndex(row.IntegrationType);
            var tracerFactory = StringIndex(row.TracerFactory);
            var metricName = row.MetricName == null ? NoString : StringIndex(row.MetricName);
            var argumentSignature = StringIndex(row.ArgumentSignature);

            recordWriter.Write(assembly);
            recordWriter.Write(type);
            recordWriter.Write(method);
            recordWriter.Write(integrationType);
            recordWriter.Write(signature);
            foreach (var version in row.Versions)
            {
                recordWriter.Write(version);
            }

            recordWriter.Write(tracerFactory);
            recordWriter.Write(metricName);
            recordWriter.Write(argumentSignature);
            recordWriter.Write(row.TracerArguments);
            recordWriter.Write(row.FunctionId);
            recordWriter.Write(row.PointKey);
            recordWriter.Write(row.SourceFile);
            recordWriter.Write((ushort)((row.DefaultOff ? 1 : 0) | (row.LogForwarding ? 2 : 0)));

            if (assemblies.Count > 0 && assemblies[assemblies.Count - 1][0] == assembly)
            {
                assemblies[assemblies.Count - 1][2]++;
            }
            else
            {
                assemblies.Add(new[] { assembly, (uint)(records.Length / RecordSize) - 1, 1u });
            }
        }

        var fileBytes = new MemoryStream();
        var fileWriter = new BinaryWriter(fileBytes);
        foreach (var file in files)
        {
            fileWriter.Write(StringIndex(file.HomeName));
            fileWriter.Write(file.ContentHash);
            fileWriter.Write((byte)(file.Compiled ? 1 : 0));
        }

        var stringBytes = new MemoryStream();
        var stringWriter = new BinaryWriter(stringBytes);
        foreach (var value in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            stringWriter.Write((ushort)bytes.Length);
            stringWriter.Write(bytes);
        }

        var signatureBytes = new MemoryStream();
        var signatureWriter = new BinaryWriter(signatureBytes);
        foreach (var signature in signatures)
        {
            signatureWriter.Write((byte)signature.Length);
            foreach (var value in signature)
            {
                signatureWriter.Write(stringIndexes[value]);
            }
        }

        var assemblyBytes = new MemoryStream();
        var assemblyWriter = new BinaryWriter(assemblyBytes);
        foreach (var entry in assemblies)
        {
            assemblyWriter.Write(entry[0]);
            assemblyWriter.Write(entry[1]);
            assemblyWriter.Write(entry[2]);
        }

        var stringsOffset = (uint)HeaderSize;
        var signaturesOffset = stringsOffset + (uint)stringBytes.Length;
        var recordsOffset = signaturesOffset + (uint)signatureBytes.Length;
        var assembliesOffset = recordsOffset + (uint)records.Length;
        var filesOffset = assembliesOffset + (uint)assemblyBytes.Length;
        var totalLength = filesOffset + (uint)fileBytes.Length;

        var table = new MemoryStream();
        var writer = new BinaryWriter(table);
        writer.Write(Encoding.ASCII.GetBytes("NRIDT"));
        writer.Write((byte)3);
        writer.Write((uint)strings.Count);
        writer.Write(stringsOffset);
        writer.Write((uint)signatures.Count);
        writer.Write(signaturesOffset);
        writer.Write((uint)sorted.Count);
        writer.Write(recordsOffset);
        writer.Write((uint)assemblies.Count);
        writer.Write(assembliesOffset);
        writer.Write(totalLength);
        writer.Write((uint)files.Length);
        writer.Write(filesOffset);
        writer.Write(stringBytes.ToArray());
        writer.Write(signatureBytes.ToArray());
        writer.Write(records.ToArray());
        writer.Write(assemblyBytes.ToArray());
        writer.Write(fileBytes.ToArray());
        return table.ToArray();
    }

    public static uint ReadU32(byte[] table, int position) => BitConverter.ToUInt32(table, position);

    /// <summary>Copy of the table with a u32 overwritten (little-endian).</summary>
    public static byte[] WithU32(byte[] table, int position, uint value)
    {
        var copy = (byte[])table.Clone();
        Array.Copy(BitConverter.GetBytes(value), 0, copy, position, 4);
        return copy;
    }

    /// <summary>Copy of the table with one byte overwritten.</summary>
    public static byte[] WithByte(byte[] table, int position, byte value)
    {
        var copy = (byte[])table.Clone();
        copy[position] = value;
        return copy;
    }

    /// <summary>
    /// Copy cut to <paramref name="length"/> bytes with the header total length patched to match, so the cut is found
    /// by the section checks and not just by the total length check.
    /// </summary>
    public static byte[] Truncated(byte[] table, int length)
    {
        var copy = new byte[length];
        Array.Copy(table, copy, length);
        return WithU32(copy, HeaderTotalLength, (uint)length);
    }

    /// <summary>Position of record <paramref name="record"/> in a table, from its header.</summary>
    public static int RecordPosition(byte[] table, int record) => (int)ReadU32(table, HeaderRecordsOffset) + record * RecordSize;

    /// <summary>Position of assembly entry <paramref name="entry"/> in a table, from its header.</summary>
    public static int AssemblyEntryPosition(byte[] table, int entry) => (int)ReadU32(table, HeaderAssembliesOffset) + entry * AssemblyEntrySize;

    /// <summary>Position of file entry <paramref name="entry"/> in a table, from its header.</summary>
    public static int FileEntryPosition(byte[] table, int entry) => (int)ReadU32(table, HeaderFilesOffset) + entry * FileEntrySize;

    /// <summary>Copy of the table with a u16 overwritten (little-endian).</summary>
    public static byte[] WithU16(byte[] table, int position, ushort value)
    {
        var copy = (byte[])table.Clone();
        Array.Copy(BitConverter.GetBytes(value), 0, copy, position, 2);
        return copy;
    }

    /// <summary>Position of signature entry 0 (its count byte) in a table, from its header.</summary>
    public static int SignaturesPosition(byte[] table) => (int)ReadU32(table, HeaderSignaturesOffset);

    /// <summary>Position of the first string entry (its length prefix) in a table, from its header.</summary>
    public static int StringsPosition(byte[] table) => (int)ReadU32(table, HeaderStringsOffset);

    /// <summary>
    /// Renders a table as readable lines for assertion messages. Independent of the production reader, and tolerant:
    /// a defective table dumps as far as it can be parsed.
    /// </summary>
    public static string Dump(byte[] table)
    {
        var lines = new List<string>();
        try
        {
            var strings = new List<string>();
            lines.Add($"header: strings={ReadU32(table, HeaderStringCount)}@{ReadU32(table, HeaderStringsOffset)} "
                + $"signatures={ReadU32(table, HeaderSignatureCount)}@{ReadU32(table, HeaderSignaturesOffset)} "
                + $"records={ReadU32(table, HeaderRecordCount)}@{ReadU32(table, HeaderRecordsOffset)} "
                + $"assemblies={ReadU32(table, HeaderAssemblyCount)}@{ReadU32(table, HeaderAssembliesOffset)} "
                + $"files={ReadU32(table, HeaderFileCount)}@{ReadU32(table, HeaderFilesOffset)} "
                + $"total={ReadU32(table, HeaderTotalLength)} actual={table.Length}");

            var position = (int)ReadU32(table, HeaderStringsOffset);
            for (var i = 0; i < ReadU32(table, HeaderStringCount); i++)
            {
                var length = BitConverter.ToUInt16(table, position);
                strings.Add(Encoding.UTF8.GetString(table, position + 2, length));
                lines.Add($"string {i}: \"{strings[i]}\"");
                position += 2 + length;
            }

            position = (int)ReadU32(table, HeaderSignaturesOffset);
            for (var i = 0; i < ReadU32(table, HeaderSignatureCount); i++)
            {
                var count = table[position++];
                var indexes = new List<uint>();
                for (var e = 0; e < count; e++)
                {
                    indexes.Add(ReadU32(table, position));
                    position += 4;
                }

                lines.Add($"signature {i}: [{string.Join(",", indexes)}]");
            }

            for (var i = 0; i < ReadU32(table, HeaderRecordCount); i++)
            {
                var at = RecordPosition(table, i);
                var versions = Enumerable.Range(0, 6).Select(v => BitConverter.ToUInt16(table, at + 20 + v * 2)).ToArray();
                lines.Add($"record {i}: assembly={ReadU32(table, at)} type={ReadU32(table, at + 4)} method={ReadU32(table, at + 8)} "
                    + $"integration={ReadU32(table, at + 12)} signature={ReadU32(table, at + 16)} "
                    + $"versions={versions[0]}.{versions[1]}.{versions[2]}-{versions[3]}.{versions[4]}.{versions[5]} "
                    + $"tracer={ReadU32(table, at + 32)} metric={ReadU32(table, at + 36)} arguments={ReadU32(table, at + 40)} "
                    + $"tracerArguments={ReadU32(table, at + 44)} functionId=0x{BitConverter.ToUInt64(table, at + 48):X16} "
                    + $"pointKey=0x{BitConverter.ToUInt64(table, at + RecordPointKey):X16} sourceFile={BitConverter.ToUInt16(table, at + RecordSourceFile)} "
                    + $"flags={BitConverter.ToUInt16(table, at + RecordFlags)}");
            }

            for (var i = 0; i < ReadU32(table, HeaderAssemblyCount); i++)
            {
                var at = AssemblyEntryPosition(table, i);
                lines.Add($"assembly {i}: string={ReadU32(table, at)} firstRecord={ReadU32(table, at + 4)} recordCount={ReadU32(table, at + 8)}");
            }

            for (var i = 0; i < ReadU32(table, HeaderFileCount); i++)
            {
                var at = FileEntryPosition(table, i);
                lines.Add($"file {i}: string={ReadU32(table, at)} hash=0x{BitConverter.ToUInt64(table, at + FileEntryContentHash):X16} flags={table[at + FileEntryFlags]}");
            }
        }
        catch (Exception ex)
        {
            lines.Add("(dump stopped: " + ex.GetType().Name + ")");
        }

        return string.Join(System.Environment.NewLine, lines);
    }
}
