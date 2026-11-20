// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroActivitySourceFilterTests
{
    private IConfiguration _config;
    private List<string> _customerIncluded;
    private List<string> _customerExcluded;

    [SetUp]
    public void SetUp()
    {
        _config = Mock.Create<IConfiguration>();
        _customerIncluded = new List<string>();
        _customerExcluded = new List<string>();

        Mock.Arrange(() => _config.OpenTelemetryTracingDefaultExcludedActivitySources).Returns(new List<string> { "Hybrid.Default.Excluded" });
        Mock.Arrange(() => _config.OpenTelemetryTracingIncludedActivitySources).Returns(_customerIncluded);
        Mock.Arrange(() => _config.OpenTelemetryTracingExcludedActivitySources).Returns(_customerExcluded);
    }

    [Test]
    public void ShouldListenTo_HybridDefaultEntry_IsExcluded()
    {
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Hybrid.Default.Excluded", _config), Is.False);
    }

    [Test]
    public void ShouldListenTo_GeneratedAddition_IsExcluded()
    {
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.False);
    }

    [Test]
    public void ShouldListenTo_AdditionMatchIsCaseInsensitive()
    {
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("DISTRO.addition", _config), Is.False);
    }

    [Test]
    public void ShouldListenTo_SourceInNeitherList_IsListenedTo()
    {
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Some.Other.Source", _config), Is.True);
    }

    [Test]
    public void ShouldListenTo_NoAdditions_IsExactlyTheHybridList()
    {
        var filter = new DistroActivitySourceFilter(new string[0]);

        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldListenTo("Hybrid.Default.Excluded", _config), Is.False);
            Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.True);
            Assert.That(filter.ShouldListenTo("Some.Other.Source", _config), Is.True);
        });
    }

    [Test]
    public void ShouldListenTo_NullAdditions_BehavesAsEmpty()
    {
        var filter = new DistroActivitySourceFilter(null);

        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldListenTo("Hybrid.Default.Excluded", _config), Is.False);
            Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.True);
        });
    }

    [Test]
    public void ShouldListenTo_NullAndBlankAdditionEntries_AreIgnored()
    {
        var filter = new DistroActivitySourceFilter(new[] { null, "", "  ", "Distro.Addition" });

        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.False);
            Assert.That(filter.ShouldListenTo("Some.Other.Source", _config), Is.True);
            Assert.That(filter.ShouldListenTo("", _config), Is.True);
        });
    }

    [Test]
    public void ShouldListenTo_AdditionIncludedByCustomer_IsListenedTo()
    {
        _customerIncluded.Add("Distro.Addition");
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.True);
    }

    [Test]
    public void ShouldListenTo_AdditionIncludedAndExcludedByCustomer_CustomerExcludeWins()
    {
        _customerIncluded.Add("Distro.Addition");
        _customerExcluded.Add("Distro.Addition");
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Distro.Addition", _config), Is.False);
    }

    [Test]
    public void ShouldListenTo_HybridDefaultIncludedByCustomer_IsListenedTo()
    {
        _customerIncluded.Add("Hybrid.Default.Excluded");
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Hybrid.Default.Excluded", _config), Is.True);
    }

    [Test]
    public void ShouldListenTo_CustomerExcludedSource_IsExcluded()
    {
        _customerExcluded.Add("Customer.Excluded");
        var filter = new DistroActivitySourceFilter(new[] { "Distro.Addition" });

        Assert.That(filter.ShouldListenTo("Customer.Excluded", _config), Is.False);
    }

    [Test]
    public void Additions_ReturnsTheConfiguredNames()
    {
        var filter = new DistroActivitySourceFilter(new[] { "A", "", "B" });

        Assert.That(filter.Additions, Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void Additions_AreSnapshottedAtConstruction()
    {
        var source = new List<string> { "A" };
        var filter = new DistroActivitySourceFilter(source);
        source.Add("B");

        Assert.Multiple(() =>
        {
            Assert.That(filter.Additions, Is.EquivalentTo(new[] { "A" }));
            Assert.That(filter.ShouldListenTo("B", _config), Is.True);
        });
    }
}
