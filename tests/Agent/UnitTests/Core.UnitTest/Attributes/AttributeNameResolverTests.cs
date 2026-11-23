// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class AttributeNameResolverTests
{
    private static AttributeFilter CreateFilter(AttributeFilter.Settings settings = null)
    {
        return new AttributeFilter(settings ?? new AttributeFilter.Settings());
    }

    [TestCase(AttributeDestinations.All, (AttributeDestinations)255)]
    [TestCase(AttributeDestinations.SpanEvent, (AttributeDestinations)64)]
    [TestCase(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, (AttributeDestinations)20)]
    [TestCase(AttributeDestinations.CustomEvent, (AttributeDestinations)128)]
    public void ResolveCustom_AllDestinationsEnabled(AttributeDestinations family, AttributeDestinations expected)
    {
        var resolved = AttributeNameResolver.ResolveCustom(CreateFilter(), "foo", family);

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Name, Is.EqualTo("foo"));
            Assert.That(resolved.Destinations, Is.EqualTo(expected));
        });
    }

    [Test]
    public void ResolveCustom_BrowserAttributesDisabled_AllFamilyExcludesJavaScriptAgent()
    {
        var filter = CreateFilter(new AttributeFilter.Settings { JavaScriptAgentEnabled = false });

        var resolved = AttributeNameResolver.ResolveCustom(filter, "foo", AttributeDestinations.All);

        Assert.That(resolved.Destinations, Is.EqualTo((AttributeDestinations)247));
    }

    [Test]
    public void ResolveCustom_ExcludeRule()
    {
        var filter = CreateFilter(new AttributeFilter.Settings { TransactionEventExcludes = new[] { "foo" } });

        var foo = AttributeNameResolver.ResolveCustom(filter, "foo", AttributeDestinations.All);
        var bar = AttributeNameResolver.ResolveCustom(filter, "bar", AttributeDestinations.All);

        Assert.Multiple(() =>
        {
            Assert.That(foo.Destinations & AttributeDestinations.TransactionEvent, Is.EqualTo(AttributeDestinations.None));
            Assert.That(foo.Destinations & AttributeDestinations.TransactionTrace, Is.EqualTo(AttributeDestinations.TransactionTrace));
            Assert.That(bar.Destinations & AttributeDestinations.TransactionEvent, Is.EqualTo(AttributeDestinations.TransactionEvent));
        });
    }

    [Test]
    public void ResolveCustom_WildcardExclude()
    {
        var filter = CreateFilter(new AttributeFilter.Settings { TransactionEventExcludes = new[] { "fo*" } });

        var foo = AttributeNameResolver.ResolveCustom(filter, "foo", AttributeDestinations.All);
        var fox = AttributeNameResolver.ResolveCustom(filter, "fox", AttributeDestinations.All);
        var bar = AttributeNameResolver.ResolveCustom(filter, "bar", AttributeDestinations.All);

        Assert.Multiple(() =>
        {
            Assert.That(foo.Destinations & AttributeDestinations.TransactionEvent, Is.EqualTo(AttributeDestinations.None));
            Assert.That(fox.Destinations & AttributeDestinations.TransactionEvent, Is.EqualTo(AttributeDestinations.None));
            Assert.That(bar.Destinations & AttributeDestinations.TransactionEvent, Is.EqualTo(AttributeDestinations.TransactionEvent));
        });
    }

    [TestCase("request.parameters.")]
    [TestCase("request.headers.")]
    public void ResolvePrefixed_DefaultConfig_IsNone(string prefix)
    {
        var resolved = AttributeNameResolver.ResolvePrefixed(CreateFilter(), prefix, "foo");

        Assert.That(resolved.Destinations, Is.EqualTo(AttributeDestinations.None));
    }

    [Test]
    public void ResolvePrefixed_WildcardInclude()
    {
        var filter = CreateFilter(new AttributeFilter.Settings { Includes = new[] { "request.parameters.*" } });

        var resolved = AttributeNameResolver.ResolvePrefixed(filter, "request.parameters.", "foo");
        var header = AttributeNameResolver.ResolvePrefixed(filter, "request.headers.", "foo");

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Destinations, Is.EqualTo(
                AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace
                | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent));
            Assert.That(header.Destinations, Is.EqualTo(AttributeDestinations.None));
        });
    }

    [Test]
    public void ResolvePrefixed_Name()
    {
        var resolved = AttributeNameResolver.ResolvePrefixed(CreateFilter(), "request.headers.", "x-foo");

        Assert.That(resolved.Name, Is.EqualTo("request.headers.x-foo"));
    }

    [Test]
    public void ResolvePrefixed_ShouldFilterAttribute_RemovesDestination()
    {
        var settings = new AttributeFilter.Settings { Includes = new[] { "request.parameters.*" }, TransactionTraceEnabled = false };

        var resolved = AttributeNameResolver.ResolvePrefixed(CreateFilter(settings), "request.parameters.", "foo");

        Assert.That(resolved.Destinations, Is.EqualTo(
            AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent));
    }

    [Test]
    public void Resolve_FilterCallOrder()
    {
        const string full = "request.parameters.foo";
        var calls = new List<(string Name, AttributeDestinations Default, AttributeDestinations Destination)>();
        var filter = Mock.Create<IAttributeFilter>();
        Mock.Arrange(() => filter.ShouldFilterAttribute(Arg.IsAny<AttributeDestinations>())).Returns(false);
        Mock.Arrange(() => filter.CheckOrAddAttributeClusionCache(Arg.IsAny<string>(), Arg.IsAny<AttributeDestinations>(), Arg.IsAny<AttributeDestinations>()))
            .Returns((string name, AttributeDestinations defaults, AttributeDestinations destination) =>
            {
                calls.Add((name, defaults, destination));
                return destination != AttributeDestinations.ErrorTrace && destination != AttributeDestinations.SpanEvent;
            });

        var resolved = AttributeNameResolver.ResolvePrefixed(filter, "request.parameters.", "foo");

        var expected = new List<(string, AttributeDestinations, AttributeDestinations)>
        {
            (full, AttributeDestinations.None, AttributeDestinations.TransactionEvent),
            (full, AttributeDestinations.None, AttributeDestinations.TransactionTrace),
            (full, AttributeDestinations.None, AttributeDestinations.ErrorTrace),
            (full, AttributeDestinations.None, AttributeDestinations.ErrorEvent),
            (full, AttributeDestinations.None, AttributeDestinations.SpanEvent),
            (full, AttributeDestinations.TransactionEvent, AttributeDestinations.TransactionEvent),
            (full, AttributeDestinations.TransactionTrace, AttributeDestinations.TransactionTrace),
            (full, AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorEvent),
        };

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(expected));
            Assert.That(resolved.Destinations, Is.EqualTo(
                AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent));
        });
    }
}
