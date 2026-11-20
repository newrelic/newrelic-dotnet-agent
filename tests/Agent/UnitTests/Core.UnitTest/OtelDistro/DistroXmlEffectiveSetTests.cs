// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.OtelDistro.Xml;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.DistroShippedXmlTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroXmlEffectiveSetTests
{
    private const char Bom = (char)0xFEFF;
    private const string FileA = "A.xml";
    private const string FileB = "B.xml";
    private const string FileC = "C.xml";
    private const string FileHashOnly = "H.xml";

    private static readonly string FactoryRun = Factory("F1", "M1", "Asm", "NS.Foo", "Run", "System.String");
    private static readonly string FactoryWide = Factory("F1", "M1", "Asm", "NS.Foo", "Wide", null);
    private static readonly string FactoryRead = Factory("Fdis", "Mdis", "Asm", "NS.Foo", "Read", "System.Int32", enabled: "false");

    // A.xml: one explicit point, one overload-wide point (9 records) and one default-off point.
    private static readonly string TextA = File(FactoryRun, FactoryWide, FactoryRead);
    private static readonly string TextB = File(Factory("F2", "M2", "Asm2", "NS.Bar", "Go", ""));
    private static readonly string TextC = File(Factory("Fdis2", "Mdis2", "Asm3", "NS.Baz", "Off", "", enabled: "false"));
    private static readonly string TextHashOnly = File(Factory("F3", "M3", "Asm4", "NS.Hash", "Only", ""));

    private Shipped _shipped;

    [SetUp]
    public void SetUp()
    {
        _shipped = new Shipped(
            new[] { (FileA, TextA, true), (FileB, TextB, true), (FileC, TextC, true), (FileHashOnly, TextHashOnly, false) });
    }

    private static DistroXmlFile Disk(string name, string text) => new DistroXmlFile(name, "/ext/" + name, Encoding.UTF8.GetBytes(text), null);

    private static DistroXmlFile Unreadable(string name, string error) => new DistroXmlFile(name, "/ext/" + name, null, error);

    private DistroXmlEffectiveSetResult Compute(params DistroXmlFile[] onDisk) =>
        DistroXmlEffectiveSet.Compute(_shipped.Sites, _shipped.Manifest, onDisk, skipInvalid: false);

    private DistroXmlEffectiveSetResult ComputeSkippingInvalid(params DistroXmlFile[] onDisk) =>
        DistroXmlEffectiveSet.Compute(_shipped.Sites, _shipped.Manifest, onDisk, skipInvalid: true);

    private DistroXmlFile[] Unchanged() => new[] { Disk(FileA, TextA), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly) };

    private int[] SitesOf(string method)
    {
        var indexes = new List<int>();
        for (var i = 0; i < _shipped.Sites.Count; i++)
        {
            _shipped.Sites.TryGet(i, out var site);
            if (site.MethodName == method)
            {
                indexes.Add(i);
            }
        }

        return indexes.ToArray();
    }

    private static bool[] StatesOf(DistroXmlEffectiveSetResult result, int[] indexes) => indexes.Select(i => result.On[i]).ToArray();

    private static void AssertAllOn(DistroXmlEffectiveSetResult result, int[] indexes, bool expected) =>
        Assert.That(indexes.Select(i => result.On[i]).All(on => on == expected), Is.True, "expected all " + expected + ": " + string.Join(",", StatesOf(result, indexes)));

    private bool[] DefaultState()
    {
        var state = new bool[_shipped.Sites.Count];
        for (var i = 0; i < state.Length; i++)
        {
            state[i] = !_shipped.Sites.IsDefaultOff(i);
        }

        return state;
    }

    [Test]
    public void Compute_AllShippedUnchanged_DefaultsNoParse()
    {
        var result = Compute(Unchanged());

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False);
            Assert.That(result.ParsedFiles, Is.Empty);
            Assert.That(result.UnreadableFiles, Is.Empty);
            Assert.That(result.Unsupported, Is.Empty);
            Assert.That(result.HashOnlyFileCount, Is.EqualTo(1));
            Assert.That(result.On, Is.EqualTo(DefaultState()));
            AssertAllOn(result, SitesOf("Read"), false);
            AssertAllOn(result, SitesOf("Off"), false);
            AssertAllOn(result, SitesOf("Run"), true);
            AssertAllOn(result, SitesOf("Wide"), true);
            Assert.That(SitesOf("Wide"), Has.Length.EqualTo(9));
        });
    }

    [Test]
    public void Compute_EnabledTrueOnDisabledFactory_TurnsOn()
    {
        var edited = TextA.Replace("enabled=\"false\"", "enabled=\"true\"");

        var result = Compute(Disk(FileA, edited), Disk(FileB, TextB), Disk(FileC, TextC));

        Assert.Multiple(() =>
        {
            Assert.That(result.ParsedFiles, Is.EqualTo(new[] { FileA }));
            Assert.That(result.Unsupported, Is.Empty);
            AssertAllOn(result, SitesOf("Read"), true);
            AssertAllOn(result, SitesOf("Run"), true);
            AssertAllOn(result, SitesOf("Wide"), true);
            AssertAllOn(result, SitesOf("Go"), true);
        });
    }

    [Test]
    public void Compute_EnabledFalseOnShippedFactory_TurnsOff()
    {
        var edited = TextA.Replace("<tracerFactory name=\"F1\" metricName=\"M1\">", "<tracerFactory name=\"F1\" metricName=\"M1\" enabled=\"false\">");

        var result = Compute(Disk(FileA, edited), Disk(FileB, TextB), Disk(FileC, TextC));

        Assert.Multiple(() =>
        {
            Assert.That(result.Unsupported, Is.Empty);
            AssertAllOn(result, SitesOf("Run"), false);
            AssertAllOn(result, SitesOf("Wide"), false);
            AssertAllOn(result, SitesOf("Read"), false);
            AssertAllOn(result, SitesOf("Go"), true);
        });
    }

    [Test]
    public void Compute_CommentedOutMatch_ThatPointOff_OthersOn()
    {
        var edited = File(FactoryWide, FactoryRead).Replace("</instrumentation>", "<!-- " + FactoryRun.Replace("--", "- -") + " --></instrumentation>");

        var result = Compute(Disk(FileA, edited), Disk(FileB, TextB), Disk(FileC, TextC));

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False);
            AssertAllOn(result, SitesOf("Run"), false);
            AssertAllOn(result, SitesOf("Wide"), true);
            AssertAllOn(result, SitesOf("Go"), true);
        });
    }

    [Test]
    public void Compute_CompiledFileDeleted_ItsPointsOff()
    {
        var result = Compute(Disk(FileA, TextA), Disk(FileC, TextC));

        Assert.Multiple(() =>
        {
            Assert.That(result.ParsedFiles, Is.Empty);
            AssertAllOn(result, SitesOf("Go"), false);
            AssertAllOn(result, SitesOf("Run"), true);
            Assert.That(result.HashOnlyFileCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Compute_PluginRecords_AlwaysOn()
    {
        var result = Compute();

        Assert.Multiple(() =>
        {
            AssertAllOn(result, SitesOf("Own"), true);
            AssertAllOn(result, SitesOf("Run"), false);
            AssertAllOn(result, SitesOf("Wide"), false);
            AssertAllOn(result, SitesOf("Go"), false);
        });
    }

    [Test]
    public void Compute_ExtraFileWithShippedKeys_TurnsThemOn()
    {
        var extra = File(Factory("Fdis", "Mdis", "Asm", "NS.Foo", "Read", "System.Int32"));

        var result = Compute(Disk("Extra.xml", extra));

        Assert.Multiple(() =>
        {
            Assert.That(result.ParsedFiles, Is.EqualTo(new[] { "Extra.xml" }));
            Assert.That(result.Unsupported, Is.Empty);
            AssertAllOn(result, SitesOf("Read"), true);
            AssertAllOn(result, SitesOf("Run"), false);
        });
    }

    [Test]
    public void Compute_ExtraFileWithUnknownMethod_Unsupported()
    {
        var extra = File(
            Factory("Custom", "MC", "Asm", "NS.Foo", "Unknown", "System.String,System.Int32"),
            Factory("Custom", "MC", "Asm", "NS.Foo", "AnyOverload", null),
            Factory("CustomOff", "MC", "Asm", "NS.Foo", "Disabled", "", enabled: "false"));

        var result = Compute(Disk("Extra.xml", extra));

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False);
            Assert.That(result.Unsupported, Has.Count.EqualTo(2));
            Assert.That(Describe(result.Unsupported[0]), Is.EqualTo("Extra.xml|NS.Foo|Unknown|System.String,System.Int32|Custom"));
            Assert.That(Describe(result.Unsupported[1]), Is.EqualTo("Extra.xml|NS.Foo|AnyOverload|*|Custom"));
        });
    }

    [Test]
    public void Compute_HashOnlyFileEdited_Ignored()
    {
        var edited = File(Factory("F3", "M3", "Asm4", "NS.Hash", "Only", ""), Factory("Custom", "MC", "Asm4", "NS.Hash", "Extra", ""));

        var result = Compute(Disk(FileA, TextA), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, edited));

        Assert.Multiple(() =>
        {
            Assert.That(result.ParsedFiles, Is.Empty);
            Assert.That(result.Unsupported, Is.Empty);
            Assert.That(result.HashOnlyFileCount, Is.EqualTo(1));
            Assert.That(result.On, Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void Compute_HashOnlyFileUnreadableOrNotRead_CountedButNeverListedUnreadable()
    {
        var unreadable = Compute(Unreadable(FileHashOnly, "locked"));
        var notRead = Compute(DistroXmlFile.NotRead(FileHashOnly, "/ext/" + FileHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(unreadable.HashOnlyFileCount, Is.EqualTo(1));
            Assert.That(unreadable.UnreadableFiles, Is.Empty);
            Assert.That(notRead.HashOnlyFileCount, Is.EqualTo(1));
            Assert.That(notRead.UnreadableFiles, Is.Empty);
            Assert.That(notRead.FileHashes, Is.Empty);
        });
    }

    [Test]
    public void Compute_MalformedChangedFile_Rejected()
    {
        var result = Compute(Disk(FileB, TextB), Disk(FileA, "<extension><instrumentation>"), Disk("Later.xml", TextHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.True);
            Assert.That(result.RejectedFile, Is.EqualTo(FileA));
            Assert.That(result.RejectedReason, Does.StartWith("Malformed instrumentation XML"));
            Assert.That(result.On, Is.Null);
            Assert.That(result.ParsedFiles, Is.Empty);
        });
    }

    [Test]
    public void Compute_UnreadableCompiledFile_UsesDefaults()
    {
        var result = Compute(Unreadable(FileA, "locked"), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(result.UnreadableFiles, Is.EqualTo(new[] { FileA }));
            Assert.That(result.ParsedFiles, Is.Empty);
            Assert.That(result.On, Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void Compute_UnreadableUnknownFile_ListedAndContributesNothing()
    {
        var result = Compute(Disk(FileA, TextA), Unreadable("Mystery.xml", "locked"));

        Assert.Multiple(() =>
        {
            Assert.That(result.UnreadableFiles, Is.EqualTo(new[] { "Mystery.xml" }));
            Assert.That(result.ParsedFiles, Is.Empty);
            Assert.That(result.Unsupported, Is.Empty);
            AssertAllOn(result, SitesOf("Run"), true);
        });
    }

    [Test]
    public void Compute_OverloadWidePoint_AllNineRecordsFollow()
    {
        var wideOnly = File(FactoryWide);
        var wideOff = File(Factory("F1", "M1", "Asm", "NS.Foo", "Wide", null, enabled: "false"));

        var on = Compute(Disk(FileA, wideOnly));
        var off = Compute(Disk(FileA, wideOff));

        Assert.Multiple(() =>
        {
            AssertAllOn(on, SitesOf("Wide"), true);
            AssertAllOn(on, SitesOf("Run"), false);
            AssertAllOn(off, SitesOf("Wide"), false);
        });
    }

    [Test]
    public void Compute_MetricNameChanged_OriginalOffAndUnsupported()
    {
        var edited = TextA.Replace("metricName=\"M1\"", "metricName=\"Renamed\"");

        var result = Compute(Disk(FileA, edited));

        Assert.Multiple(() =>
        {
            AssertAllOn(result, SitesOf("Run"), false);
            AssertAllOn(result, SitesOf("Wide"), false);
            Assert.That(result.Unsupported.Select(u => u.MethodName), Is.EqualTo(new[] { "Run", "Wide" }));
        });
    }

    [Test]
    public void Compute_VersionRangeChanged_OriginalOffAndUnsupported()
    {
        var edited = File(Factory("F1", "M1", "Asm", "NS.Foo", "Run", "System.String", min: "2.0.0"));

        var result = Compute(Disk(FileA, edited));

        Assert.Multiple(() =>
        {
            AssertAllOn(result, SitesOf("Run"), false);
            Assert.That(result.Unsupported.Select(u => u.MethodName), Is.EqualTo(new[] { "Run" }));
        });
    }

    [Test]
    public void Compute_ReSavedWithBomAndCrlf_NoChanges()
    {
        // The hash ignores a BOM and CRLF, so a pure re-save is never parsed; an editor that also reflows the file
        // changes the hash and the file is parsed, and must still come out as the defaults.
        var resaved = Bom + TextA.Replace("\n", "\r\n");
        var reflowed = Bom + TextA.Replace("\n", "\r\n").Replace("  <tracerFactory", "\t<tracerFactory").Replace("</instrumentation>", "  </instrumentation>");

        var pure = Compute(Disk(FileA, resaved), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));
        var parsed = Compute(Disk(FileA, reflowed.TrimStart(Bom)), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));
        var parsedWithBom = Compute(Disk(FileA, reflowed), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(pure.ParsedFiles, Is.Empty);
            Assert.That(pure.On, Is.EqualTo(DefaultState()));
            Assert.That(parsed.ParsedFiles, Is.EqualTo(new[] { FileA }));
            Assert.That(parsed.On, Is.EqualTo(DefaultState()));
            Assert.That(parsed.Unsupported, Is.Empty);
            Assert.That(parsedWithBom.Rejected, Is.False);
            Assert.That(parsedWithBom.On, Is.EqualTo(DefaultState()));
            Assert.That(parsedWithBom.Unsupported, Is.Empty);
        });
    }

    [Test]
    public void Compute_DuplicateCopyOfShippedFile_UnionKeepsPointsOn()
    {
        var withBoth = Compute(Disk(FileA, TextA), Disk("Copy of A.xml", TextA), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));
        var originalDeleted = Compute(Disk("Copy of A.xml", TextA), Disk(FileB, TextB), Disk(FileC, TextC), Disk(FileHashOnly, TextHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(withBoth.Unsupported, Is.Empty);
            Assert.That(withBoth.ParsedFiles, Is.EqualTo(new[] { "Copy of A.xml" }));
            Assert.That(withBoth.On, Is.EqualTo(DefaultState()));
            Assert.That(originalDeleted.Unsupported, Is.Empty);
            Assert.That(originalDeleted.On, Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void Compute_PointsTheGeneratorWouldNotEmit_AreNotUnsupported()
    {
        var extra = File(
            Factory("Custom", "MC", "Asm", "NS.Foo,", "Trailing", ""),
            Factory("Custom", "MC", "Asm", "NS.Foo, NS.Padded", "Padded", ""),
            Factory("Custom", "MC", "Asm", "NS.Foo", "EmptyRange", "", min: "2.0.0", max: "1.0.0"),
            Factory("Custom", "MC", "Asm", "NS.Foo", "NineParameters", string.Join(",", Enumerable.Repeat("System.Int32", XmlPointEligibility.MaxParameters + 1))));

        var result = Compute(Disk("Extra.xml", extra));

        // Only the valid "NS.Foo" segment of each comma list is a point; the empty and the space-padded segments, the empty
        // version range and the point with more parameters than OTel takes produce nothing (the generator drops them too).
        Assert.That(result.Unsupported.Select(u => u.TypeName + "." + u.MethodName), Is.EqualTo(new[] { "NS.Foo.Trailing", "NS.Foo.Padded" }));
    }

    [Test]
    public void Compute_PointWithoutClassName_IsUnsupportedAndNeverThrows()
    {
        var extra = File(Factory("Custom", "MC", "Asm", null, "NoClass", ""));

        var result = Compute(Disk("Extra.xml", extra));

        Assert.Multiple(() =>
        {
            Assert.That(result.Unsupported, Has.Count.EqualTo(1));
            Assert.That(result.Unsupported[0].TypeName, Is.Null);
        });
    }

    [Test]
    public void DistroXmlFile_CarriesNamePathBytesAndReadError()
    {
        var unreadable = new DistroXmlFile("A.xml", "/ext/A.xml", null, "locked");
        var read = new DistroXmlFile("B.xml", "/ext/B.xml", new byte[] { 1 }, null);
        var notRead = DistroXmlFile.NotRead("C.xml", "/ext/C.xml");

        Assert.Multiple(() =>
        {
            Assert.That((unreadable.HomeName, unreadable.Path, unreadable.Bytes, unreadable.ReadError, unreadable.IsRead), Is.EqualTo(("A.xml", "/ext/A.xml", (byte[])null, "locked", true)));
            Assert.That((read.HomeName, read.Path, read.ReadError, read.IsRead), Is.EqualTo(("B.xml", "/ext/B.xml", (string)null, true)));
            Assert.That(read.Bytes, Is.EqualTo(new byte[] { 1 }));
            Assert.That((notRead.HomeName, notRead.Path, notRead.Bytes, notRead.ReadError, notRead.IsRead), Is.EqualTo(("C.xml", "/ext/C.xml", (byte[])null, (string)null, false)));
        });
    }

    [Test]
    public void Compute_Unchanged_FastPathEqualsTheFullComputation()
    {
        // The same files, once unchanged (fast path, no key sets) and once with an extra file that names nothing (full
        // computation): the on-state must be identical.
        var fast = Compute(Unchanged());
        var full = Compute(Unchanged().Append(Disk("Empty.xml", File())).ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(full.ParsedFiles, Is.EqualTo(new[] { "Empty.xml" }));
            Assert.That(fast.On, Is.EqualTo(full.On));
            Assert.That(fast.On, Is.EqualTo(DistroXmlEffectiveSet.Defaults(_shipped.Sites)));
            Assert.That(fast.MissingFiles, Is.Empty);
            Assert.That(fast.InvalidFiles, Is.Empty);
            Assert.That(fast.ParsedFiles, Is.Empty);
            Assert.That(fast.Unsupported, Is.Empty);
        });
    }

    [Test]
    public void Compute_FileHashes_EveryReadReadableFileExceptHashOnly()
    {
        var edited = TextA.Replace("enabled=\"false\"", "enabled=\"true\"");

        var result = Compute(Disk(FileA, edited), Disk(FileB, TextB), Unreadable(FileC, "locked"), Disk(FileHashOnly, TextHashOnly), Disk("Extra.xml", File()));

        Assert.That(result.FileHashes.Select(h => (h.HomeName, h.Path, h.Hash)), Is.EqualTo(new[]
        {
            (FileA, "/ext/" + FileA, XmlContentHasher.Compute(edited)),
            (FileB, "/ext/" + FileB, XmlContentHasher.Compute(TextB)),
            ("Extra.xml", "/ext/Extra.xml", XmlContentHasher.Compute(File())),
        }));
    }

    [Test]
    public void Compute_CompiledFilesMissing_ListedInManifestOrder()
    {
        var result = Compute(Disk(FileB, TextB), Disk(FileHashOnly, TextHashOnly));

        Assert.Multiple(() =>
        {
            Assert.That(result.MissingFiles, Is.EqualTo(new[] { FileA, FileC }), "the hash-only file is never missing");
            AssertAllOn(result, SitesOf("Run"), false);
            AssertAllOn(result, SitesOf("Go"), true);
        });
    }

    [Test]
    public void Compute_SkipInvalid_InvalidCompiledFileKeepsItsDefaultsOthersApply()
    {
        var extra = File(Factory("Fdis", "Mdis", "Asm", "NS.Foo", "Read", "System.Int32"));

        var result = ComputeSkippingInvalid(Disk(FileA, "<extension><instrumentation>"), Disk(FileB, TextB), Disk(FileC, TextC), Disk("Extra.xml", extra));

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False);
            Assert.That(result.InvalidFiles.Select(f => f.HomeName), Is.EqualTo(new[] { FileA }));
            Assert.That(result.InvalidFiles[0].Reason, Does.StartWith("Malformed instrumentation XML"));
            Assert.That(result.ParsedFiles, Is.EqualTo(new[] { "Extra.xml" }));
            AssertAllOn(result, SitesOf("Run"), true);
            AssertAllOn(result, SitesOf("Wide"), true);
            AssertAllOn(result, SitesOf("Read"), true);
            AssertAllOn(result, SitesOf("Go"), true);
        });
    }

    [Test]
    public void Compute_SkipInvalid_InvalidExtraFileContributesNothing()
    {
        var result = ComputeSkippingInvalid(Disk(FileA, TextA), Disk(FileB, TextB), Disk(FileC, TextC), Disk("Broken.xml", "<extension>"));

        Assert.Multiple(() =>
        {
            Assert.That(result.InvalidFiles.Select(f => f.HomeName), Is.EqualTo(new[] { "Broken.xml" }));
            Assert.That(result.On, Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void Compute_NotReadCompiledOrUnknownFile_TreatedAsUnreadable()
    {
        // Callers never skip reading these; if one did, the file keeps its defaults (compiled) or adds nothing (unknown).
        var result = Compute(DistroXmlFile.NotRead(FileA, "/ext/A.xml"), Disk(FileB, TextB), Disk(FileC, TextC), DistroXmlFile.NotRead("Mystery.xml", "/ext/Mystery.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(result.UnreadableFiles, Is.EqualTo(new[] { FileA, "Mystery.xml" }));
            Assert.That(result.On, Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void Compute_Utf16File_DecodedAndParsed()
    {
        var edited = TextA.Replace("enabled=\"false\"", "enabled=\"true\"");
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(edited)).ToArray();

        var result = Compute(new DistroXmlFile(FileA, "/ext/A.xml", utf16, null), Disk(FileB, TextB), Disk(FileC, TextC));

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False, () => result.RejectedReason);
            Assert.That(result.ParsedFiles, Is.EqualTo(new[] { FileA }));
            AssertAllOn(result, SitesOf("Read"), true);
        });
    }

    [Test]
    public void Compute_UnsupportedPoint_CarriesItsPointKey()
    {
        var extra = File(Factory("Custom", "MC", "Asm", "NS.Foo", "Unknown", "System.String"));

        var result = Compute(Disk("Extra.xml", extra));

        var point = InstrumentationXmlParser.Parse(extra).Points.Single();
        Assert.That(result.Unsupported.Single().PointKey, Is.EqualTo(PointKeyHasher.Compute(point, point.ClassName, VersionRangeConverter.Convert(null, null).RangeKey)));
    }

    [Test]
    public void Result_NullLists_AreEmpty()
    {
        var result = new DistroXmlEffectiveSetResult(false, null, null, new bool[0], null, null, null, null, null, null, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.ParsedFiles, Is.Empty);
            Assert.That(result.UnreadableFiles, Is.Empty);
            Assert.That(result.InvalidFiles, Is.Empty);
            Assert.That(result.MissingFiles, Is.Empty);
            Assert.That(result.Unsupported, Is.Empty);
            Assert.That(result.FileHashes, Is.Empty);
        });
    }

    private static string Describe(DistroXmlUnsupportedPoint point) =>
        point.File + "|" + point.TypeName + "|" + point.MethodName + "|" + point.Parameters + "|" + point.TracerFactory;
}
