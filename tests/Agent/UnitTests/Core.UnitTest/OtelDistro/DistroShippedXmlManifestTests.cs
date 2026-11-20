// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroShippedXmlManifestTests
{
    [TestCase("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", 1)]
    [TestCase("newrelic.providers.wrapper.sql.instrumentation.xml", 1)]
    [TestCase("NEWRELIC.PROVIDERS.WRAPPER.SQL.INSTRUMENTATION.XML", 1)]
    [TestCase("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", 0)]
    public void Manifest_TryFindFile_CaseInsensitive(string homeName, int expectedFile)
    {
        var manifest = ReadManifest();

        var found = manifest.TryFindFile(homeName, out var file);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(file, Is.EqualTo(expectedFile));
        });
    }

    [TestCase("Copy of NewRelic.Providers.Wrapper.Sql.Instrumentation.xml")]
    [TestCase("NewRelic.Providers.Wrapper.Sql.Instrumentation")]
    [TestCase("")]
    [TestCase(null)]
    public void Manifest_TryFindFile_UnknownName_ReturnsFalse(string homeName)
    {
        var manifest = ReadManifest();

        var found = manifest.TryFindFile(homeName, out var file);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(file, Is.EqualTo(-1));
        });
    }

    [Test]
    public void Manifest_PerFileValues_InFileOrder()
    {
        var manifest = ReadManifest();

        Assert.Multiple(() =>
        {
            Assert.That(manifest.FileCount, Is.EqualTo(2));
            Assert.That(manifest.HomeName(0), Is.EqualTo("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(0x8000000000000000UL));
            Assert.That(manifest.IsCompiled(0), Is.False);
            Assert.That(manifest.HomeName(1), Is.EqualTo("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(1), Is.EqualTo(42UL));
            Assert.That(manifest.IsCompiled(1), Is.True);
        });
    }

    [TestCase(-1)]
    [TestCase(2)] // FileCount
    public void Manifest_PerFileReadersOutOfRange_Throw(int file)
    {
        var manifest = ReadManifest();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => manifest.HomeName(file));
            Assert.Throws<ArgumentOutOfRangeException>(() => manifest.ContentHash(file));
            Assert.Throws<ArgumentOutOfRangeException>(() => manifest.IsCompiled(file));
        });
    }

    private static DistroShippedXmlManifest ReadManifest()
    {
        var files = new[]
        {
            new ShippedFile { HomeName = "NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", ContentHash = 0x8000000000000000, Compiled = false },
            new ShippedFile { HomeName = "NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", ContentHash = 42, Compiled = true },
        };
        var table = Build(files, new Row { SourceFile = 1 });
        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out _, out var manifest, out var failureReason);
        Assert.That(ok, Is.True, () => failureReason + System.Environment.NewLine + Dump(table));
        return manifest;
    }
}
