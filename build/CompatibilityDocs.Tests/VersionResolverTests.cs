using System.Collections.Generic;
using CompatibilityDocs.Derivation;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class VersionResolverTests
{
    [Test]
    public void Resolve_ComputesLatestPerPlatform()
    {
        var refs = new List<PackageRef>
        {
            new("Elastic.Clients.Elasticsearch", "8.0.0", "net462"),
            new("Elastic.Clients.Elasticsearch", "8.18.3", "net481"),
            new("Elastic.Clients.Elasticsearch", "8.0.0", "net8.0"),
            new("Elastic.Clients.Elasticsearch", "9.0.7", "net10.0"),
        };

        var index = new VersionResolver().BuildIndex(refs);

        Assert.That(index[("elastic.clients.elasticsearch", Platform.Framework)], Is.EqualTo("8.18.3"));
        Assert.That(index[("elastic.clients.elasticsearch", Platform.Core)], Is.EqualTo("9.0.7"));
    }

    [Test]
    public void Resolve_NoCondition_CountsForBothPlatforms()
    {
        var refs = new List<PackageRef> { new("Hangfire", "1.8.23", null) };
        var index = new VersionResolver().BuildIndex(refs);

        Assert.That(index[("hangfire", Platform.Core)], Is.EqualTo("1.8.23"));
        Assert.That(index[("hangfire", Platform.Framework)], Is.EqualTo("1.8.23"));
    }

    [Test]
    public void Resolve_PackageIdMatchingIsCaseInsensitive()
    {
        var refs = new List<PackageRef>
        {
            new("npgsql", "4.0.0", "net481"),
            new("Npgsql", "7.0.7", "net481"),
        };
        var index = new VersionResolver().BuildIndex(refs);

        Assert.That(index[("npgsql", Platform.Framework)], Is.EqualTo("7.0.7"));
    }

    [Test]
    public void Resolve_PrereleaseAndFourPartVersionsCompareCorrectly()
    {
        var refs = new List<PackageRef>
        {
            new("AWSSDK.BedrockRuntime", "3.7.200.0", "net10.0"),
            new("AWSSDK.BedrockRuntime", "4.0.20.1", "net10.0"),
        };
        var index = new VersionResolver().BuildIndex(refs);

        Assert.That(index[("awssdk.bedrockruntime", Platform.Core)], Is.EqualTo("4.0.20.1"));
    }

    [Test]
    public void Resolve_UnknownTfm_IsIgnored()
    {
        var refs = new List<PackageRef> { new("Foo", "1.0.0", "netstandard2.0") };
        var index = new VersionResolver().BuildIndex(refs);

        Assert.That(index, Is.Empty);
    }
}
