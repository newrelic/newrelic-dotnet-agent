// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class InstrumentationDefinitionsTableTests
{
    // ---- Valid tables ---------------------------------------------------------------------------------------------

    [Test]
    public void TryRead_GoldenTwoAssemblies_DecodesAllRecordsInOrder()
    {
        var ok = InstrumentationDefinitionsTable.TryRead(GoldenTwoAssemblies, out var definitions, out var sites, out _, out var failureReason);

        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(GoldenTwoAssemblies));
        Assert.Multiple(() =>
        {
            Assert.That(failureReason, Is.Null);
            Assert.That(definitions.Count, Is.EqualTo(3));

            Assert.That(definitions[0].TargetAssembly, Is.EqualTo("AsmA"));
            Assert.That(definitions[0].TargetType, Is.EqualTo("NS.TypeA"));
            Assert.That(definitions[0].TargetMethod, Is.EqualTo("MethodA"));
            Assert.That(definitions[0].TargetSignatureTypes, Is.EqualTo(new[] { "_", "System.String" }));
            Assert.That(definitions[0].IntegrationTypeName, Is.EqualTo("Integ1"));
            AssertVersions(definitions[0], 0, 0, 0, 65535, 65535, 65535);

            Assert.That(definitions[1].TargetAssembly, Is.EqualTo("AsmA"));
            Assert.That(definitions[1].TargetType, Is.EqualTo("NS.TypeA"));
            Assert.That(definitions[1].TargetMethod, Is.EqualTo("MethodB"));
            Assert.That(definitions[1].TargetSignatureTypes, Is.EqualTo(new[] { "_", "System.String" }));
            Assert.That(definitions[1].IntegrationTypeName, Is.EqualTo("Integ2"));
            AssertVersions(definitions[1], 0, 0, 0, 65535, 65535, 65535);

            Assert.That(definitions[2].TargetAssembly, Is.EqualTo("AsmB"));
            Assert.That(definitions[2].TargetType, Is.EqualTo("NS.TypeB"));
            Assert.That(definitions[2].TargetMethod, Is.EqualTo("MethodC"));
            Assert.That(definitions[2].TargetSignatureTypes, Is.EqualTo(new[] { "_", "System.Int32", "System.String" }));
            Assert.That(definitions[2].IntegrationTypeName, Is.EqualTo("Integ3"));
            AssertVersions(definitions[2], 1, 2, 3, 4, 5, 6);

            // Dedup: the two AsmA records share decoded strings and the decoded signature array.
            Assert.That(definitions[1].TargetAssembly, Is.SameAs(definitions[0].TargetAssembly));
            Assert.That(definitions[1].TargetType, Is.SameAs(definitions[0].TargetType));
            Assert.That(definitions[1].TargetSignatureTypes, Is.SameAs(definitions[0].TargetSignatureTypes));
            Assert.That(definitions[2].TargetSignatureTypes, Is.Not.SameAs(definitions[0].TargetSignatureTypes));
            Assert.That(definitions[2].TargetSignatureTypes[2], Is.SameAs(definitions[0].TargetSignatureTypes[1]));
        });

        // Sites: one per record, same index.
        Assert.That(sites.Count, Is.EqualTo(3));
        var site0 = GetSite(sites, 0);
        var site1 = GetSite(sites, 1);
        var site2 = GetSite(sites, 2);
        Assert.Multiple(() =>
        {
            AssertSite(site0, "TracerA", null, 0u, "AsmA", "NS.TypeA", "MethodA", "System.String", 0x0102030405060708UL);
            AssertSite(site1, "TracerA", "", 7u, "AsmA", "NS.TypeA", "MethodB", "", 0xFFFFFFFFFFFFFFFFUL);
            AssertSite(site2, "TracerB", "Custom/B", 0u, "AsmB", "NS.TypeB", "MethodC", "System.Int32,System.String", 1UL);

            // Sites share the reader's decoded strings with each other and with the definitions.
            Assert.That(site1.TracerFactoryName, Is.SameAs(site0.TracerFactoryName));
            Assert.That(site0.AssemblyName, Is.SameAs(definitions[0].TargetAssembly));
            Assert.That(site0.ArgumentSignature, Is.SameAs(definitions[0].TargetSignatureTypes[1]));
        });
    }

    [Test]
    public void TryRead_V3Golden_ReadsPointKeySourceFileFlagsAndManifest()
    {
        var ok = InstrumentationDefinitionsTable.TryRead(GoldenTwoAssemblies, out _, out var sites, out var manifest, out var failureReason);

        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(GoldenTwoAssemblies));
        var site0 = GetSite(sites, 0);
        var site1 = GetSite(sites, 1);
        var site2 = GetSite(sites, 2);
        Assert.Multiple(() =>
        {
            Assert.That(sites.PointKey(0), Is.EqualTo(0x1112131415161718UL));
            Assert.That(sites.PointKey(1), Is.EqualTo(0x8000000000000001UL));
            Assert.That(sites.PointKey(2), Is.EqualTo(2UL));
            Assert.That(sites.SourceFile(0), Is.EqualTo(0));
            Assert.That(sites.SourceFile(1), Is.EqualTo(-1), "0xFFFF is the plugin");
            Assert.That(sites.SourceFile(2), Is.EqualTo(1));
            Assert.That(sites.IsDefaultOff(0), Is.False);
            Assert.That(sites.IsDefaultOff(1), Is.False);
            Assert.That(sites.IsDefaultOff(2), Is.True);
            Assert.That(sites.IsOn(0), Is.True);
            Assert.That(sites.IsOn(1), Is.True);
            Assert.That(sites.IsOn(2), Is.False);

            Assert.That(site0.PointKey, Is.EqualTo(0x1112131415161718UL));
            Assert.That(site0.SourceFile, Is.EqualTo(0));
            Assert.That(site0.DefaultOff, Is.False);
            Assert.That(site1.SourceFile, Is.EqualTo(-1));
            Assert.That(site2.PointKey, Is.EqualTo(2UL));
            Assert.That(site2.SourceFile, Is.EqualTo(1));
            Assert.That(site2.DefaultOff, Is.True);

            Assert.That(manifest.FileCount, Is.EqualTo(3));
            Assert.That(manifest.HomeName(0), Is.EqualTo("A.xml"));
            Assert.That(manifest.HomeName(1), Is.EqualTo("B.xml"));
            Assert.That(manifest.HomeName(2), Is.EqualTo("C.xml"));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(0xA1A2A3A4A5A6A7A8UL));
            Assert.That(manifest.ContentHash(1), Is.EqualTo(0xFFFFFFFFFFFFFFFFUL));
            Assert.That(manifest.ContentHash(2), Is.EqualTo(3UL));
            Assert.That(manifest.IsCompiled(0), Is.True);
            Assert.That(manifest.IsCompiled(1), Is.True);
            Assert.That(manifest.IsCompiled(2), Is.False, "hash-only");
        });
    }

    [Test]
    public void TryRead_UnknownFlagBits_AreIgnored()
    {
        // A record's flags: bit 0 default-off, bit 1 log-forwarding; higher bits reserved and ignored. A file's flags:
        // only bit 0 (compiled) has a meaning; the rest are reserved and ignored. 0xFFFE has bit 0 clear and bit 1 set;
        // 0xFFFF has both set.
        var table = WithU16(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, 0) + RecordFlags, 0xFFFE);
        table = WithU16(table, RecordPosition(table, 2) + RecordFlags, 0xFFFF);
        table = WithByte(table, FileEntryPosition(table, 2) + FileEntryFlags, 0xFE);
        table = WithByte(table, FileEntryPosition(table, 0) + FileEntryFlags, 0xFF);

        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out var manifest, out var failureReason);

        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(table));
        Assert.Multiple(() =>
        {
            Assert.That(sites.IsDefaultOff(0), Is.False);
            Assert.That(sites.IsDefaultOff(2), Is.True);
            Assert.That(sites.IsLogForwarding(0), Is.True, "bit 1 set");
            Assert.That(sites.IsLogForwarding(2), Is.True, "bit 1 set");
            Assert.That(manifest.IsCompiled(2), Is.False);
            Assert.That(manifest.IsCompiled(0), Is.True);
        });
    }

    [Test]
    public void TryRead_SourceFileIsTheLastFile_IsAccepted()
    {
        var table = WithU16(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, 0) + RecordSourceFile, 2);

        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var failureReason);

        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(table));
        Assert.That(sites.SourceFile(0), Is.EqualTo(2));
    }

    [Test]
    public void TryRead_FileHomeNameSharesARecordString_IsAccepted()
    {
        // Strings are deduplicated across sections: a home name equal to a record string uses the same index.
        var files = new[] { new ShippedFile { HomeName = "Asm" } };
        var table = Build(files, new Row { SourceFile = 0 });

        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out _, out var manifest, out var failureReason);

        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(table));
        Assert.That(manifest.HomeName(0), Is.EqualTo("Asm"));
    }

    [Test]
    public void TryRead_MetricNoStringAndEmptyMetric_AreDistinct()
    {
        var table = Build(
            new Row { Method = "NoMetric", MetricName = null },
            new Row { Method = "EmptyMetric", MetricName = "" });

        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var failureReason);

        Assert.That(ok, Is.True, failureReason + System.Environment.NewLine + Dump(table));
        Assert.Multiple(() =>
        {
            Assert.That(GetSite(sites, 0).MetricName, Is.Null);
            Assert.That(GetSite(sites, 1).MetricName, Is.Not.Null.And.Empty);
        });
    }

    [Test]
    public void TryRead_EmptyTable_ReturnsTrueAndNoDefinitions()
    {
        var table = Build();

        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out var sites, out var manifest, out var failureReason);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, Dump(table));
            Assert.That(definitions.Count, Is.EqualTo(0));
            Assert.That(sites.Count, Is.EqualTo(0));
            Assert.That(manifest.FileCount, Is.EqualTo(0));
            Assert.That(failureReason, Is.Null);
        });
    }

    [Test]
    public void TryRead_RowsGivenOutOfAssemblyOrder_ReadsThemInTheTableOrder()
    {
        var table = Build(
            new Row { Assembly = "B", IntegrationType = "B1", TracerFactory = "TB1" },
            new Row { Assembly = "A", IntegrationType = "A1", TracerFactory = "TA1" },
            new Row { Assembly = "B", IntegrationType = "B2", TracerFactory = "TB2" },
            new Row { Assembly = "A", IntegrationType = "A2", TracerFactory = "TA2" });

        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out var sites, out _, out var failureReason);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, failureReason + System.Environment.NewLine + Dump(table));
            Assert.That(definitions.Select(d => d.IntegrationTypeName), Is.EqualTo(new[] { "A1", "A2", "B1", "B2" }));
            Assert.That(Enumerable.Range(0, sites.Count).Select(i => GetSite(sites, i).TracerFactoryName), Is.EqualTo(new[] { "TA1", "TA2", "TB1", "TB2" }));
        });
    }

    [Test]
    public void TryRead_NonAsciiStrings_AreDecodedAsUtf8()
    {
        var table = Build(new Row { Type = "Týpe€" });

        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out _);

        Assert.That(ok, Is.True);
        Assert.That(definitions[0].TargetType, Is.EqualTo("Týpe€"));
    }

    [Test]
    public void TryRead_EmptyStringAsSignatureElement_IsAccepted()
    {
        var table = Build(new Row { Signature = new[] { "_", "" } });

        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out var failureReason);

        Assert.That(ok, Is.True, failureReason + System.Environment.NewLine + Dump(table));
        Assert.That(definitions[0].TargetSignatureTypes, Is.EqualTo(new[] { "_", "" }));
    }

    [Test]
    public void TryRead_AssemblyEntryWithARunOfLengthZero_IsAccepted()
    {
        // A third entry naming AsmB (string 10) with no records, after the two golden entries.
        var table = WithExtraAssemblyEntry(GoldenTwoAssemblies, 10, 3, 0);

        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out _, out _, out var failureReason);

        Assert.That(ok, Is.True, failureReason + System.Environment.NewLine + Dump(table));
        Assert.That(definitions.Count, Is.EqualTo(3));
    }

    // ---- Test data helpers are themselves pinned --------------------------------------------------------------------

    [Test]
    public void Build_GoldenRows_ProducesTheGoldenBytes()
    {
        var built = Build(GoldenFiles(), GoldenRows());

        Assert.That(built, Is.EqualTo(GoldenTwoAssemblies), () => Dump(built) + System.Environment.NewLine + "--- expected" + System.Environment.NewLine + Dump(GoldenTwoAssemblies));
    }

    [Test]
    public void Dump_GoldenTwoAssemblies_ListsEverySection()
    {
        var dump = Dump(GoldenTwoAssemblies);

        Assert.Multiple(() =>
        {
            Assert.That(dump, Does.Contain("header: strings=21@50 signatures=2@244 records=3@266 assemblies=2@470 files=3@494 total=533 actual=533"));
            Assert.That(dump, Does.Contain("string 20: \"C.xml\""));
            Assert.That(dump, Does.Contain("signature 1: [3,13,4]"));
            Assert.That(dump, Does.Contain("record 2: assembly=10 type=11 method=12 integration=14 signature=1 versions=1.2.3-4.5.6 "
                + "tracer=15 metric=16 arguments=17 tracerArguments=0 functionId=0x0000000000000001 "
                + "pointKey=0x0000000000000002 sourceFile=1 flags=1"));
            Assert.That(dump, Does.Contain("assembly 1: string=10 firstRecord=2 recordCount=1"));
            Assert.That(dump, Does.Contain("file 0: string=18 hash=0xA1A2A3A4A5A6A7A8 flags=1"));
        });
    }

    [Test]
    public void Dump_TruncatedTable_StopsInsteadOfThrowing()
    {
        Assert.That(Dump(Truncated(GoldenTwoAssemblies, 60)), Does.Contain("(dump stopped: "));
    }

    // ---- Whole-table defects ----------------------------------------------------------------------------------------

    [Test]
    public void TryRead_Null_ReturnsFalse()
    {
        AssertRejected(null, "table is null");
    }

    [TestCase(0)]
    [TestCase(49)]
    public void TryRead_ShorterThanHeader_ReturnsFalse(int length)
    {
        AssertRejected(GoldenTwoAssemblies.Take(length).ToArray(), "header runs past the end");
    }

    [TestCase(0)]
    [TestCase(4)]
    public void TryRead_BadMagic_ReturnsFalse(int position)
    {
        AssertRejected(WithByte(GoldenTwoAssemblies, position, 0x58), "bad magic");
    }

    [TestCase((byte)0)]
    [TestCase((byte)1)]
    [TestCase((byte)4)]
    public void TryRead_WrongVersion_ReturnsFalse(byte version)
    {
        AssertRejected(WithByte(GoldenTwoAssemblies, 5, version), "unsupported format version " + version);
    }

    [Test]
    public void TryRead_Version2_Rejected()
    {
        // A version 2 table (56-byte records, no files section) must never be misread as version 3.
        AssertRejected(WithByte(GoldenTwoAssemblies, 5, 2), "version");
    }

    [TestCase(532u)]
    [TestCase(534u)]
    public void TryRead_TotalLengthMismatch_ReturnsFalse(uint declaredLength)
    {
        AssertRejected(WithU32(GoldenTwoAssemblies, HeaderTotalLength, declaredLength), "total length " + declaredLength + " does not match");
    }

    [Test]
    public void TryRead_BytesAfterTheFilesSection_ReturnsFalse()
    {
        // One file entry fewer than the bytes hold: the section ends before the table does.
        AssertRejected(WithU32(GoldenTwoAssemblies, HeaderFileCount, 2), "files section ends at 520 but the table is 533 bytes");
    }

    [Test]
    public void TryRead_AssemblyCountShort_FilesSectionStartMismatch_ReturnsFalse()
    {
        // One assembly entry fewer: the files section is now expected 12 bytes earlier than the header says.
        AssertRejected(WithU32(GoldenTwoAssemblies, HeaderAssemblyCount, 1), "files section starts at 494, expected 482");
    }

    // ---- Truncation and out-of-range header values ------------------------------------------------------------------

    [TestCase(68, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_InsideStrings")]
    [TestCase(93, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_AtStringLengthPrefix")]
    [TestCase(250, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_InsideSignatures")]
    [TestCase(266, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_AtStartOfRecords")]
    [TestCase(330, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_InsideRecords")]
    [TestCase(470, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_AtStartOfAssemblies")]
    [TestCase(475, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_InsideAssemblies")]
    [TestCase(494, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_AtStartOfFiles")]
    [TestCase(500, TestName = "TryRead_TruncatedInEachSection_ReturnsFalse_InsideFiles")]
    public void TryRead_TruncatedInEachSection_ReturnsFalse(int length)
    {
        AssertRejected(Truncated(GoldenTwoAssemblies, length), "runs past the end");
    }

    [TestCase(494)]
    [TestCase(507)]
    [TestCase(532)]
    public void TryRead_FileSectionTruncated_Rejected(int length)
    {
        AssertRejected(Truncated(GoldenTwoAssemblies, length), "files section runs past the end");
    }

    [Test]
    public void TryRead_TruncatedAtAStringLengthPrefix_NamesTheString()
    {
        // Cut at 93: strings 0-4 end exactly at byte 93, so string 5 has no room for its u16 length prefix. (The cut must
        // leave room for 21 length prefixes, or the whole-section check rejects it first.)
        AssertRejected(Truncated(GoldenTwoAssemblies, 93), "string 5 length runs past the end");
    }

    [Test]
    public void TryRead_TruncatedAtASignatureCount_NamesTheSignature()
    {
        // Cut at 253: signature 0 (count byte at 244 plus two u32 elements) ends exactly at byte 253, so signature 1
        // has no room for its count byte.
        AssertRejected(Truncated(GoldenTwoAssemblies, 253), "signature 1 count runs past the end");
    }

    [Test]
    public void TryRead_StringLengthRunsPastTheEnd_ReturnsFalse()
    {
        var table = WithByte(WithByte(GoldenTwoAssemblies, StringsPosition(GoldenTwoAssemblies), 0xFF), StringsPosition(GoldenTwoAssemblies) + 1, 0xFF);

        AssertRejected(table, "string 0 runs past the end");
    }

    [Test]
    public void TryRead_SignatureLengthRunsPastTheEnd_ReturnsFalse()
    {
        AssertRejected(WithByte(GoldenTwoAssemblies, SignaturesPosition(GoldenTwoAssemblies), 200), "signature 0 runs past the end");
    }

    [TestCase(HeaderStringCount)]
    [TestCase(HeaderSignatureCount)]
    [TestCase(HeaderRecordCount)]
    [TestCase(HeaderAssemblyCount)]
    public void TryRead_CountLargerThanTheBuffer_ReturnsFalse(int countField)
    {
        AssertRejected(WithU32(GoldenTwoAssemblies, countField, uint.MaxValue), "runs past the end");
    }

    [TestCase(0x10000u)]
    [TestCase(uint.MaxValue)]
    public void TryRead_FileCountOverTheLimit_ReturnsFalse(uint fileCount)
    {
        // Index 0xFFFF means "the plugin", so a table can name at most 65535 files (indices 0 to 65534).
        AssertRejected(WithU32(GoldenTwoAssemblies, HeaderFileCount, fileCount), "file count " + fileCount + " is over the limit of 65535");
    }

    [Test]
    public void TryRead_FileCountAtTheLimit_IsCheckedAgainstTheBuffer()
    {
        AssertRejected(WithU32(GoldenTwoAssemblies, HeaderFileCount, 0xFFFF), "files section runs past the end");
    }

    [TestCase(HeaderStringsOffset, 0x00FFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_Strings")]
    [TestCase(HeaderSignaturesOffset, 0x00FFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_Signatures")]
    [TestCase(HeaderRecordsOffset, 0x00FFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_Records")]
    [TestCase(HeaderAssembliesOffset, 0x00FFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_Assemblies")]
    [TestCase(HeaderFilesOffset, 0x00FFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_Files")]
    [TestCase(HeaderStringsOffset, 0xFFFFFFFFu, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_StringsMaxValue")]
    [TestCase(HeaderSignaturesOffset, 60u, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_SignaturesOverlappingStrings")]
    [TestCase(HeaderRecordsOffset, 267u, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_RecordsLeavingAGap")]
    [TestCase(HeaderFilesOffset, 482u, TestName = "TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse_FilesOverlappingAssemblies")]
    public void TryRead_SectionOffsetNotWherePreviousSectionEnds_ReturnsFalse(int offsetField, uint offset)
    {
        AssertRejected(WithU32(GoldenTwoAssemblies, offsetField, offset), "section starts at " + offset);
    }

    // ---- Indices and values -----------------------------------------------------------------------------------------

    [TestCase(4, 1, "type of record 1")]
    [TestCase(8, 1, "method of record 1")]
    [TestCase(12, 1, "integration type of record 1")]
    [TestCase(32, 1, "tracer factory of record 1")]
    [TestCase(36, 1, "metric name of record 1")]
    [TestCase(40, 1, "argument signature of record 1")]
    public void TryRead_StringIndexOutOfRange_InRecord_ReturnsFalse(int fieldOffset, int record, string expectedWhere)
    {
        var table = WithU32(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, record) + fieldOffset, 99);

        AssertRejected(table, "string index 99 out of range in " + expectedWhere);
    }

    [Test]
    public void TryRead_StringIndexOutOfRange_InSignatureEntry_ReturnsFalse()
    {
        // First element of signature 0 starts one byte after its count byte.
        var table = WithU32(GoldenTwoAssemblies, SignaturesPosition(GoldenTwoAssemblies) + 1, 99);

        AssertRejected(table, "string index 99 out of range in signature 0");
    }

    [Test]
    public void TryRead_SignatureIndexOutOfRange_ReturnsFalse()
    {
        var table = WithU32(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, 2) + 16, 2);

        AssertRejected(table, "signature index 2 out of range in record 2");
    }

    [Test]
    public void TryRead_EmptySignature_ReturnsFalse()
    {
        AssertRejected(Build(new Row { Signature = new string[0] }), "signature 0 is empty");
    }

    [TestCase("assembly")]
    [TestCase("type")]
    [TestCase("method")]
    [TestCase("integration type")]
    [TestCase("tracer factory")]
    public void TryRead_EmptyRequiredString_ReturnsFalse(string field)
    {
        var row = new Row
        {
            Assembly = field == "assembly" ? "" : "Asm",
            Type = field == "type" ? "" : "Type",
            Method = field == "method" ? "" : "Method",
            IntegrationType = field == "integration type" ? "" : "Integration",
            TracerFactory = field == "tracer factory" ? "" : "Tracer",
        };

        AssertRejected(Build(row), "empty " + field + " in record 0");
    }

    [TestCase((ushort)3)]
    [TestCase((ushort)0xFFFE)]
    public void TryRead_SourceFileOutOfRange_Rejected(ushort sourceFile)
    {
        // The golden table has 3 files; 0xFFFF (the plugin) is the only index allowed past them.
        var table = WithU16(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, 1) + RecordSourceFile, sourceFile);

        AssertRejected(table, "source file " + sourceFile + " out of range in record 1 (the table has 3 files)");
    }

    [Test]
    public void TryRead_SourceFileWithNoFiles_Rejected()
    {
        AssertRejected(Build(new Row { SourceFile = 0 }), "source file 0 out of range in record 0 (the table has 0 files)");
    }

    // ---- Files ------------------------------------------------------------------------------------------------------

    [Test]
    public void TryRead_FileHomeNameIndexOutOfRange_ReturnsFalse()
    {
        var table = WithU32(GoldenTwoAssemblies, FileEntryPosition(GoldenTwoAssemblies, 1), 99);

        AssertRejected(table, "string index 99 out of range in file 1");
    }

    [Test]
    public void TryRead_FileHomeNameEmpty_ReturnsFalse()
    {
        // String 9 is "".
        var table = WithU32(GoldenTwoAssemblies, FileEntryPosition(GoldenTwoAssemblies, 2), 9);

        AssertRejected(table, "empty home name in file 2");
    }

    [Test]
    public void TryRead_DuplicateHomeName_ReturnsFalse()
    {
        // File 1 names "A.xml" (string 18) too.
        var table = WithU32(GoldenTwoAssemblies, FileEntryPosition(GoldenTwoAssemblies, 1), 18);

        AssertRejected(table, "duplicate home name \"A.xml\" in file 1");
    }

    [Test]
    public void TryRead_DuplicateHomeNameDifferingOnlyInCase_ReturnsFalse()
    {
        // Windows file names: "a.XML" and "A.xml" are the same file.
        var files = new[] { new ShippedFile { HomeName = "A.xml" }, new ShippedFile { HomeName = "a.XML" } };

        AssertRejected(Build(files, new Row()), "duplicate home name \"a.XML\" in file 1");
    }

    // ---- Assembly index ---------------------------------------------------------------------------------------------

    [Test]
    public void TryRead_AssemblyRangesNotCovering_GapBetweenEntries_ReturnsFalse()
    {
        // Entry 0 shrinks to one record, so entry 1 (starting at record 2) leaves record 1 in no assembly.
        var table = WithU32(GoldenTwoAssemblies, AssemblyEntryPosition(GoldenTwoAssemblies, 0) + 8, 1);

        AssertRejected(table, "assembly entry 1 starts at record 2, expected 1");
    }

    [Test]
    public void TryRead_AssemblyRangesNotCovering_LastRecordUncovered_ReturnsFalse()
    {
        // Entry 1 shrinks to zero records, so record 2 belongs to no assembly.
        var table = WithU32(GoldenTwoAssemblies, AssemblyEntryPosition(GoldenTwoAssemblies, 1) + 8, 0);

        AssertRejected(table, "assembly entries cover 2 of 3 records");
    }

    [Test]
    public void TryRead_AssemblyRangeOverlap_ReturnsFalse()
    {
        // Entry 1 claims to start at record 1, which entry 0 already owns.
        var table = WithU32(GoldenTwoAssemblies, AssemblyEntryPosition(GoldenTwoAssemblies, 1) + 4, 1);

        AssertRejected(table, "assembly entry 1 starts at record 1, expected 2");
    }

    [Test]
    public void TryRead_AssemblyRangeLongerThanTheRecords_ReturnsFalse()
    {
        var table = WithU32(GoldenTwoAssemblies, AssemblyEntryPosition(GoldenTwoAssemblies, 1) + 8, 5);

        AssertRejected(table, "assembly entry 1 covers 5 records from 2 but the table has 3");
    }

    [Test]
    public void TryRead_NoAssemblyEntriesButRecordsExist_ReturnsFalse()
    {
        // Entries are the only way to reach the records, so dropping them all leaves every record uncovered.
        // Cut at the start of the assemblies section, which also drops the files section after it.
        var withoutEntries = Truncated(GoldenTwoAssemblies, 470);
        withoutEntries = WithU32(withoutEntries, HeaderAssemblyCount, 0);
        withoutEntries = WithU32(withoutEntries, HeaderFileCount, 0);
        withoutEntries = WithU32(withoutEntries, HeaderFilesOffset, 470);

        AssertRejected(withoutEntries, "assembly entries cover 0 of 3 records");
    }

    [Test]
    public void TryRead_RecordAssemblyMismatchesIndex_ReturnsFalse()
    {
        // Record 1 names AsmB (string 10), a valid string, but its assembly entry says AsmA (string 0).
        var table = WithU32(GoldenTwoAssemblies, RecordPosition(GoldenTwoAssemblies, 1), 10);

        AssertRejected(table, "record 1 names assembly string 10 but its assembly entry names 0");
    }

    [Test]
    public void TryRead_AssemblyEntryStringIndexOutOfRange_ReturnsFalse()
    {
        // Entry 1 owns record 2; its string index is checked before the record is compared with it.
        var table = WithU32(GoldenTwoAssemblies, AssemblyEntryPosition(GoldenTwoAssemblies, 1), 99);

        AssertRejected(table, "string index 99 out of range in assembly entry 1");
    }

    [Test]
    public void TryRead_AssemblyEntryStringIndexOutOfRange_InARunOfLengthZero_ReturnsFalse()
    {
        // No record ever compares against an entry with no records, so the entry's own check is the only one.
        var table = WithExtraAssemblyEntry(GoldenTwoAssemblies, 99, 3, 0);

        AssertRejected(table, "string index 99 out of range in assembly entry 2");
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------

    // Copy of the table with one more assembly entry inserted at the end of the assemblies section (just before the
    // files section), and the header's assembly count, files offset and total length updated to match.
    private static byte[] WithExtraAssemblyEntry(byte[] table, uint assemblyString, uint firstRecord, uint recordCount)
    {
        var filesOffset = (int)ReadU32(table, HeaderFilesOffset);
        var extended = table.Take(filesOffset)
            .Concat(System.BitConverter.GetBytes(assemblyString))
            .Concat(System.BitConverter.GetBytes(firstRecord))
            .Concat(System.BitConverter.GetBytes(recordCount))
            .Concat(table.Skip(filesOffset))
            .ToArray();
        extended = WithU32(extended, HeaderAssemblyCount, ReadU32(table, HeaderAssemblyCount) + 1);
        extended = WithU32(extended, HeaderFilesOffset, (uint)(filesOffset + AssemblyEntrySize));
        return WithU32(extended, HeaderTotalLength, (uint)extended.Length);
    }

    private static void AssertRejected(byte[] table, string expectedReason)
    {
        var ok = InstrumentationDefinitionsTable.TryRead(table, out var definitions, out var sites, out var manifest, out var failureReason);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False, table == null ? "null table" : Dump(table));
            Assert.That(definitions, Is.Not.Null.And.Empty);
            Assert.That(sites, Is.Null);
            Assert.That(manifest, Is.Null);
            Assert.That(failureReason, Does.Contain(expectedReason));
        });
    }

    private static void AssertVersions(DistroCallTargetDefinition definition, int minMajor, int minMinor, int minPatch, int maxMajor, int maxMinor, int maxPatch)
    {
        Assert.That(
            new int[] { definition.MinMajor, definition.MinMinor, definition.MinPatch, definition.MaxMajor, definition.MaxMinor, definition.MaxPatch },
            Is.EqualTo(new[] { minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch }));
    }

    private static DistroInstrumentationSite GetSite(DistroInstrumentationSites sites, int index)
    {
        Assert.That(sites.TryGet(index, out var site), Is.True, "site " + index);
        return site;
    }

    private static void AssertSite(DistroInstrumentationSite site, string tracerFactoryName, string metricName, uint tracerArguments, string assemblyName, string typeName, string methodName, string argumentSignature, ulong functionId)
    {
        Assert.That(site.TracerFactoryName, Is.EqualTo(tracerFactoryName));
        Assert.That(site.MetricName, Is.EqualTo(metricName));
        Assert.That(site.TracerArguments, Is.EqualTo(tracerArguments));
        Assert.That(site.AssemblyName, Is.EqualTo(assemblyName));
        Assert.That(site.TypeName, Is.EqualTo(typeName));
        Assert.That(site.MethodName, Is.EqualTo(methodName));
        Assert.That(site.ArgumentSignature, Is.EqualTo(argumentSignature));
        Assert.That(site.FunctionId, Is.EqualTo(functionId));
    }
}
