// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport.Client;

[TestFixture]
public class HttpResponseHeaderFormatterTests
{
    [Test]
    public void Format_ReturnsNoneSentinel_WhenHeadersAreNull()
    {
        var result = HttpResponseHeaderFormatter.Format((IEnumerable<KeyValuePair<string, IEnumerable<string>>>)null);

        Assert.That(result, Is.EqualTo("(none)"));
    }

    [Test]
    public void Format_ReturnsNoneSentinel_WhenHeadersAreEmpty()
    {
        var result = HttpResponseHeaderFormatter.Format(new List<KeyValuePair<string, IEnumerable<string>>>());

        Assert.That(result, Is.EqualTo("(none)"));
    }

    [Test]
    public void Format_FormatsSingleHeaderWithSingleValue()
    {
        var headers = new List<KeyValuePair<string, IEnumerable<string>>>
        {
            new("cf-ray", new[] { "abc123-DFW" })
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Is.EqualTo("cf-ray=[abc123-DFW]"));
    }

    [Test]
    public void Format_JoinsMultipleValuesForOneHeader()
    {
        var headers = new List<KeyValuePair<string, IEnumerable<string>>>
        {
            new("x-request-id", new[] { "id-1", "id-2" })
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Is.EqualTo("x-request-id=[id-1, id-2]"));
    }

    [Test]
    public void Format_JoinsMultipleHeadersWithSemicolon()
    {
        var headers = new List<KeyValuePair<string, IEnumerable<string>>>
        {
            new("cf-ray", new[] { "abc123-DFW" }),
            new("x-request-id", new[] { "id-1" })
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Is.EqualTo("cf-ray=[abc123-DFW]; x-request-id=[id-1]"));
    }

    [TestCase("WWW-Authenticate")]
    [TestCase("Authorization")]
    [TestCase("X-Auth-Token")]
    [TestCase("x-auth-token")]
    [TestCase("X-Api-Key")]
    [TestCase("x-api-key")]
    [TestCase("access-token")]
    [TestCase("refresh_token")]
    public void Format_RedactsValue_ForSensitiveHeaderName(string headerName)
    {
        var headers = new List<KeyValuePair<string, IEnumerable<string>>>
        {
            new(headerName, new[] { "super-secret-value" })
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Does.Contain($"{headerName}=[REDACTED]"));
        Assert.That(result, Does.Not.Contain("super-secret-value"));
    }

    [Test]
    public void Format_OnlyRedactsSensitiveHeaders_LeavingOthersIntact()
    {
        var headers = new List<KeyValuePair<string, IEnumerable<string>>>
        {
            new("cf-ray", new[] { "abc123-DFW" }),
            new("WWW-Authenticate", new[] { "Bearer realm=\"collector\"" }),
            new("x-request-id", new[] { "id-1" })
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Does.Contain("cf-ray=[abc123-DFW]"));
        Assert.That(result, Does.Contain("WWW-Authenticate=[REDACTED]"));
        Assert.That(result, Does.Contain("x-request-id=[id-1]"));
        Assert.That(result, Does.Not.Contain("Bearer"));
    }

#if NETFRAMEWORK
    [Test]
    public void Format_WebHeaderCollection_FormatsHeaders()
    {
        var headers = new System.Net.WebHeaderCollection
        {
            { "cf-ray", "abc123-DFW" },
            { "x-request-id", "id-1" }
        };

        var result = HttpResponseHeaderFormatter.Format(headers);

        Assert.That(result, Does.Contain("cf-ray=[abc123-DFW]"));
        Assert.That(result, Does.Contain("x-request-id=[id-1]"));
    }

    [Test]
    public void Format_WebHeaderCollection_ReturnsNoneSentinel_WhenEmpty()
    {
        var result = HttpResponseHeaderFormatter.Format(new System.Net.WebHeaderCollection());

        Assert.That(result, Is.EqualTo("(none)"));
    }
#endif
}
