// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
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
        // Real default (DefaultConfiguration.MaxPayloadSizeInBytes) -- big enough that every payload built
        // in this file's other tests stays well under it and only the size-guard tests below override it.
        Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1_000_000);
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
    public void Post_reports_the_gzip_compressed_body_length_as_sent_bytes_not_the_raw_payload_length()
    {
        // Cluster 4: ReportSupportabilityDataUsage must reflect what's actually on the wire (the
        // gzip-compressed body), not the pre-compression payload size.
        HttpRequestMessage captured = null;
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, req => { captured = req; return response; });

        var payload = Encoding.UTF8.GetBytes(new string('a', 500));
        var result = dispatcher.Post(payload, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.SentBytes, Is.EqualTo(captured.Content.Headers.ContentLength.Value));
            Assert.That(result.SentBytes, Is.Not.EqualTo(payload.Length), "Sent bytes must be the compressed wire size, not the raw payload length.");
        });
    }

    [Test]
    public void CreateHandler_configures_automatic_decompression_so_a_gzip_encoded_response_can_be_parsed()
    {
        // Cluster 4: without this, a gzip-encoded collector response body would reach
        // TryParsePartialSuccess still compressed and silently fail to parse, swallowing
        // partial-success diagnostics.
        var createHandler = typeof(OtlpProfilesHttpDispatcher).GetMethod("CreateHandler", BindingFlags.NonPublic | BindingFlags.Static);
        var handler = (HttpMessageHandler)createHandler.Invoke(null, new object[] { null });

        var automaticDecompression = handler.GetType().GetProperty("AutomaticDecompression");
        Assert.That(automaticDecompression, Is.Not.Null, "The created handler must expose AutomaticDecompression.");
        Assert.That(automaticDecompression.GetValue(handler), Is.EqualTo(DecompressionMethods.GZip | DecompressionMethods.Deflate));
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

    #region Payload-size guard (H6)

    [Test]
    public void Post_drops_the_batch_without_sending_when_the_compressed_payload_exceeds_the_configured_max_size()
    {
        // Gzip's fixed header/footer overhead alone exceeds this, so any non-empty payload trips the guard --
        // no need for a multi-MB buffer to exercise the over-limit path deterministically.
        Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(5);
        var sendInvoked = false;
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => { sendInvoked = true; return new HttpResponseMessage(HttpStatusCode.OK); });

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(0));
            Assert.That(sendInvoked, Is.False, "An oversized payload must be dropped client-side, never sent.");
        });
    }

    [Test]
    public void Post_sends_normally_when_the_compressed_payload_is_within_the_configured_max_size()
    {
        Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1_000_000);
        var sendInvoked = false;
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, _ => { sendInvoked = true; return response; });

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(sendInvoked, Is.True);
        });
    }

    [Test]
    public void A_send_factory_that_throws_is_retried_on_the_next_Post_not_cached_permanently()
    {
        // Regression test (Cluster B): the _send Lazy must use PublicationOnly, not the default
        // ExecutionAndPublication which caches a factory exception permanently. A single transient throw
        // building the real send pipeline (CreateRealSend) would otherwise rethrow on every subsequent Post
        // for the process lifetime, leaving CP silently dead with no recovery but a process restart.
        var factoryCalls = 0;
        Func<HttpRequestMessage, HttpResponseMessage> workingSend =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };

        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, (Func<Func<HttpRequestMessage, HttpResponseMessage>>)(() =>
        {
            factoryCalls++;
            if (factoryCalls == 1)
                throw new InvalidOperationException("transient failure building the send pipeline");
            return workingSend;
        }));

        var payload = new byte[] { 1, 2, 3 };

        // First Post: the factory throws. Post's own try/catch swallows it and returns a failure result;
        // crucially the exception must NOT be cached by the Lazy.
        var first = dispatcher.Post(payload, Endpoint);
        Assert.That(first.Accepted, Is.False, "the first send fails because the factory threw");

        // Second Post: PublicationOnly re-runs the factory, which now succeeds -- the dispatcher recovers.
        var second = dispatcher.Post(payload, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.EqualTo(2), "the factory must be retried, proving the throw was not cached (would stay 1 with the default Lazy mode)");
            Assert.That(second.Accepted, Is.True, "the dispatcher must recover once the transient construction failure clears");
        });
    }

    [Test]
    public void Post_records_the_payload_dropped_supportability_metric_when_over_the_limit()
    {
        Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(5);
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var counters = new FakePayloadDroppedCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(counters.PayloadDroppedCount, Is.EqualTo(1));
            Assert.That(innerHandler.RequestCount, Is.EqualTo(0), "The real pipeline must never be invoked once the size guard drops the batch.");
        });
    }

    [Test]
    public void Post_does_not_throw_when_only_the_narrow_export_retry_counters_interface_is_supplied_and_the_payload_is_dropped()
    {
        // FakeExportRetryCounters (below) implements only IExportRetrySupportabilityMetricCounters, not the
        // wider IContinuousProfilingSupportabilityMetricCounters the size guard casts to -- the "as" cast
        // must fail safely (null) rather than throw, since a caller could legitimately supply either shape.
        Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(5);
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        ProfilesSendResult result = default;
        Assert.DoesNotThrow(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint));
        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(innerHandler.RequestCount, Is.EqualTo(0), "The real pipeline must never be invoked once the size guard drops the batch.");
        });
    }

    // Widens FakeExportRetryCounters (defined below, in the real-pipeline region) with the size-guard
    // counter, so a single fake can prove RecordPayloadDropped is actually reached through the "as" cast.
    private class FakePayloadDroppedCounters : IExportRetrySupportabilityMetricCounters, IContinuousProfilingSupportabilityMetricCounters
    {
        public int PayloadDroppedCount { get; private set; }
        public void RecordExportSuccess() { }
        public void RecordExportRetry() { }
        public void RecordExportFailure() { }
        public void RecordPayloadDropped() => PayloadDroppedCount++;
        public void RecordFullRejection() { }
        public void CollectMetrics() { }
        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate) { }
    }

    #endregion

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

    [Test]
    public void Post_via_real_pipeline_records_export_success_exactly_once_on_a_clean_2xx()
    {
        // The dispatcher -- not CustomRetryHandler -- owns the terminal success counter for CP, recorded
        // once the body is actually read (Cluster C, otlp-egress F1). A clean 2xx with a readable body
        // records exactly one success and nothing else.
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(counters.SuccessCount, Is.EqualTo(1));
            Assert.That(counters.FailureCount, Is.EqualTo(0));
            Assert.That(counters.RetryCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Post_records_export_success_not_failure_when_the_body_read_fails_after_a_2xx_header()
    {
        // Cluster 4: delivery already succeeded once a 2xx header is received -- a subsequent body-read
        // failure (stalled/truncated body) is diagnostics-only (the response body is never anything but
        // a small OTLP partial-success ack or empty). Counting it as an export failure understates real
        // delivery and would incorrectly escalate CP's backoff for a batch that was actually accepted.
        var innerHandler = new BodyThrowsAfterHeadersHandler();
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True, "A 2xx header means delivery succeeded regardless of whether the (diagnostics-only) body could be read.");
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(counters.SuccessCount, Is.EqualTo(1));
            Assert.That(counters.FailureCount, Is.EqualTo(0), "A body-read failure after a 2xx header must not be counted as an export failure.");
        });
    }

    [Test]
    public void Post_records_export_failure_when_the_body_read_fails_after_a_non_2xx_header()
    {
        // A body-read failure on a non-2xx response is not a "delivery already succeeded" case -- it must
        // still count as a failure, same as any other non-2xx outcome.
        var innerHandler = new BodyThrowsAfterHeadersHandler(HttpStatusCode.InternalServerError);
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(counters.SuccessCount, Is.EqualTo(0));
            Assert.That(counters.FailureCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Post_records_export_failure_on_a_non_transient_rejection_without_retrying()
    {
        // F2 at the CP layer: a non-transient status (403) is terminal and not retried, but it is a send
        // failure and must be counted -- these are the rejections customers hit most (bad key, path not
        // enabled). Previously every CP export counter stayed at zero for exactly these cases.
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("denied") });
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(innerHandler.RequestCount, Is.EqualTo(1), "A non-transient status must not be retried.");
            Assert.That(counters.FailureCount, Is.EqualTo(1));
            Assert.That(counters.RetryCount, Is.EqualTo(0));
            Assert.That(counters.SuccessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Post_treats_a_body_read_that_actually_trips_the_cancellation_token_as_a_diagnostics_only_stall_after_a_2xx()
    {
        // Proves the real mechanism, not just the outcome: ReadResponseBodyBounded's
        // CancellationTokenSource(_bodyReadTimeout) actually fires against a stream that never completes
        // a read on its own -- BodyThrowsAfterHeadersHandler's ThrowOnReadContent instead throws
        // synchronously from CreateContentReadStreamAsync, which never touches the token at all. Uses the
        // bodyReadTimeout test seam to shorten the deadline so this runs fast and deterministically.
        var innerHandler = new StallingBodyHandler();
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay, bodyReadTimeout: TimeSpan.FromMilliseconds(50));

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True, "A 2xx header means delivery succeeded even though the body read timed out.");
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(counters.SuccessCount, Is.EqualTo(1));
            Assert.That(counters.FailureCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Post_treats_a_body_read_that_actually_trips_the_cancellation_token_as_a_failure_after_a_non_2xx()
    {
        // Same real-timeout mechanism as above, but on a non-2xx response: the stall must still count as
        // a failure, same as any other non-2xx outcome.
        var innerHandler = new StallingBodyHandler(HttpStatusCode.InternalServerError);
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay, bodyReadTimeout: TimeSpan.FromMilliseconds(50));

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(counters.SuccessCount, Is.EqualTo(0));
            Assert.That(counters.FailureCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Post_records_export_failure_when_the_endpoint_is_invalid()
    {
        // Sibling no-counter path: an invalid endpoint short-circuits before the wire, so nothing else can
        // count it -- the dispatcher must record the failure or the counter stays silently at zero.
        var innerHandler = new SequencedHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = dispatcher.Post(new byte[] { 1, 2, 3 }, "not a uri");

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(ProfilesSendFailureReason.InvalidEndpoint));
            Assert.That(counters.FailureCount, Is.EqualTo(1));
            Assert.That(innerHandler.RequestCount, Is.EqualTo(0), "An invalid endpoint must never reach the wire.");
        });
    }

    [Test]
    public void Post_records_export_failure_when_the_send_throws_at_the_transport_layer()
    {
        // Sibling no-counter path: a non-retryable throw from the send is caught by Post's blanket catch;
        // with recordTerminalOutcomes:false the retry handler recorded nothing terminal, so Post must count
        // it or the failure is invisible.
        var innerHandler = new ThrowingHandler(new InvalidOperationException("transport blew up"));
        var counters = new FakeExportRetryCounters();
        var dispatcher = new OtlpProfilesHttpDispatcher(_configuration, innerHandler, counters, NoDelay);

        var result = default(ProfilesSendResult);
        Assert.That(() => result = dispatcher.Post(new byte[] { 1, 2, 3 }, Endpoint), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(ProfilesSendFailureReason.TransportException));
            Assert.That(counters.FailureCount, Is.EqualTo(1));
            Assert.That(counters.SuccessCount, Is.EqualTo(0));
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

    // Returns a 200 with a body that throws when read -- models a proxy/collector that returns headers and
    // then stalls or truncates the body, the exact case ResponseHeadersRead + a bounded body read exposes.
    private class BodyThrowsAfterHeadersHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public int RequestCount { get; private set; }

        public BodyThrowsAfterHeadersHandler(HttpStatusCode statusCode = HttpStatusCode.OK) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new ThrowOnReadContent() });
        }
    }

    private class ThrowOnReadContent : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() => throw new IOException("body stalled after headers");
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => throw new IOException("body stalled after headers");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    // Returns a 200/non-2xx with a body stream whose reads never complete on their own -- unlike
    // ThrowOnReadContent (a synchronous throw that never touches the cancellation token at all), this
    // models a genuinely stalled connection, so a test against it actually exercises
    // ReadResponseBodyBounded's CancellationTokenSource firing rather than substituting a same-catch-
    // branch exception for it.
    private class StallingBodyHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public StallingBodyHandler(HttpStatusCode statusCode = HttpStatusCode.OK) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new StallingContent() });
    }

    private class StallingContent : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new StallingStream());
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => throw new NotSupportedException();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    // A read never completes by itself; it only ever finishes when the CancellationToken it's given is
    // cancelled, at which point it throws -- exactly the shape a hung connection presents to
    // ReadResponseBodyBounded, and the only way to prove its CancellationTokenSource(BodyReadTimeout)
    // actually trips something instead of just being constructed and ignored.
    private class StallingStream : Stream
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<int>();
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Always throws from the send, modelling a transport-layer failure the retry handler rethrows.
    private class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;
        public ThrowingHandler(Exception exception) => _exception = exception;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(_exception);
    }

    #endregion
}
