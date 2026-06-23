// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Providers.Wrapper;

[TestFixture]
public class TracingHeaderExistenceTests
{
    [Test]
    public void ContainsTracingHeader_ReturnsFalse_ForNull()
    {
        Assert.That(TracingHeaderExistence.ContainsTracingHeader(null), Is.False);
    }

    [Test]
    public void ContainsTracingHeader_ReturnsFalse_ForEmpty()
    {
        Assert.That(TracingHeaderExistence.ContainsTracingHeader(new string[0]), Is.False);
    }

    [Test]
    public void ContainsTracingHeader_ReturnsFalse_WhenNoTracingHeaders()
    {
        var keys = new[] { "Content-Type", "Accept", "Host", "User-Agent" };
        Assert.That(TracingHeaderExistence.ContainsTracingHeader(keys), Is.False);
    }

    [TestCase("traceparent")]
    [TestCase("TraceParent")]
    [TestCase("tracestate")]
    [TestCase("newrelic")]
    [TestCase("NEWRELIC")]
    [TestCase("Newrelic")]
    [TestCase("X-NewRelic-ID")]
    [TestCase("x-newrelic-id")]
    [TestCase("X-NewRelic-Transaction")]
    public void ContainsTracingHeader_ReturnsTrue_ForTracingHeader(string headerKey)
    {
        var keys = new[] { "Content-Type", headerKey, "Accept" };
        Assert.That(TracingHeaderExistence.ContainsTracingHeader(keys), Is.True);
    }

    [Test]
    public void ContainsTracingHeader_IgnoresNullAndEmptyKeys()
    {
        var keys = new[] { null, "", "traceparent" };
        Assert.That(TracingHeaderExistence.ContainsTracingHeader(keys), Is.True);
    }
}
