// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.OtelDistro;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class InstrumentationDefinitionsTableWriterTests
{
    // Header positions, spelled out on the test side on purpose (the golden bytes pin the format, not the constants).
    private const int HeaderStringCount = 6;
    private const int HeaderSignatureCount = 14;
    private const int HeaderRecordCount = 22;
    private const int HeaderAssemblyCount = 30;
    private const int HeaderRecordsOffset = 26;
    private const int HeaderFileCount = 42;
    private const int RecordSize = 68;
    private const int RecordMetricName = 36;
    private const int RecordArgumentSignature = 40;
    private const int RecordPointKey = 56;
    private const int RecordSourceFile = 64;
    private const int RecordFlags = 66;

    private static readonly ShippedXmlFile[] NoFiles = Array.Empty<ShippedXmlFile>();

    /// <summary>
    /// Copied verbatim from Core.UnitTest's InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies (hand-written,
    /// never produced by a writer). Pinning the same bytes on both sides means a change to either the writer or the
    /// reader fails a test. Its input rows are <see cref="GoldenEntries"/> and <see cref="GoldenFiles"/>.
    /// </summary>
    private static readonly byte[] GoldenTwoAssemblies =
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

    /// <summary>The input rows behind <see cref="GoldenTwoAssemblies"/>.</summary>
    private static TableEntry[] GoldenEntries() => new[]
    {
        new TableEntry("AsmA", "NS.TypeA", "MethodA", new[] { "_", "System.String" }, "Integ1", "TracerA", null, "System.String", 0, 0x0102030405060708, pointKey: 0x1112131415161718, sourceFile: 0),
        new TableEntry("AsmA", "NS.TypeA", "MethodB", new[] { "_", "System.String" }, "Integ2", "TracerA", "", "", 7, 0xFFFFFFFFFFFFFFFF, pointKey: 0x8000000000000001),
        new TableEntry("AsmB", "NS.TypeB", "MethodC", new[] { "_", "System.Int32", "System.String" }, "Integ3", "TracerB", "Custom/B", "System.Int32,System.String", 0, 1, 1, 2, 3, 4, 5, 6, pointKey: 2, sourceFile: 1, flags: TableEntryFlags.DefaultOff),
    };

    /// <summary>The shipped files behind <see cref="GoldenTwoAssemblies"/>.</summary>
    private static ShippedXmlFile[] GoldenFiles() => new[]
    {
        new ShippedXmlFile("A.xml", 0xA1A2A3A4A5A6A7A8, compiled: true),
        new ShippedXmlFile("B.xml", 0xFFFFFFFFFFFFFFFF, compiled: true),
        new ShippedXmlFile("C.xml", 3, compiled: false),
    };

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void TryWrite_NullEntriesOrFiles_FailsWithoutThrowing(bool nullEntries, bool nullFiles)
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(nullEntries ? null : GoldenEntries(), nullFiles ? null : GoldenFiles(), out var table, out var recordIndexOfEntry, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(recordIndexOfEntry, Is.Null);
            Assert.That(reason, Is.EqualTo("entries or files is null"));
        });
    }

    [Test]
    public void Write_GoldenInput_ProducesGoldenBytes()
    {
        var table = WriteOrFail(GoldenEntries(), GoldenFiles());

        Assert.That(table, Is.EqualTo(GoldenTwoAssemblies), () => "Written:\n" + Hex(table));
    }

    [Test]
    public void Write_GoldenInputOutOfAssemblyOrder_StillProducesGoldenBytes()
    {
        var golden = GoldenEntries();

        var table = WriteOrFail(new[] { golden[2], golden[0], golden[1] }, GoldenFiles());

        Assert.That(table, Is.EqualTo(GoldenTwoAssemblies), () => "Written:\n" + Hex(table));
    }

    [Test]
    public void Write_SameInputTwice_IdenticalBytes()
    {
        var entries = new List<TableEntry>();
        for (var i = 0; i < 200; i++)
        {
            entries.Add(new TableEntry("Asm" + (i % 7), "Type" + (i % 13), "Method" + i, new[] { "_", "P" + (i % 5) }, "Integ" + i, "Tracer", null, "", 0, 0));
        }

        var first = WriteOrFail(entries);
        var second = WriteOrFail(entries);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Write_DeduplicatesStringsAndSignatures()
    {
        // Nine arity variants of one overload-wide match: one assembly/type/method, "_" shared by every signature.
        var entries = Enumerable.Range(0, 9)
            .Select(arity => new TableEntry("Asm", "Type", "Method", Enumerable.Repeat("_", arity + 1).ToArray(), "Integ" + arity, "Tracer", null, "", 0, 0))
            .ToList();
        // The same signature again under another method: no new signature, one new string.
        entries.Add(new TableEntry("Asm", "Type", "Other", new[] { "_", "_" }, "Integ0", "Tracer", null, "", 0, 0));

        var table = WriteOrFail(entries);

        Assert.Multiple(() =>
        {
            // Asm, Type, Method, "_", Integ0, Tracer, "" (argument signature), Integ1..Integ8, Other
            Assert.That(ReadU32(table, HeaderStringCount), Is.EqualTo(16));
            Assert.That(ReadU32(table, HeaderSignatureCount), Is.EqualTo(9));
            Assert.That(ReadU32(table, HeaderRecordCount), Is.EqualTo(10));
            Assert.That(ReadU32(table, HeaderAssemblyCount), Is.EqualTo(1));
        });
    }

    [Test]
    public void Write_StableSortsByAssembly_KeepsOrderWithinAssembly()
    {
        var entries = new[]
        {
            Entry("b", "b-first"),
            Entry("B", "B-first"),
            Entry("a", "a-first"),
            Entry("b", "b-second"),
            Entry("B", "B-second"),
            Entry("a", "a-second"),
        };

        var table = WriteOrFail(entries);
        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out var reason), Is.True, reason);

        // Ordinal: "B" (0x42) sorts before "a" (0x61) and "b" (0x62).
        Assert.That(definitions.Select(d => d.IntegrationTypeName), Is.EqualTo(new[] { "B-first", "B-second", "a-first", "a-second", "b-first", "b-second" }));
        Assert.That(ReadU32(table, HeaderAssemblyCount), Is.EqualTo(3));
    }

    [Test]
    public void Write_NoEntries_WritesAnEmptyTable()
    {
        var table = WriteOrFail(Array.Empty<TableEntry>());

        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out var reason), Is.True, reason);
        Assert.That(definitions, Is.Empty);
    }

    [Test]
    public void Write_StringAtLimit_IsWritten()
    {
        var table = WriteOrFail(new[] { new TableEntry("Asm", "Type", new string('m', 65535), new[] { "_" }, "Integ", "Tracer", null, "", 0, 0) });

        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out var reason), Is.True, reason);
        Assert.That(definitions[0].TargetMethod, Has.Length.EqualTo(65535));
    }

    [Test]
    public void Write_StringOverLimit_ReportsLimit()
    {
        // 32768 two-byte UTF-8 characters: 65536 bytes, one over the limit although only 32768 chars long.
        var tooLong = new string((char)0xE9, 32768);

        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry("Asm", "Type", "Method", new[] { "_", tooLong }, "Integ", "Tracer", null, "", 0, 0) }, NoFiles, out var table, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(reason, Does.Contain("65536 UTF-8 bytes").And.Contain("65535").And.Contain("Asm/Type.Method"));
        });
    }

    [Test]
    public void Write_SignatureAtLimit_IsWritten()
    {
        WriteOrFail(new[] { new TableEntry("Asm", "Type", "Method", Enumerable.Repeat("_", 255).ToArray(), "Integ", "Tracer", null, "", 0, 0) });
    }

    [Test]
    public void Write_SignatureOverLimit_ReportsLimit()
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry("Asm", "Type", "Method", Enumerable.Repeat("_", 256).ToArray(), "Integ", "Tracer", null, "", 0, 0) }, NoFiles, out var table, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(reason, Does.Contain("256 elements").And.Contain("255").And.Contain("Asm/Type.Method"));
        });
    }

    [TestCase("", "Type", "Method", "Integ", "assembly")]
    [TestCase(null, "Type", "Method", "Integ", "assembly")]
    [TestCase("Asm", "", "Method", "Integ", "type")]
    [TestCase("Asm", "Type", "", "Integ", "method")]
    [TestCase("Asm", "Type", "Method", "", "integration type")]
    public void Write_EmptyRequiredString_ReportsIt(string assembly, string type, string method, string integrationType, string field)
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry(assembly, type, method, new[] { "_" }, integrationType, "Tracer", null, "", 0, 0) }, NoFiles, out var table, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(reason, Does.StartWith("empty " + field + " in definition 0"));
        });
    }

    [Test]
    public void Write_EmptySignature_ReportsIt()
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry("Asm", "Type", "Method", Array.Empty<string>(), "Integ", "Tracer", null, "", 0, 0) }, NoFiles, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Does.Contain("signature has no elements").And.Contain("Asm/Type.Method"));
        });
    }

    [Test]
    public void Write_NullSignatureElement_ReportsIt()
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry("Asm", "Type", "Method", new[] { "_", null }, "Integ", "Tracer", null, "", 0, 0) }, NoFiles, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Does.Contain("null signature element").And.Contain("Asm/Type.Method"));
        });
    }

    [Test]
    public void TryWrite_EntriesOutOfAssemblyOrder_ReportsRecordIndexOfEntry()
    {
        var entries = new[] { Entry("B", "B1"), Entry("A", "A1"), Entry("B", "B2"), Entry("A", "A2") };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(entries, NoFiles, out var table, out var recordIndexOfEntry, out var reason);

        Assert.That(written, Is.True, reason);
        Assert.That(recordIndexOfEntry, Is.EqualTo(new[] { 2, 0, 3, 1 }));
        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out reason), Is.True, reason);
        for (var i = 0; i < entries.Length; i++)
        {
            Assert.That(definitions[recordIndexOfEntry[i]].IntegrationTypeName, Is.EqualTo(entries[i].IntegrationType), "entry " + i);
        }
    }

    [Test]
    public void TryWrite_NullMetric_WritesNoString()
    {
        var table = WriteOrFail(new[] { new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", null, "", 0, 0) });

        Assert.That(ReadU32(table, RecordPosition(table, 0) + RecordMetricName), Is.EqualTo(0xFFFFFFFFu));
        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var reason), Is.True, reason);
        Assert.That(sites.TryGet(0, out var site), Is.True);
        Assert.That(site.MetricName, Is.Null);
    }

    [Test]
    public void TryWrite_EmptyMetric_WritesStringIndex()
    {
        var table = WriteOrFail(new[] { new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", "", "", 0, 0) });

        // Strings in first-use order: Asm 0, Type 1, Method 2, "_" 3, Integ 4, Tracer 5, "" 6 (metric, then reused by
        // the argument signature).
        Assert.Multiple(() =>
        {
            Assert.That(ReadU32(table, RecordPosition(table, 0) + RecordMetricName), Is.EqualTo(6));
            Assert.That(ReadU32(table, RecordPosition(table, 0) + RecordArgumentSignature), Is.EqualTo(6));
        });
        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var reason), Is.True, reason);
        Assert.That(sites.TryGet(0, out var site), Is.True);
        Assert.That(site.MetricName, Is.Not.Null.And.Empty);
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryWrite_EmptyTracerFactory_ReportsFailure(string tracerFactory)
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(
            new[] { new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", tracerFactory, null, "", 0, 0) },
            NoFiles,
            out var table,
            out var recordIndexOfEntry,
            out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(recordIndexOfEntry, Is.Null);
            Assert.That(reason, Does.StartWith("empty tracer factory in definition 0"));
        });
    }

    [Test]
    public void TryWrite_NullArgumentSignature_ReportsFailure()
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", null, null, 0, 0) }, NoFiles, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Does.StartWith("null argument signature in definition 0"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TryWrite_MetricOrArgumentSignatureOverLimit_ReportsLimit(bool metric)
    {
        var tooLong = new string('x', 65536);
        var entry = new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", metric ? tooLong : null, metric ? "" : tooLong, 0, 0);

        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { entry }, NoFiles, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Does.Contain("65536 UTF-8 bytes").And.Contain(metric ? "metric name" : "argument signature").And.Contain("Asm/Type.Method"));
        });
    }

    [Test]
    public void TryWrite_WritesFilesSection_RoundTripsThroughCoreReader()
    {
        var entries = new[]
        {
            new TableEntry("AsmB", "Type", "FromSql", new[] { "_" }, "IntegB", "Tracer", null, "", 0, 1, pointKey: 0x8000000000000011, sourceFile: 1),
            new TableEntry("AsmA", "Type", "DefaultOff", new[] { "_" }, "IntegA", "Tracer", null, "", 0, 2, pointKey: 0x8000000000000022, sourceFile: 1, flags: TableEntryFlags.DefaultOff),
            new TableEntry("AsmA", "Type", "Plugin", new[] { "_" }, "IntegP", "Tracer", null, "", 0, 3),
        };
        var files = new[]
        {
            new ShippedXmlFile("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", 0x0123456789ABCDEF, compiled: false),
            new ShippedXmlFile("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", 0xFEDCBA9876543210, compiled: true),
        };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(entries, files, out var table, out var recordIndexOfEntry, out var reason);

        Assert.That(written, Is.True, reason);
        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out var manifest, out reason), Is.True, reason);
        var fromSql = recordIndexOfEntry[0];
        var defaultOff = recordIndexOfEntry[1];
        var plugin = recordIndexOfEntry[2];
        Assert.Multiple(() =>
        {
            Assert.That(ReadU32(table, HeaderFileCount), Is.EqualTo(2));
            Assert.That(manifest.FileCount, Is.EqualTo(2));
            Assert.That(manifest.HomeName(0), Is.EqualTo("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(0x0123456789ABCDEFUL));
            Assert.That(manifest.IsCompiled(0), Is.False);
            Assert.That(manifest.HomeName(1), Is.EqualTo("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(1), Is.EqualTo(0xFEDCBA9876543210UL));
            Assert.That(manifest.IsCompiled(1), Is.True);
            Assert.That(manifest.TryFindFile("newrelic.providers.wrapper.sql.instrumentation.xml", out var sqlFile), Is.True);
            Assert.That(sqlFile, Is.EqualTo(1));

            Assert.That(sites.PointKey(fromSql), Is.EqualTo(0x8000000000000011UL));
            Assert.That(sites.SourceFile(fromSql), Is.EqualTo(1));
            Assert.That(sites.IsDefaultOff(fromSql), Is.False);
            Assert.That(sites.IsOn(fromSql), Is.True);
            Assert.That(sites.PointKey(defaultOff), Is.EqualTo(0x8000000000000022UL));
            Assert.That(sites.IsDefaultOff(defaultOff), Is.True);
            Assert.That(sites.IsOn(defaultOff), Is.False);
            Assert.That(sites.PointKey(plugin), Is.EqualTo(0UL));
            Assert.That(sites.SourceFile(plugin), Is.EqualTo(-1));
            Assert.That(sites.IsOn(plugin), Is.True);
        });
    }

    [Test]
    public void TryWrite_LogForwardingAndDefaultOff_WriteTheirFlagBitsAndRoundTrip()
    {
        // Flag bit 0 is default-off, bit 1 is log-forwarding; they are independent and may be set together.
        var entries = new[]
        {
            new TableEntry("Asm", "Type", "LogOn", new[] { "_" }, "IntegA", "Tracer", null, "", 0, 1, flags: TableEntryFlags.LogForwarding),
            new TableEntry("Asm", "Type", "LogOff", new[] { "_" }, "IntegB", "Tracer", null, "", 0, 2, flags: TableEntryFlags.DefaultOff | TableEntryFlags.LogForwarding),
            new TableEntry("Asm", "Type", "Plain", new[] { "_" }, "IntegC", "Tracer", null, "", 0, 3),
        };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(entries, new ShippedXmlFile[0], out var table, out var recordIndexOfEntry, out var reason);

        Assert.That(written, Is.True, reason);
        Assert.Multiple(() =>
        {
            Assert.That(BitConverter.ToUInt16(table, RecordPosition(table, recordIndexOfEntry[0]) + RecordFlags), Is.EqualTo(2), "log-forwarding only");
            Assert.That(BitConverter.ToUInt16(table, RecordPosition(table, recordIndexOfEntry[1]) + RecordFlags), Is.EqualTo(3), "default-off and log-forwarding");
            Assert.That(BitConverter.ToUInt16(table, RecordPosition(table, recordIndexOfEntry[2]) + RecordFlags), Is.EqualTo(0), "neither");
        });

        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out reason), Is.True, reason);
        Assert.Multiple(() =>
        {
            Assert.That(sites.IsLogForwarding(recordIndexOfEntry[0]), Is.True);
            Assert.That(sites.IsLogForwarding(recordIndexOfEntry[1]), Is.True);
            Assert.That(sites.IsDefaultOff(recordIndexOfEntry[1]), Is.True);
            Assert.That(sites.IsLogForwarding(recordIndexOfEntry[2]), Is.False);
        });
    }

    [Test]
    public void TryWrite_DefaultEntryValues_WriteAPluginPointThatIsOn()
    {
        var table = WriteOrFail(new[] { Entry("Asm", "Integ") });

        var record = RecordPosition(table, 0);
        Assert.Multiple(() =>
        {
            Assert.That(BitConverter.ToUInt64(table, record + RecordPointKey), Is.EqualTo(0UL));
            Assert.That(BitConverter.ToUInt16(table, record + RecordSourceFile), Is.EqualTo(0xFFFF));
            Assert.That(BitConverter.ToUInt16(table, record + RecordFlags), Is.EqualTo(0));
            Assert.That(ReadU32(table, HeaderFileCount), Is.EqualTo(0));
        });
    }

    [TestCase("A.xml", "A.xml")]
    [TestCase("A.xml", "a.XML")]
    public void TryWrite_DuplicateHomeName_Fails(string first, string second)
    {
        var files = new[] { new ShippedXmlFile(first, 1, compiled: true), new ShippedXmlFile(second, 2, compiled: false) };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { Entry("Asm", "Integ") }, files, out var table, out var recordIndexOfEntry, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(table, Is.Null);
            Assert.That(recordIndexOfEntry, Is.Null);
            Assert.That(reason, Is.EqualTo("duplicate home name \"" + second + "\" in file 1"));
        });
    }

    [TestCase(2, (ushort)2)]
    [TestCase(0, (ushort)0)]
    [TestCase(2, (ushort)0xFFFE)]
    public void TryWrite_SourceFileOutOfRange_Fails(int fileCount, ushort sourceFile)
    {
        var files = Enumerable.Range(0, fileCount).Select(i => new ShippedXmlFile("F" + i + ".xml", 0, compiled: true)).ToArray();
        var entry = new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", null, "", 0, 0, sourceFile: sourceFile);

        var written = InstrumentationDefinitionsTableWriter.TryWrite(new[] { entry }, files, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Is.EqualTo("source file " + sourceFile + " out of range (" + fileCount + " files) in definition 0 (Asm/Type.Method)"));
        });
    }

    [Test]
    public void TryWrite_SourceFileIsTheLastFile_IsWritten()
    {
        var files = new[] { new ShippedXmlFile("A.xml", 0, compiled: true), new ShippedXmlFile("B.xml", 0, compiled: true) };
        var entry = new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", null, "", 0, 0, sourceFile: 1);

        var table = WriteOrFail(new[] { entry }, files);

        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var reason), Is.True, reason);
        Assert.That(sites.SourceFile(0), Is.EqualTo(1));
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryWrite_EmptyHomeName_Fails(string homeName)
    {
        var files = new[] { new ShippedXmlFile("A.xml", 0, compiled: true), new ShippedXmlFile(homeName, 0, compiled: true) };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(Array.Empty<TableEntry>(), files, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Is.EqualTo("empty home name in file 1"));
        });
    }

    [Test]
    public void TryWrite_NullFile_Fails()
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(Array.Empty<TableEntry>(), new ShippedXmlFile[] { null }, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Is.EqualTo("null file 0"));
        });
    }

    [Test]
    public void TryWrite_HomeNameOverLimit_Fails()
    {
        var files = new[] { new ShippedXmlFile(new string('x', 65536), 0, compiled: true) };

        var written = InstrumentationDefinitionsTableWriter.TryWrite(Array.Empty<TableEntry>(), files, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Does.Contain("home name of 65536 UTF-8 bytes").And.Contain("65535").And.Contain("file 0"));
        });
    }

    [Test]
    public void TryWrite_FilesAtTheLimit_AreWrittenAndRead()
    {
        // 0xFFFF means "the plugin", so 65535 files (indices 0 to 65534) is the most a table can name.
        var files = Enumerable.Range(0, 65535).Select(i => new ShippedXmlFile("F" + i, (ulong)i, compiled: i % 2 == 0)).ToArray();
        var entry = new TableEntry("Asm", "Type", "Method", new[] { "_" }, "Integ", "Tracer", null, "", 0, 0, sourceFile: 65534);

        var table = WriteOrFail(new[] { entry }, files);

        Assert.That(InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out var manifest, out var reason), Is.True, reason);
        Assert.Multiple(() =>
        {
            Assert.That(manifest.FileCount, Is.EqualTo(65535));
            Assert.That(manifest.HomeName(65534), Is.EqualTo("F65534"));
            Assert.That(sites.SourceFile(0), Is.EqualTo(65534));
        });
    }

    [Test]
    public void TryWrite_TooManyFiles_Fails()
    {
        var files = Enumerable.Range(0, 65536).Select(i => new ShippedXmlFile("F" + i, 0, compiled: true)).ToArray();

        var written = InstrumentationDefinitionsTableWriter.TryWrite(Array.Empty<TableEntry>(), files, out _, out _, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False);
            Assert.That(reason, Is.EqualTo("65536 files is over the limit of 65535"));
        });
    }

    [Test]
    public void ShippedXmlFile_Ctor_SetsProperties()
    {
        var file = new ShippedXmlFile("A.xml", 0x8000000000000001, compiled: true);

        Assert.Multiple(() =>
        {
            Assert.That(file.HomeName, Is.EqualTo("A.xml"));
            Assert.That(file.ContentHash, Is.EqualTo(0x8000000000000001UL));
            Assert.That(file.Compiled, Is.True);
        });
    }

    private static TableEntry Entry(string assembly, string integrationType) =>
        new TableEntry(assembly, "Type", "Method", new[] { "_" }, integrationType, "Tracer", null, "", 0, 0);

    private static byte[] WriteOrFail(IReadOnlyList<TableEntry> entries) => WriteOrFail(entries, NoFiles);

    private static byte[] WriteOrFail(IReadOnlyList<TableEntry> entries, IReadOnlyList<ShippedXmlFile> files)
    {
        var written = InstrumentationDefinitionsTableWriter.TryWrite(entries, files, out var table, out _, out var reason);
        Assert.That(written, Is.True, reason);
        return table;
    }

    private static uint ReadU32(byte[] table, int position) => BitConverter.ToUInt32(table, position);

    private static int RecordPosition(byte[] table, int record) => (int)ReadU32(table, HeaderRecordsOffset) + record * RecordSize;

    private static string Hex(byte[] table) =>
        string.Join("\n", table.Select((b, i) => (b, i)).GroupBy(x => x.i / 16).Select(g => string.Join(" ", g.Select(x => x.b.ToString("X2")))));
}
