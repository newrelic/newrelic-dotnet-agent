// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;

namespace ArtifactBuilder.Tests;

[TestFixture]
public class DeterministicArchiveTests
{
    private static readonly DateTimeOffset _timestamp = new DateTimeOffset(2024, 5, 6, 12, 30, 41, TimeSpan.Zero);

    private string _workDirectory;

    [SetUp]
    public void SetUp()
    {
        _workDirectory = Path.Combine(Path.GetTempPath(), "ArtifactBuilderTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_workDirectory, true);
    }

    [Test]
    public void CreateFromDirectory_ProducesIdenticalBytesRegardlessOfFileModificationTimes()
    {
        var source = CreateSourceDirectory("source");
        var first = Path.Combine(_workDirectory, "first.zip");
        var second = Path.Combine(_workDirectory, "second.zip");

        DeterministicArchive.CreateFromDirectory(source, first, _timestamp);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, new DateTime(2001, 2, 3, 4, 5, 7, DateTimeKind.Utc));
        }

        DeterministicArchive.CreateFromDirectory(source, second, _timestamp);

        Assert.That(File.ReadAllBytes(second), Is.EqualTo(File.ReadAllBytes(first)));
    }

    [Test]
    public void CreateFromDirectory_WritesSortedEntriesWithFixedTimestampAndEmptyDirectories()
    {
        var source = CreateSourceDirectory("source");
        var zipPath = Path.Combine(_workDirectory, "out.zip");

        DeterministicArchive.CreateFromDirectory(source, zipPath, _timestamp);

        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(entry => entry.FullName).ToList();

        Assert.That(names, Is.EqualTo(new[] { "B.txt", "a.txt", "empty/", "sub/c.txt" }));
        Assert.That(archive.Entries.Select(entry => entry.LastWriteTime.DateTime), Has.All.EqualTo(new DateTime(2024, 5, 6, 12, 30, 40)));
        Assert.That(new StreamReader(archive.GetEntry("sub/c.txt").Open()).ReadToEnd(), Is.EqualTo("c"));
    }

    [Test]
    public void NormalizeNupkg_ProducesIdenticalBytesForPackagesDifferingOnlyInRandomNamesAndTimes()
    {
        var first = Path.Combine(_workDirectory, "first.nupkg");
        var second = Path.Combine(_workDirectory, "second.nupkg");
        CreateFakeNupkg(first, Guid.NewGuid().ToString("N"), new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero));
        CreateFakeNupkg(second, Guid.NewGuid().ToString("N"), new DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.Zero));

        Assert.That(File.ReadAllBytes(second), Is.Not.EqualTo(File.ReadAllBytes(first)));

        DeterministicArchive.NormalizeNupkg(first, _timestamp);
        DeterministicArchive.NormalizeNupkg(second, _timestamp);

        Assert.That(File.ReadAllBytes(second), Is.EqualTo(File.ReadAllBytes(first)));
    }

    [Test]
    public void NormalizeNupkg_KeepsContentAndPointsRelationshipAtRenamedCoreProperties()
    {
        var path = Path.Combine(_workDirectory, "pkg.nupkg");
        var randomName = Guid.NewGuid().ToString("N");
        CreateFakeNupkg(path, randomName, new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero));

        DeterministicArchive.NormalizeNupkg(path, _timestamp);

        using var archive = ZipFile.OpenRead(path);
        var names = archive.Entries.Select(entry => entry.FullName).ToList();
        var coreProperties = names.Single(name => name.EndsWith(".psmdcp"));
        var rels = new StreamReader(archive.GetEntry("_rels/.rels").Open()).ReadToEnd();

        Assert.That(names[0], Is.EqualTo("[Content_Types].xml"));
        Assert.That(coreProperties, Does.Not.Contain(randomName));
        Assert.That(rels, Does.Contain("Target=\"/" + coreProperties + "\""));
        Assert.That(rels, Does.Not.Contain(randomName));
        Assert.That(rels, Does.Contain("Target=\"/Pkg.nuspec\""));
        Assert.That(new StreamReader(archive.GetEntry("lib/net462/a.dll").Open()).ReadToEnd(), Is.EqualTo("dll"));
        Assert.That(archive.Entries.Select(entry => entry.LastWriteTime.DateTime), Has.All.EqualTo(new DateTime(2024, 5, 6, 12, 30, 40)));
    }

    [Test]
    public void NormalizeNupkg_ProducesIdenticalBytesWhenContentTypesListSameEntriesInDifferentOrder()
    {
        var first = Path.Combine(_workDirectory, "first.nupkg");
        var second = Path.Combine(_workDirectory, "second.nupkg");
        var entryTime = new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero);
        CreateFakeNupkg(first, "aaaa1111bbbb2222", entryTime, ContentTypes("dll", "xml", "config", "nuspec"));
        CreateFakeNupkg(second, "aaaa1111bbbb2222", entryTime, ContentTypes("nuspec", "config", "dll", "xml"));

        Assert.That(File.ReadAllBytes(second), Is.Not.EqualTo(File.ReadAllBytes(first)));

        DeterministicArchive.NormalizeNupkg(first, _timestamp);
        DeterministicArchive.NormalizeNupkg(second, _timestamp);

        Assert.That(File.ReadAllBytes(second), Is.EqualTo(File.ReadAllBytes(first)));
    }

    [Test]
    public void NormalizeNupkg_SortsContentTypesAndKeepsEveryEntry()
    {
        var path = Path.Combine(_workDirectory, "pkg.nupkg");
        CreateFakeNupkg(path, "aaaa1111bbbb2222", new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero), ContentTypes("xml", "dll", "config"));

        DeterministicArchive.NormalizeNupkg(path, _timestamp);

        using var archive = ZipFile.OpenRead(path);
        var document = XDocument.Load(archive.GetEntry("[Content_Types].xml").Open());
        var types = document.Root.Elements().ToList();

        Assert.That(types.Select(element => element.Name.LocalName), Is.EqualTo(new[] { "Default", "Default", "Default", "Override" }));
        Assert.That(types.Take(3).Select(element => element.Attribute("Extension").Value), Is.EqualTo(new[] { "config", "dll", "xml" }));
        Assert.That(types[3].Attribute("PartName").Value, Is.EqualTo("/Pkg.nuspec"));
        Assert.That(types[0].Attribute("ContentType").Value, Is.EqualTo("application/config"));
    }

    private static string ContentTypes(params string[] extensions)
    {
        var defaults = string.Concat(extensions.Select(extension => $"<Default Extension=\"{extension}\" ContentType=\"application/{extension}\" />"));

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
               "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
               defaults +
               "<Override PartName=\"/Pkg.nuspec\" ContentType=\"application/octet-stream\" />" +
               "</Types>";
    }

    private string CreateSourceDirectory(string name)
    {
        var root = Path.Combine(_workDirectory, name);
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        Directory.CreateDirectory(Path.Combine(root, "empty"));
        File.WriteAllText(Path.Combine(root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "B.txt"), "b");
        File.WriteAllText(Path.Combine(root, "sub", "c.txt"), "c");
        return root;
    }

    private static void CreateFakeNupkg(string path, string randomName, DateTimeOffset entryTime, string contentTypes = "<Types />")
    {
        var corePropertiesPath = $"package/services/metadata/core-properties/{randomName}.psmdcp";
        var rels = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Type=\"http://schemas.microsoft.com/packaging/2010/07/manifest\" Target=\"/Pkg.nuspec\" Id=\"R1\" />" +
                   $"<Relationship Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"/{corePropertiesPath}\" Id=\"R{randomName.Substring(0, 8)}\" />" +
                   "</Relationships>";

        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        void Add(string name, string content)
        {
            var entry = archive.CreateEntry(name);
            entry.LastWriteTime = entryTime;
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        Add("_rels/.rels", rels);
        Add("Pkg.nuspec", "<package />");
        Add("lib/net462/a.dll", "dll");
        Add("[Content_Types].xml", contentTypes);
        Add(corePropertiesPath, "<coreProperties />");
    }
}
