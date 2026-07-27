// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class PlatformParserTests
{
    [TestCase("core", Platform.Core)]
    [TestCase("Core", Platform.Core)]
    [TestCase("framework", Platform.Framework)]
    public void Parse_MapsTabName(string tab, Platform expected)
    {
        Assert.That(PlatformParser.Parse(tab), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_UnknownTab_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => PlatformParser.Parse("dotnet"));
    }

    [TestCase("net462", Platform.Framework)]
    [TestCase("net471", Platform.Framework)]
    [TestCase("net48", Platform.Framework)]
    [TestCase("net481", Platform.Framework)]
    [TestCase("netcoreapp3.1", Platform.Core)]
    [TestCase("net8.0", Platform.Core)]
    [TestCase("net10.0", Platform.Core)]
    public void TfmToPlatform_MapsKnownTfms(string tfm, Platform expected)
    {
        Assert.That(PlatformParser.TfmToPlatform(tfm), Is.EqualTo(expected));
    }

    [TestCase("netstandard2.0")]
    [TestCase("garbage")]
    public void TfmToPlatform_UnknownTfm_ReturnsNull(string tfm)
    {
        Assert.That(PlatformParser.TfmToPlatform(tfm), Is.Null);
    }
}
