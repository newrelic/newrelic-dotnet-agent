// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Metrics;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.DataTransport.ContinuousProfiling;

[TestFixture]
public class OtlpProfilesHttpDispatcherTests
{
    private const string Endpoint = "https://otlp.nr-data.net/v1/profiles";
    private const string FakeLicenseKey = "0123456789abcdef0123456789abcdef01234567";

    private IConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(FakeLicenseKey);
        Mock.Arrange(() => _configuration.CollectorTimeout).Returns(60000);
        Mock.Arrange(() => _configuration.ProxyHost).Returns((string)null);
    }

    [Test]
    public void AttemptConnectTimeout_is_short_and_bounded_well_below_the_collector_timeout()
    {
        // Per-attempt connect bound must stay well under the 120s collector default --
        // a hung connect on one retry attempt must not itself eat the whole budget.
        Assert.Multiple(() =>
        {
            Assert.That(OtlpProfilesHttpDispatcher.AttemptConnectTimeout, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(OtlpProfilesHttpDispatcher.AttemptConnectTimeout, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(30)));
        });
    }

    // ContinuousProfilingService's bounded drain-shutdown wait (the _drainShutdownWaitTimeout field,
    // 60s by default) is not exposed as a public constant, so it can't be referenced directly here.
    // Asserting against it by name, keeping this comment as the tripwire: if that default ever
    // changes, update this value too so the margin assertion below stays meaningful.
    private static readonly TimeSpan KnownDrainShutdownWaitTimeoutDefault = TimeSpan.FromSeconds(60);

    [Test]
    public void TotalSendTimeoutWithRetries_covers_the_full_multi_attempt_budget()
    {
        // Must be strictly larger than a single AttemptConnectTimeout (room for retries + backoff)
        // and stay well under ContinuousProfilingService's drain-shutdown wait default so Dispose's
        // bounded wait for an in-flight drain always has margin over this send-side ceiling.
        Assert.Multiple(() =>
        {
            Assert.That(OtlpProfilesHttpDispatcher.TotalSendTimeoutWithRetries,
                Is.GreaterThan(OtlpProfilesHttpDispatcher.AttemptConnectTimeout));
            Assert.That(OtlpProfilesHttpDispatcher.TotalSendTimeoutWithRetries,
                Is.LessThan(KnownDrainShutdownWaitTimeoutDefault));
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
    public void BuildRequestMessage_sets_a_user_agent_header_identifying_the_dotnet_agent()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        using var message = dispatcher.BuildRequestMessage(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.That(message.Headers.GetValues("User-Agent").Single(), Does.StartWith("NewRelic-DotNet-Agent/"));
    }

    [Test]
    public void BuildRequestMessage_sets_the_api_key_header_to_the_license_key()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        using var message = dispatcher.BuildRequestMessage(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.That(message.Headers.GetValues("api-key").Single(), Is.EqualTo(FakeLicenseKey));
    }

    [Test]
    public void BuildRequestMessage_carries_the_serialized_body_bytes_gzip_compressed()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);
        var payload = new byte[] { 9, 8, 7, 6 };

        using var message = dispatcher.BuildRequestMessage(payload, Endpoint);

        Assert.That(message.Content.Headers.ContentEncoding, Does.Contain("gzip"));

        var compressed = message.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        using var compressedStream = new MemoryStream(compressed);
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzip.CopyTo(decompressed);

        Assert.That(decompressed.ToArray(), Is.EqualTo(payload));
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
            Assert.That(captured.Headers.GetValues("api-key").Single(), Is.EqualTo(FakeLicenseKey));
        });
    }

    [Test]
    public void Post_parses_partial_success_from_a_protobuf_response()
    {
        var protobufResponse = new ExportProfilesServiceResponse
        {
            PartialSuccess = new ExportProfilesPartialSuccess { RejectedProfiles = 3, ErrorMessage = "schema drift" }
        };
        using var response = BuildProtobufResponse(HttpStatusCode.OK, protobufResponse.ToByteArray());
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True, "Partial success must not flip Accepted -- it is diagnostics only.");
            Assert.That(result.RejectedProfiles, Is.EqualTo(3));
            Assert.That(result.PartialSuccessErrorMessage, Is.EqualTo("schema drift"));
        });
    }

    [Test]
    public void Post_reports_no_partial_success_for_an_empty_response_body()
    {
        var protobufResponse = new ExportProfilesServiceResponse();
        using var response = BuildProtobufResponse(HttpStatusCode.OK, protobufResponse.ToByteArray());
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.RejectedProfiles, Is.EqualTo(0));
            Assert.That(result.PartialSuccessErrorMessage, Is.Empty);
        });
    }

    [Test]
    public void Post_does_not_attempt_to_parse_a_non_protobuf_response_body()
    {
        // A proxy/error page (plain text here) is not OTLP -- must not be fed to the protobuf parser.
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.RejectedProfiles, Is.EqualTo(0));
            Assert.That(result.PartialSuccessErrorMessage, Is.Empty);
        });
    }

    [Test]
    public void Post_does_not_throw_when_a_protobuf_content_typed_body_is_not_actually_valid_protobuf()
    {
        using var response = BuildProtobufResponse(HttpStatusCode.OK, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RejectedProfiles, Is.EqualTo(0));
        });
    }

    private static HttpResponseMessage BuildProtobufResponse(HttpStatusCode statusCode, byte[] bodyBytes)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(bodyBytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        return response;
    }

    [Test]
    public void Post_truncates_a_response_body_larger_than_the_cap()
    {
        var hugeBody = new byte[OtlpProfilesHttpDispatcher.MaxResponseBodyBytes + 1024];
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(hugeBody) };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(Encoding.UTF8.GetBytes(result.ResponseContent).Length, Is.LessThanOrEqualTo(OtlpProfilesHttpDispatcher.MaxResponseBodyBytes));
        });
    }

    [Test]
    public void Post_does_not_read_the_body_into_memory_when_content_length_declares_it_oversized()
    {
        // ResponseContent stays empty despite a 200 -- a huge declared Content-Length skips buffering
        // the body, but (see next test) the stream is still drained rather than abandoned.
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
        response.Content.Headers.ContentLength = OtlpProfilesHttpDispatcher.MaxResponseBodyBytes + 1;
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True, "An oversized-but-accepted response must not be misreported as a dropped batch.");
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.ResponseContent, Is.Empty);
        });
    }

    [Test]
    public void Post_drains_the_response_stream_to_eof_when_content_length_declares_it_oversized()
    {
        // A poisoned connection would surface here in the real pipeline as a broken pooled connection;
        // this test asserts the seam that prevents it -- the content stream is read to completion (not
        // aborted mid-read) even though the declared Content-Length short-circuits buffering the body.
        using var trackingContent = new StreamTrackingContent(new byte[] { 1, 2, 3 });
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = trackingContent };
        response.Content.Headers.ContentLength = OtlpProfilesHttpDispatcher.MaxResponseBodyBytes + 1;
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        dispatcher.Post(new byte[] { 1 }, Endpoint);

        Assert.That(trackingContent.ReadToEnd, Is.True);
    }

    [Test]
    public void Post_drains_the_response_stream_to_eof_when_the_actual_body_exceeds_the_cap()
    {
        var hugeBody = new byte[OtlpProfilesHttpDispatcher.MaxResponseBodyBytes + 1024];
        using var trackingContent = new StreamTrackingContent(hugeBody);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = trackingContent };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        dispatcher.Post(new byte[] { 1 }, Endpoint);

        Assert.That(trackingContent.ReadToEnd, Is.True);
    }

    // Wraps ByteArrayContent's stream so a test can observe whether the dispatcher read all the way to
    // EOF (ReadToEnd becomes true only once a 0-byte read is returned) rather than stopping partway
    // through once its size cap is hit.
    private class StreamTrackingContent : HttpContent
    {
        private readonly byte[] _body;
        public bool ReadToEnd { get; private set; }

        public StreamTrackingContent(byte[] body) => _body = body;

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new TrackingStream(_body, this));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => stream.WriteAsync(_body, 0, _body.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = _body.Length;
            return true;
        }

        private class TrackingStream : MemoryStream
        {
            private readonly StreamTrackingContent _owner;
            public TrackingStream(byte[] body, StreamTrackingContent owner) : base(body) => _owner = owner;

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = base.Read(buffer, offset, count);
                if (read == 0)
                    _owner.ReadToEnd = true;
                return read;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var read = base.Read(buffer, offset, count);
                if (read == 0)
                    _owner.ReadToEnd = true;
                return Task.FromResult(read);
            }
        }
    }

    [Test]
    public void Post_does_not_parse_partial_success_from_a_truncated_protobuf_body()
    {
        // The body is a fully valid, parseable protobuf message with a non-zero RejectedProfiles --
        // proving the parser would otherwise happily decode it -- but Content-Length declares it
        // oversized, forcing truncation. Partial-success fields must stay at their zero defaults.
        var protobufResponse = new ExportProfilesServiceResponse
        {
            PartialSuccess = new ExportProfilesPartialSuccess { RejectedProfiles = 3, ErrorMessage = "schema drift" }
        };
        var bodyBytes = protobufResponse.ToByteArray();
        using var response = BuildProtobufResponse(HttpStatusCode.OK, bodyBytes);
        response.Content.Headers.ContentLength = OtlpProfilesHttpDispatcher.MaxResponseBodyBytes + 1;
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => response);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.RejectedProfiles, Is.EqualTo(0));
            Assert.That(result.PartialSuccessErrorMessage, Is.Empty);
        });
    }

    [Test]
    public void Post_using_the_real_send_pipeline_does_not_throw_for_a_malformed_endpoint()
    {
        // Only exercises CreateHandler/CreateRealSend construction via the public single-arg
        // constructor; BuildRequestMessage validation short-circuits before any socket work for a
        // malformed endpoint, same guard Post_returns_false_and_does_not_throw_when_the_endpoint_is_
        // not_a_valid_uri already relies on -- CreateRealSend's CustomRetryHandler/HttpClient chain is
        // never actually invoked here. See the "real pipeline" tests below for wiring coverage.
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1 }, "not a uri"), Throws.Nothing);
        Assert.That(result.Accepted, Is.False);
    }

    #region Lazy real-pipeline construction (L20)

    // These prove the real CreateRealSend pipeline (ConnectionInfo -> reflected SocketsHttpHandler ->
    // CustomRetryHandler -> HttpClient) is built on first use, not in the constructor -- the observable
    // seam is that ConnectionInfo's ctor reads the proxy configuration off IConfiguration.

    [Test]
    public void Constructing_via_the_real_send_path_does_not_read_proxy_configuration()
    {
        _ = new OtlpProfilesHttpDispatcher(_configuration);

        Mock.Assert(() => _configuration.ProxyHost, Occurs.Never());
        Mock.Assert(() => _configuration.CollectorPort, Occurs.Never());
    }

    [Test]
    public void Post_with_an_invalid_endpoint_never_reads_proxy_configuration()
    {
        // Endpoint validation must short-circuit before the lazy real pipeline is ever touched.
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = dispatcher.Post(new byte[] { 1 }, string.Empty);

        Assert.That(result.Accepted, Is.False);
        Mock.Assert(() => _configuration.ProxyHost, Occurs.Never());
        Mock.Assert(() => _configuration.CollectorPort, Occurs.Never());
    }

    [Test]
    public void Post_with_a_non_absolute_endpoint_never_reads_proxy_configuration()
    {
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = dispatcher.Post(new byte[] { 1 }, "not a uri");

        Assert.That(result.Accepted, Is.False);
        Mock.Assert(() => _configuration.ProxyHost, Occurs.Never());
        Mock.Assert(() => _configuration.CollectorPort, Occurs.Never());
    }

    [Test]
    public void Post_with_a_well_formed_endpoint_builds_the_real_pipeline_and_reads_proxy_configuration()
    {
        // 127.0.0.1:1 refuses the connection immediately (nothing listens there), so this exercises
        // CreateRealSend/CreateHandler for real without a hanging socket. The connection failure is a
        // retryable HttpRequestException, so CustomRetryHandler burns through its real (short) backoff
        // before giving up -- this test takes a few real seconds, matching the existing "real pipeline"
        // tests below rather than adding new flakiness.
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration);

        var result = dispatcher.Post(new byte[] { 1 }, "https://127.0.0.1:1/v1/profiles");

        Assert.That(result.Accepted, Is.False);
        Mock.Assert(() => _configuration.ProxyHost, Occurs.AtLeastOnce());
        Mock.Assert(() => _configuration.CollectorPort, Occurs.AtLeastOnce());
    }

    #endregion

    #region Real pipeline wiring tests (M10/M12)

    // These exercise the actual CustomRetryHandler + HttpClient chain (OtlpProfilesHttpDispatcher's
    // internal BuildSend) over a stub inner HttpMessageHandler, so retries, exhaustion, and
    // supportability-metric wiring are verified for real rather than through the injected _send
    // delegate (which bypasses CustomRetryHandler entirely).

    // Instant no-op delay, threaded through to CustomRetryHandler's own delayFunc seam -- verifies the
    // same retry-count/outcome behavior as the real backoff without sleeping for it.
    private static Task NoDelay(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;

    [Test]
    public void Post_via_real_pipeline_retries_transient_failures_and_records_supportability_metrics()
    {
        var innerHandler = new SequencedHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(innerHandler.RequestCount, Is.EqualTo(2));
            Assert.That(counters.RetryCount, Is.EqualTo(1));
            Assert.That(counters.SuccessCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Post_via_real_pipeline_gives_up_after_max_retries_and_records_export_failure()
    {
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(innerHandler.RequestCount, Is.EqualTo(3));
            Assert.That(counters.FailureCount, Is.EqualTo(1));
        });
    }

    // CP's dispatcher must record into its own IExportRetrySupportabilityMetricCounters implementation,
    // not the shared OpenTelemetry Metrics Bridge counters -- this fake carries no dependency on
    // OtelBridgeSupportabilityMetric at all, proving the dispatcher only needs the narrow interface.
    private class FakeExportRetryCounters : IExportRetrySupportabilityMetricCounters
    {
        public int SuccessCount { get; private set; }
        public int RetryCount { get; private set; }
        public int FailureCount { get; private set; }

        public void RecordExportSuccess() => SuccessCount++;
        public void RecordExportRetry() => RetryCount++;
        public void RecordExportFailure() => FailureCount++;
    }

    // Minimal stub transport: replays a fixed response sequence, repeating the last entry once
    // exhausted (matches CustomRetryHandlerTests.TestHttpMessageHandler's SetSequence behavior).
    // Builds a fresh HttpResponseMessage/StringContent per call -- CustomRetryHandler disposes each
    // response it retries past, so reusing one HttpContent instance across calls would throw
    // ObjectDisposedException on repeat.
    private class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode StatusCode, string Body, HttpResponseHeaders Headers)[] _responses;
        private int _index;
        public int RequestCount { get; private set; }

        public SequencedHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = responses.Select(r => (r.StatusCode, r.Content?.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult() ?? string.Empty, r.Headers)).ToArray();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var configured = _responses[_index];
            if (_index < _responses.Length - 1)
                _index++;

            var rebuilt = new HttpResponseMessage(configured.StatusCode) { Content = new StringContent(configured.Body) };
            foreach (var header in configured.Headers)
                rebuilt.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return Task.FromResult(rebuilt);
        }
    }

    #endregion
}
