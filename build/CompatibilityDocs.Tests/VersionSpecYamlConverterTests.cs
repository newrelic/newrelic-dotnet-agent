// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CompatibilityDocs.Schema;
using NUnit.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class VersionSpecYamlConverterTests
{
    private sealed class Holder
    {
        public VersionSpec? MinVersion { get; set; }
    }

    private static Holder Deserialize(string yaml)
    {
        var d = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new VersionSpecYamlConverter())
            .IgnoreUnmatchedProperties()
            .Build();
        return d.Deserialize<Holder>(yaml)!;
    }

    [Test]
    public void Scalar_DeserializesToSingle()
    {
        var h = Deserialize("minVersion: \"3.17.0\"\n");

        Assert.That(h.MinVersion, Is.Not.Null);
        Assert.That(h.MinVersion!.IsMap, Is.False);
        Assert.That(h.MinVersion.For("framework"), Is.EqualTo("3.17.0"));
    }

    [Test]
    public void Map_DeserializesToPerTab()
    {
        var h = Deserialize("minVersion:\n  core: \"3.2.0\"\n  framework: \"2.0.0\"\n");

        Assert.That(h.MinVersion!.IsMap, Is.True);
        Assert.That(h.MinVersion.For("core"), Is.EqualTo("3.2.0"));
        Assert.That(h.MinVersion.For("framework"), Is.EqualTo("2.0.0"));
    }
}
