// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class ShippedXmlFileNamesTests
{
    [Test]
    public void TryGetHomeName_WrapperSource_MapsToHomeName()
    {
        var found = ShippedXmlFileNames.TryGetHomeName(@"C:\repo\src\Agent\NewRelic\Agent\Extensions\Providers\Wrapper\Sql\Instrumentation.xml", out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(homeName, Is.EqualTo("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml"));
        });
    }

    [Test]
    public void TryGetHomeName_ForwardSlashes()
    {
        var found = ShippedXmlFileNames.TryGetHomeName("/repo/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/HttpClient/Instrumentation.xml", out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(homeName, Is.EqualTo("NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml"));
        });
    }

    [Test]
    public void TryGetHomeName_RelativeWrapperSourceInAnyCase_MapsToHomeName()
    {
        var found = ShippedXmlFileNames.TryGetHomeName(@"..\..\Agent\Extensions\Providers\wrapper\Couchbase3\instrumentation.XML", out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(homeName, Is.EqualTo("NewRelic.Providers.Wrapper.Couchbase3.Instrumentation.xml"));
        });
    }

    [Test]
    public void TryGetHomeName_MiscKeepsName()
    {
        var found = ShippedXmlFileNames.TryGetHomeName(@"C:\repo\src\Agent\Miscellaneous\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(homeName, Is.EqualTo("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"));
        });
    }

    [Test]
    public void TryGetHomeName_HomeCopyKeepsName()
    {
        // The plugin's default build feeds the home's extensions folder, whose files already carry home names.
        var found = ShippedXmlFileNames.TryGetHomeName(@"C:\repo\src\Agent\newrelichome_x64_coreclr\extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(homeName, Is.EqualTo("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml"));
        });
    }

    [TestCase(@"C:\repo\src\Agent\NewRelic\OtelDistro\NewRelic.OtelDistro.Plugin\Instrumentation\Step0.xml")]
    [TestCase("/repo/src/Agent/NewRelic/OtelDistro/NewRelic.OtelDistro.Plugin/Instrumentation/AsyncProof.xml")]
    [TestCase(@"C:\repo\src\Agent\NewRelic\OtelDistro\newrelic.oteldistro.plugin\instrumentation\VersionRangesProof.xml")]
    [TestCase(@"NewRelic.OtelDistro.Plugin\Instrumentation\Couchbase3Exerciser.xml")]
    public void TryGetHomeName_PluginInstrumentationFolder_False(string path)
    {
        var found = ShippedXmlFileNames.TryGetHomeName(path, out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(homeName, Is.Null);
        });
    }

    [TestCase(@"C:\repo\elsewhere\Instrumentation.xml")]
    [TestCase("Instrumentation.xml")]
    [TestCase(@"C:\repo\Wrapper\Instrumentation.xml")]
    [TestCase(@"C:\repo\Wrapper\\Instrumentation.xml")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase(@"C:\repo\folder\")]
    public void TryGetHomeName_NoHomeNameKnown_False(string path)
    {
        var found = ShippedXmlFileNames.TryGetHomeName(path, out var homeName);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(homeName, Is.Null);
        });
    }
}
