// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroXmlFileSourceTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "nr-distro-xml-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string directory, string name, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Test]
    public void List_MissingNullAndEmptyDirectories_Skipped()
    {
        Write(_root, "A.xml", "<a/>");
        var source = new DistroXmlFileSource(new[] { Path.Combine(_root, "missing"), null, "", _root });

        var files = source.List();

        Assert.That(files.Select(f => f.HomeName), Is.EqualTo(new[] { "A.xml" }));
    }

    [Test]
    public void List_TopLevelXmlOnly_NonXmlIgnoredWithPathAndStamp()
    {
        var b = Write(_root, "b.XML", "<b/>");
        var a = Write(_root, "A.xml", "<a/>");
        Write(_root, "c.txt", "text");
        Write(_root, "d.xmlx", "<d/>");
        Write(_root, "extension.xsd", "<xsd/>");
        Write(Path.Combine(_root, "sub"), "E.xml", "<e/>");
        var stamp = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(a, stamp);

        var files = new DistroXmlFileSource(new[] { _root }).List();

        Assert.Multiple(() =>
        {
            Assert.That(files.Select(f => f.HomeName), Is.EqualTo(new[] { "A.xml", "b.XML" }), "sorted ignoring case");
            Assert.That(files.Select(f => f.Path), Is.EqualTo(new[] { a, b }));
            Assert.That(files[0].LastWriteTimeUtc, Is.EqualTo(stamp));
            Assert.That(files.Select(f => f.Length), Is.EqualTo(new[] { 4L, 4L }));
        });
    }

    [Test]
    public void List_EditedFile_StampDiffers()
    {
        var path = Write(_root, "A.xml", "<a/>");
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var source = new DistroXmlFileSource(new[] { _root });
        var before = source.List().Single();

        File.WriteAllText(path, "<a enabled=\"true\"/>");
        var after = source.List().Single();

        Assert.Multiple(() =>
        {
            Assert.That(before.SameAs(before), Is.True);
            Assert.That(after.SameAs(before), Is.False);
            Assert.That(before.SameAs(null), Is.False);
        });
    }

    [Test]
    public void Entry_SameAs_ComparesPathIgnoringCaseTimeAndLength()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new DistroXmlFileEntry("A.xml", "/x/A.xml", time, 10);

        Assert.Multiple(() =>
        {
            Assert.That(entry.SameAs(new DistroXmlFileEntry("a.XML", "/X/a.xml", time, 10)), Is.True);
            Assert.That(entry.SameAs(new DistroXmlFileEntry("A.xml", "/x/B.xml", time, 10)), Is.False);
            Assert.That(entry.SameAs(new DistroXmlFileEntry("A.xml", "/x/A.xml", time.AddTicks(1), 10)), Is.False);
            Assert.That(entry.SameAs(new DistroXmlFileEntry("A.xml", "/x/A.xml", time, 11)), Is.False);
        });
    }

    [Test]
    public void Read_ReturnsTheRawBytes()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'<', (byte)'x', (byte)'/', (byte)'>', 0x0D, 0x0A };
        File.WriteAllBytes(Path.Combine(_root, "Bom.xml"), bytes);
        var source = new DistroXmlFileSource(new[] { _root });

        var file = source.Read(source.List().Single());

        Assert.Multiple(() =>
        {
            Assert.That(file.HomeName, Is.EqualTo("Bom.xml"));
            Assert.That(file.Path, Is.EqualTo(Path.Combine(_root, "Bom.xml")));
            Assert.That(file.Bytes, Is.EqualTo(bytes), "no decode, no BOM or CRLF handling here");
            Assert.That(file.ReadError, Is.Null);
        });
    }

    [Test]
    public void Read_LockedFile_BytesNullWithReadError()
    {
        var path = Write(_root, "Locked.xml", "<locked/>");
        Write(_root, "Open.xml", "<open/>");
        var source = new DistroXmlFileSource(new[] { _root });
        var listing = source.List();

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var locked = source.Read(listing.Single(f => f.HomeName == "Locked.xml"));
            var open = source.Read(listing.Single(f => f.HomeName == "Open.xml"));

            Assert.Multiple(() =>
            {
                Assert.That(locked.Bytes, Is.Null);
                Assert.That(locked.ReadError, Is.Not.Null.And.Not.Empty);
                Assert.That(locked.HomeName, Is.EqualTo("Locked.xml"));
                Assert.That(open.Bytes, Is.EqualTo(System.Text.Encoding.UTF8.GetBytes("<open/>")));
            });
        }
    }

    [Test]
    public void Read_DeletedSinceListingOrNull_ReadErrorNeverThrows()
    {
        var path = Write(_root, "Gone.xml", "<gone/>");
        var source = new DistroXmlFileSource(new[] { _root });
        var entry = source.List().Single();
        File.Delete(path);

        var gone = source.Read(entry);
        var none = source.Read(null);

        Assert.Multiple(() =>
        {
            Assert.That(gone.Bytes, Is.Null);
            Assert.That(gone.ReadError, Is.Not.Null);
            Assert.That(none.HomeName, Is.Null);
            Assert.That(none.ReadError, Is.Not.Null);
        });
    }

    [Test]
    public void List_TwoDirectories_InDirectoryOrder()
    {
        var runtime = Path.Combine(_root, AgentInstallConfiguration.RuntimeDirectoryName);
        Write(runtime, "A.xml", "<runtime/>");
        Write(_root, "Z.xml", "<root/>");

        var files = new DistroXmlFileSource(DistroXmlFileSource.DirectoriesFor(_root)).List();

        Assert.Multiple(() =>
        {
            Assert.That(files.Select(f => f.HomeName), Is.EqualTo(new[] { "Z.xml", "A.xml" }));
            Assert.That(files.Select(f => f.Path), Is.EqualTo(new[] { Path.Combine(_root, "Z.xml"), Path.Combine(runtime, "A.xml") }));
        });
    }

    [Test]
    public void Root_IsTheFirstDirectory()
    {
        var existing = new DistroXmlFileSource(new[] { _root, Path.Combine(_root, "missing") });
        var missing = new DistroXmlFileSource(new[] { Path.Combine(_root, "missing"), _root });

        Assert.Multiple(() =>
        {
            Assert.That(existing.RootDirectory, Is.EqualTo(_root));
            Assert.That(existing.RootExists, Is.True);
            Assert.That(missing.RootDirectory, Is.EqualTo(Path.Combine(_root, "missing")));
            Assert.That(missing.RootExists, Is.False);
        });
    }

    [Test]
    public void Root_NoDirectoriesOrEmpty_NullAndMissing()
    {
        var none = new DistroXmlFileSource(new string[0]);
        var empty = new DistroXmlFileSource(new[] { "" });

        Assert.Multiple(() =>
        {
            Assert.That(none.RootDirectory, Is.Null);
            Assert.That(none.RootExists, Is.False);
            Assert.That(empty.RootDirectory, Is.Null);
            Assert.That(empty.RootExists, Is.False);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void DirectoriesFor_NoExtensionsDirectory_Empty(string extensionsDirectory)
    {
        Assert.That(DistroXmlFileSource.DirectoriesFor(extensionsDirectory), Is.Empty);
    }

    [Test]
    public void DirectoriesFor_ExtensionsDirectory_RootThenRuntimeSubfolder()
    {
        var directories = DistroXmlFileSource.DirectoriesFor(_root);

#if NETFRAMEWORK
        const string runtime = "netframework";
#else
        const string runtime = "netcore";
#endif
        Assert.That(directories, Is.EqualTo(new[] { _root, Path.Combine(_root, runtime) }));
    }

    [Test]
    public void Constructor_NullDirectories_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistroXmlFileSource(null));
    }
}
