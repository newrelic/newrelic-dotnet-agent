// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class OtlpProfilesHttpDispatcherTests
{
    private const string Endpoint = "https://otlp.nr-data.net/v1/profiles";
    private const string LicenseKey = "0123456789abcdef0123456789abcdef01234567";

    private IConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(LicenseKey);
        Mock.Arrange(() => _configuration.CollectorTimeout).Returns(60000);
        Mock.Arrange(() => _configuration.ProxyHost).Returns((string)null);
    }

    [Test]
    public void SendTimeout_is_short_and_bounded_well_below_the_collector_timeout()
    {
        // CP sends are best-effort and never retried; a hung endpoint must not park the drain ThreadPool
        // thread for the 120s collector default. This regresses if someone rewires it back to CollectorTimeout.
        Assert.Multiple(() =>
        {
            Assert.That(OtlpProfilesHttpDispatcher.SendTimeout, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(OtlpProfilesHttpDispatcher.SendTimeout, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(30)));
        });
    }

    [Test]
    public void BuildRequestMessage_targets_the_configured_endpoint_with_post()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        using var message = dispatcher.BuildRequestMessage(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(message.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(message.RequestUri, Is.EqualTo(new Uri(Endpoint)));
        });
    }

    [Test]
    public void BuildRequestMessage_sets_the_protobuf_content_type()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        using var message = dispatcher.BuildRequestMessage(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.That(message.Content.Headers.ContentType.MediaType, Is.EqualTo("application/x-protobuf"));
    }

    [Test]
    public void BuildRequestMessage_sets_the_api_key_header_to_the_license_key()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        using var message = dispatcher.BuildRequestMessage(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.That(message.Headers.GetValues("api-key").Single(), Is.EqualTo(LicenseKey));
    }

    [Test]
    public void BuildRequestMessage_carries_the_serialized_body_bytes()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);
        var payload = new byte[] { 9, 8, 7, 6 };

        using var message = dispatcher.BuildRequestMessage(payload, Endpoint);

        var actual = message.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        Assert.That(actual, Is.EqualTo(payload));
    }

    [Test]
    public void Post_returns_false_and_does_not_throw_when_the_endpoint_is_missing()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1 }, null), Throws.Nothing);
        Assert.That(result.Accepted, Is.False);
    }

    [Test]
    public void Post_returns_false_and_does_not_throw_when_the_endpoint_is_not_a_valid_uri()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1 }, "not a uri"), Throws.Nothing);
        Assert.That(result.Accepted, Is.False);
    }

    [Test]
    public void Post_returns_false_and_swallows_a_transport_failure()
    {
        // A send delegate that throws simulates any HTTP/socket failure. Best-effort semantics:
        // the dispatcher must log-and-drop, returning false, never propagating the exception.
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration,
            _ => throw new HttpRequestException("connection refused"));

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1 }, Endpoint), Throws.Nothing);
        Assert.That(result.Accepted, Is.False);
    }

    [Test]
    public void Post_returns_true_on_a_successful_response()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.ResponseContent, Is.EqualTo("ok"));
        });
    }

    [Test]
    public void Post_returns_false_on_a_non_success_response()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("denied") };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(result.ResponseContent, Is.EqualTo("denied"));
        });
    }

    [Test]
    public void Post_sends_the_request_built_by_BuildRequestMessage()
    {
        HttpRequestMessage captured = null;
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, req => { captured = req; return response; });

        dispatcher.Post(new byte[] { 4, 2 }, Endpoint);

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(captured.RequestUri, Is.EqualTo(new Uri(Endpoint)));
            Assert.That(captured.Content.Headers.ContentType.MediaType, Is.EqualTo("application/x-protobuf"));
            Assert.That(captured.Headers.GetValues("api-key").Single(), Is.EqualTo(LicenseKey));
        });
    }
}
