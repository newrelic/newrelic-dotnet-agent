// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class VersionSpecTests
{
    [Test]
    public void Single_ResolvesForAnyTab()
    {
        var spec = VersionSpec.Single("3.17.0");

        Assert.That(spec.For("core"), Is.EqualTo("3.17.0"));
        Assert.That(spec.For("framework"), Is.EqualTo("3.17.0"));
        Assert.That(spec.IsMap, Is.False);
    }

    [Test]
    public void Map_ResolvesPerTab_AndReturnsNullForUndeclaredTab()
    {
        var spec = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "3.2.0", ["framework"] = "2.0.0" });

        Assert.That(spec.For("core"), Is.EqualTo("3.2.0"));
        Assert.That(spec.For("framework"), Is.EqualTo("2.0.0"));
        Assert.That(spec.IsMap, Is.True);
        Assert.That(spec.Tabs, Is.EquivalentTo(new[] { "core", "framework" }));
    }

    [Test]
    public void Map_MissingTab_ReturnsNull()
    {
        var spec = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "8.0.0" });

        Assert.That(spec.For("framework"), Is.Null);
    }
}
