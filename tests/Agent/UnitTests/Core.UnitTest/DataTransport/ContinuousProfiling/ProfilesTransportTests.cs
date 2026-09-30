// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Google.Protobuf;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Profiles.V1Development;
using OpenTelemetry.Proto.Resource.V1;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.DataTransport.ContinuousProfiling;

[TestFixture]
public class ProfilesTransportTests
{
    private ILogger _nrLogger;

    [SetUp]
    public void SetUp()
    {
        // The agent's Log facade is independent of Serilog's static logger; initialize it so calls are mockable.
        _nrLogger = Mock.Create<ILogger>();
        Log.Initialize(_nrLogger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        AuditLog.IsAuditLogEnabled = false;
    }

    [Test]
    public void Send_logs_partial_success_at_finest_when_rejected_profiles_reported()
    {
        // A genuinely partial rejection: 3 of 4 profiles sent were rejected, not all of them -- must stay
        // Finest-only (contrast Send_warns_and_records_full_rejection_when_partial_success_rejects_every_profile_sent).
        var request = BuildNonEmptyRequest();
        AddProfile(request);
        AddProfile(request);
        AddProfile(request);
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, 3, "schema drift"),
            "http://unused", null);

        transport.Send(request);

        // Log.Finest(message, args) keeps the format template and args separate -- match the template
        // and assert the substituted values landed in args, not in the (still-unformatted) message string.
        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("partial success")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Contains(a, (object)3L) && System.Linq.Enumerable.Contains(a, (object)"schema drift"))),
            Occurs.Once());
    }

    [Test]
    public void Send_does_not_log_partial_success_when_none_reported()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(Arg.Matches<string>(m => m.Contains("partial success")), Arg.IsAny<object[]>()), Occurs.Never());
    }

    [Test]
    public void Send_warns_and_records_full_rejection_when_partial_success_rejects_every_profile_sent()
    {
        // Cluster 4: BuildNonEmptyRequest sends exactly one profile, so RejectedProfiles == 1 is a
        // 100%-rejected partial_success -- total silent data loss that must not look like an ordinary
        // Finest-only partial rejection.
        var counters = new FakeContinuousProfilingCounters();
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, 1, "schema drift"),
            "http://unused", null, counters);

        transport.Send(BuildNonEmptyRequest());

        Assert.Multiple(() =>
        {
            Assert.That(counters.FullRejectionCount, Is.EqualTo(1));
            Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("all") && m.Contains("rejected")), Arg.IsAny<object[]>()), Occurs.Once());
            Mock.Assert(() => _nrLogger.Finest(Arg.Matches<string>(m => m.Contains("partial success")), Arg.IsAny<object[]>()), Occurs.Never());
        });
    }

    [Test]
    public void Send_logs_partial_success_at_finest_not_warn_when_only_some_profiles_are_rejected()
    {
        // Contrast case: a genuinely partial rejection (rejected < total sent) stays Finest-only and does
        // not record the full-rejection counter -- the existing deliberate behavior must be unaffected.
        var counters = new FakeContinuousProfilingCounters();
        var request = BuildNonEmptyRequest();
        AddProfile(request); // now 2 profiles total sent
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, 1, "schema drift"),
            "http://unused", null, counters);

        transport.Send(request);

        Assert.Multiple(() =>
        {
            Assert.That(counters.FullRejectionCount, Is.EqualTo(0));
            Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>()), Occurs.Never());
            Mock.Assert(() => _nrLogger.Finest(Arg.Matches<string>(m => m.Contains("partial success")), Arg.IsAny<object[]>()), Occurs.Once());
        });
    }

    [Test]
    public void Send_tolerates_a_null_supportability_metric_counters_on_full_rejection()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, 1, "schema drift"),
            "http://unused", null);

        Assert.That(() => transport.Send(BuildNonEmptyRequest()), Throws.Nothing);
    }

    [Test]
    public void Send_does_not_flip_accepted_when_partial_success_reports_rejections()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, 3, "schema drift"),
            "http://unused", null);

        Assert.That(transport.Send(BuildNonEmptyRequest()), Is.True, "Partial success is diagnostics only; Accepted stays HTTP-status-only.");
    }

    [Test]
    public void Send_invokes_http_dispatch_with_the_serialized_request_bytes_and_endpoint()
    {
        byte[] dispatchedBytes = null;
        string dispatchedEndpoint = null;
        var transport = new ProfilesTransport((bytes, endpoint) =>
        {
            dispatchedBytes = bytes;
            dispatchedEndpoint = endpoint;
            return new ProfilesSendResult(true, 200, string.Empty);
        }, "https://otlp.nr-data.net/v1/profiles", null);

        var request = BuildNonEmptyRequest();
        transport.Send(request);

        Assert.Multiple(() =>
        {
            Assert.That(dispatchedEndpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
            Assert.That(dispatchedBytes, Is.EqualTo(request.ToByteArray()), "The dispatched bytes must be the serialized request.");
        });
    }

    [Test]
    public void Send_invokes_http_dispatch_even_for_an_empty_request()
    {
        var dispatched = false;
        var transport = new ProfilesTransport((bytes, endpoint) => { dispatched = true; return new ProfilesSendResult(true, 200, string.Empty); }, "http://unused", null);

        transport.Send(new ExportProfilesServiceRequest());

        Assert.That(dispatched, Is.True, "Send must post whatever it built; gating happens upstream (CP-enabled), not here.");
    }

    [Test]
    public void Send_does_not_throw_when_the_dispatch_reports_failure()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 500, "error"), "http://unused", null);
        Assert.That(() => transport.Send(BuildNonEmptyRequest()), Throws.Nothing);
    }

    [TestCase(401)]
    [TestCase(403)]
    public void Send_warns_on_the_first_auth_failure(int statusCode)
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, statusCode, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("license key")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_does_not_repeat_the_auth_failure_warn_within_the_rate_limit_window()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 401, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());
        transport.Send(BuildNonEmptyRequest());
        transport.Send(BuildNonEmptyRequest());

        // Three consecutive failures within the same rate-limit window must warn exactly once.
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("license key")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_repeats_the_rejection_warn_once_the_rate_limit_window_elapses()
    {
        // Stopwatch.Frequency ticks-per-second times 5 minutes plus 1 tick -- one tick past the window
        // rather than a wall-clock sleep.
        var windowTicks = (long)(System.TimeSpan.FromMinutes(5).TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        var currentTicks = 1_000_000L;
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 401, "error"),
            "http://unused", null, () => currentTicks);

        transport.Send(BuildNonEmptyRequest()); // first rejection: warns, arms the window

        currentTicks += windowTicks - 1;
        transport.Send(BuildNonEmptyRequest()); // still inside the window: suppressed

        currentTicks += 2; // now past the window
        transport.Send(BuildNonEmptyRequest()); // window elapsed: warns again

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("license key")), Arg.IsAny<object[]>()), Occurs.Exactly(2));
    }

    [TestCase(404)]
    [TestCase(413)]
    [TestCase(400)]
    [TestCase(500)]
    public void Send_warns_on_the_first_non_auth_rejection_too(int statusCode)
    {
        // Every non-2xx rejection mode -- not just 401/403 -- must escalate above Debug on the first
        // occurrence, otherwise a permanently-rejected drain produces zero delivered data silently.
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, statusCode, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("rejected")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_uses_generic_rejection_wording_for_a_non_auth_status()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 413, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => !m.Contains("license key")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_does_not_repeat_a_non_auth_rejection_warn_within_the_rate_limit_window()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 404, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());
        transport.Send(BuildNonEmptyRequest());
        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_shares_the_rate_limit_window_across_different_rejection_statuses()
    {
        // A 401 warn and a subsequent 404 within the same window share one rate-limit gate per
        // transport instance -- the second status must not warn again.
        var statusCode = 401;
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, statusCode, "error"), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());
        statusCode = 404;
        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_names_the_invalid_endpoint_cause_in_the_status_zero_warn()
    {
        // F5: status 0 has three distinct causes; the warn must name the actual one rather than an
        // ambiguous "status 0" that points at none of them.
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.InvalidEndpoint),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("endpoint") && !m.Contains("status")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_names_the_oversized_payload_cause_in_the_status_zero_warn()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.OversizedPayloadDropped),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("payload") && m.Contains("maximum")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_names_the_transport_failure_cause_in_the_status_zero_warn()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.TransportException),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("transport")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_reports_the_duration_metric_for_an_accepted_send()
    {
        var counters = new FakeContinuousProfilingCounters();
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty, elapsed: TimeSpan.FromMilliseconds(120)),
            "http://unused", null, counters);

        transport.Send(BuildNonEmptyRequest());

        Assert.That(counters.Durations, Is.EqualTo(new[] { TimeSpan.FromMilliseconds(120) }));
    }

    [Test]
    public void Send_reports_the_duration_metric_for_a_failed_send_too()
    {
        var counters = new FakeContinuousProfilingCounters();
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 500, "error", elapsed: TimeSpan.FromMilliseconds(80)),
            "http://unused", null, counters);

        transport.Send(BuildNonEmptyRequest());

        Assert.That(counters.Durations, Is.EqualTo(new[] { TimeSpan.FromMilliseconds(80) }));
    }

    [Test]
    public void Send_does_not_report_a_duration_when_the_send_never_reached_the_wire()
    {
        var counters = new FakeContinuousProfilingCounters();
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.InvalidEndpoint),
            "http://unused", null, counters);

        transport.Send(BuildNonEmptyRequest());

        Assert.That(counters.Durations, Is.Empty);
    }

    [TestCase(400)]
    [TestCase(401)]
    [TestCase(503)]
    public void Send_reports_the_per_status_http_error_metric_for_a_rejected_non_zero_status(int statusCode)
    {
        var counters = new FakeContinuousProfilingCounters();
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, statusCode, "error"), "http://unused", null, counters);

        transport.Send(BuildNonEmptyRequest());

        Assert.That(counters.HttpErrors, Is.EqualTo(new[] { statusCode }));
    }

    [Test]
    public void Send_does_not_report_a_per_status_http_error_metric_for_status_zero_or_an_accepted_send()
    {
        var counters = new FakeContinuousProfilingCounters();
        var zero = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.TransportException),
            "http://unused", null, counters);
        var accepted = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "http://unused", null, counters);

        zero.Send(BuildNonEmptyRequest());
        accepted.Send(BuildNonEmptyRequest());

        Assert.That(counters.HttpErrors, Is.Empty);
    }

    [Test]
    public void Send_records_only_the_matching_transport_failure_counter()
    {
        var counters = new FakeContinuousProfilingCounters();
        foreach (var reason in new[] { ProfilesSendFailureReason.TransportTimeout, ProfilesSendFailureReason.TransportNetwork, ProfilesSendFailureReason.TransportTls, ProfilesSendFailureReason.TransportException })
        {
            var failureReason = reason;
            new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: failureReason), "http://unused", null, counters)
                .Send(BuildNonEmptyRequest());
        }

        Assert.Multiple(() =>
        {
            Assert.That(counters.TimeoutFailureCount, Is.EqualTo(1));
            Assert.That(counters.NetworkFailureCount, Is.EqualTo(1));
            Assert.That(counters.TlsFailureCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Send_names_the_timeout_cause_in_the_status_zero_warn()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.TransportTimeout),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("timed out")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_names_the_network_cause_in_the_status_zero_warn()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.TransportNetwork),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("network layer")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Send_names_the_tls_cause_in_the_status_zero_warn()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 0, string.Empty, failureReason: ProfilesSendFailureReason.TransportTls),
            "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("TLS")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [TestCase(false, 401)]
    [TestCase(false, 403)]
    [TestCase(false, 404)]
    [TestCase(false, 500)]
    [TestCase(false, 0)]
    [TestCase(true, 200)]
    public void Send_never_sets_agent_health_status(bool accepted, int statusCode)
    {
        // CP shares the agent's single health slot with the collector connection path and has no way to
        // clear it again on a later success, so no send outcome -- including a 401/403 -- writes it.
        var health = Mock.Create<IAgentHealthReporter>();
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(accepted, statusCode, "x"), "http://unused", health);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => health.SetAgentControlStatus(Arg.IsAny<(bool IsHealthy, string Code, string Status)>(), Arg.IsAny<string[]>()), Occurs.Never());
    }

    [Test]
    public void Send_tolerates_null_counters_and_health_reporter_for_the_new_outcome_metrics()
    {
        var transport = new ProfilesTransport(
            (bytes, endpoint) => new ProfilesSendResult(false, 401, "error", failureReason: ProfilesSendFailureReason.TransportTimeout, elapsed: TimeSpan.FromSeconds(1)),
            "http://unused", null);

        Assert.That(() => transport.Send(BuildNonEmptyRequest()), Throws.Nothing);
    }

    [Test]
    public void Send_does_not_warn_when_accepted()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "http://unused", null);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>()), Occurs.Never());
    }

    [Test]
    public void Send_returns_true_when_the_dispatch_reports_accepted()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "http://unused", null);
        Assert.That(transport.Send(BuildNonEmptyRequest()), Is.True);
    }

    [Test]
    public void Send_returns_false_when_the_dispatch_reports_not_accepted()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 500, "error"), "http://unused", null);
        Assert.That(transport.Send(BuildNonEmptyRequest()), Is.False);
    }

    [Test]
    public void Send_reports_data_usage_on_acceptance_same_as_other_otlp_and_collector_sends()
    {
        var health = Mock.Create<IAgentHealthReporter>();
        var request = BuildNonEmptyRequest();
        var uncompressedBytes = request.ToByteArray().Length;
        // Deliberately different from the uncompressed size: the wire body is gzipped
        // (OtlpProfilesHttpDispatcher.BuildRequestMessage), so the reported usage must come from
        // ProfilesSendResult.SentBytes (the dispatcher's compressed byte count), not bytes.Length.
        const long compressedBytesSent = 999;
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, "ok", sentBytes: compressedBytesSent), "http://unused", health);

        transport.Send(request);

        // "OTLP"/"Profiles" mirrors OtlpAuditHandler's ("OTLP", "Metrics") for the Meter bridge -- CP's
        // closest sibling send path.
        Mock.Assert(() => health.ReportSupportabilityDataUsage("OTLP", "Profiles", compressedBytesSent, 2), Occurs.Once());
        Assert.That(uncompressedBytes, Is.Not.EqualTo(compressedBytesSent), "sanity: the two byte counts must actually differ for this test to prove anything.");
    }

    [Test]
    public void Send_does_not_report_data_usage_when_not_accepted()
    {
        var health = Mock.Create<IAgentHealthReporter>();
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 500, "error"), "http://unused", health);

        transport.Send(BuildNonEmptyRequest());

        Mock.Assert(() => health.ReportSupportabilityDataUsage(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<long>(), Arg.IsAny<long>()), Occurs.Never());
    }

    [Test]
    public void Send_tolerates_a_null_agent_health_reporter()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, "ok"), "http://unused", null);
        Assert.That(() => transport.Send(BuildNonEmptyRequest()), Throws.Nothing);
    }

    [Test]
    public void Send_constructs_without_throwing_given_endpoint_and_dispatch_delegate()
    {
        Assert.That(() => new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "https://otlp.nr-data.net/v1/profiles", null),
            Throws.Nothing);
    }

    [Test]
    public void Send_handles_null_resource_profiles_without_throwing()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "http://unused", null);
        Assert.That(() => transport.Send(new ExportProfilesServiceRequest()), Throws.Nothing);
    }

    [Test]
    public void UpdateEndpoint_changes_the_endpoint_subsequent_sends_post_to()
    {
        string dispatchedEndpoint = null;
        var transport = new ProfilesTransport((bytes, endpoint) =>
        {
            dispatchedEndpoint = endpoint;
            return new ProfilesSendResult(true, 200, string.Empty);
        }, "https://otlp.nr-data.net/v1/profiles", null);

        transport.UpdateEndpoint("https://collector.eu01.nr-data.net/v1/profiles");
        transport.Send(BuildNonEmptyRequest());

        Assert.That(dispatchedEndpoint, Is.EqualTo("https://collector.eu01.nr-data.net/v1/profiles"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void UpdateEndpoint_ignores_a_null_or_empty_value(string newEndpoint)
    {
        string dispatchedEndpoint = null;
        var transport = new ProfilesTransport((bytes, endpoint) =>
        {
            dispatchedEndpoint = endpoint;
            return new ProfilesSendResult(true, 200, string.Empty);
        }, "https://otlp.nr-data.net/v1/profiles", null);

        transport.UpdateEndpoint(newEndpoint);
        transport.Send(BuildNonEmptyRequest());

        Assert.That(dispatchedEndpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
    }

    private static IConfiguration ConfigWith(bool logPayload, int maxChars)
    {
        var config = Mock.Create<IConfiguration>();
        Mock.Arrange(() => config.ContinuousProfilingLogPayload).Returns(logPayload);
        Mock.Arrange(() => config.ContinuousProfilingLogPayloadMaxChars).Returns(maxChars);
        return config;
    }

    private static string Decode(string base64)
    {
        using var input = new MemoryStream(Convert.FromBase64String(base64));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool IsEncodedProfilePayload(object arg)
    {
        if (!(arg is string s) || !s.StartsWith(ProfilesTransport.EncodedPayloadPrefix))
            return false;
        try
        {
            return Decode(s.Substring(ProfilesTransport.EncodedPayloadPrefix.Length)).Contains("resourceProfiles");
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTooLargePlaceholder(object arg)
        => arg is string s && s.StartsWith("{payload too large") && s.Contains("NEW_RELIC_PROFILING_LOG_PAYLOAD_MAX_CHARS");

    private static ExportProfilesServiceRequest BuildHugeRequest()
    {
        // Varied names so gzip cannot shrink it below the 1024 floor.
        var dictionary = new ProfilesDictionary();
        var rng = new Random(42);
        for (var i = 0; i < 2_000; i++)
            dictionary.StringTable.Add(Guid.NewGuid().ToString("N") + rng.Next());
        return new ExportProfilesServiceRequest { Dictionary = dictionary };
    }

    private static ProfilesTransport NewTransport(IConfiguration configuration)
        => new ProfilesTransport((b, e) => new ProfilesSendResult(true, 200, string.Empty), "http://unused", null, configuration: configuration);

    // ---- Send: the Finest "Invoked" line ----

    [Test]
    public void Send_logs_placeholder_at_finest_when_switch_is_off()
    {
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(false, 65536)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Contains(a, (object)ProfilesTransport.PayloadUnavailablePlaceholder))),
            Occurs.Once());
    }

    [Test]
    public void Send_logs_placeholder_when_configuration_is_null()
    {
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(null).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Contains(a, (object)ProfilesTransport.PayloadUnavailablePlaceholder))),
            Occurs.Once());
    }

    [Test]
    public void Send_logs_gzip_base64_payload_at_finest_when_switch_is_on()
    {
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(true, 65536)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Any(a, x => IsEncodedProfilePayload(x)))),
            Occurs.Once());
    }

    [Test]
    public void Send_logs_payload_too_large_placeholder_when_encoded_size_exceeds_cap()
    {
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(true, 1024)).Send(BuildHugeRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Any(a, x => IsTooLargePlaceholder(x)))),
            Occurs.Once());
    }

    [Test]
    public void Send_treats_a_non_positive_configured_cap_as_the_default()
    {
        // A loose mock returns 0 for an un-arranged int; that must fall back to the 64K default, not mark everything too large.
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(true, 0)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Any(a, x => IsEncodedProfilePayload(x)))),
            Occurs.Once());
    }

    [Test]
    public void Send_uses_placeholder_when_switch_is_on_but_nobody_is_listening()
    {
        // Finest off and audit off: no sink will read the payload, so it must not be rendered/encoded.
        // Observable via the Debug-only state: nothing logs the encoded payload at all.
        Mock.Arrange(() => _nrLogger.IsDebugEnabled).Returns(true);
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(false);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(true, 65536)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(Arg.IsAny<string>(), Arg.Matches<object[]>(a => System.Linq.Enumerable.Any(a, x => IsEncodedProfilePayload(x)))), Occurs.Never());
    }

    [Test]
    public void Send_encodes_payload_when_switch_is_on_and_only_audit_log_is_enabled()
    {
        // Log.Finest passes through without checking IsFinestEnabled, and the same payloadText goes to the audit
        // log, so the audit-only case must still encode: pins the `|| AuditLog.IsAuditLogEnabled` half of the gate.
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(false);
        AuditLog.IsAuditLogEnabled = true;

        NewTransport(ConfigWith(true, 65536)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Finest(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.Matches<object[]>(a => System.Linq.Enumerable.Any(a, x => IsEncodedProfilePayload(x)))),
            Occurs.Once());
    }

    [Test]
    public void Send_no_longer_logs_the_payload_at_debug()
    {
        Mock.Arrange(() => _nrLogger.IsDebugEnabled).Returns(true);
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        NewTransport(ConfigWith(true, 65536)).Send(BuildNonEmptyRequest());

        Mock.Assert(() => _nrLogger.Debug(
                Arg.Matches<string>(m => m.Contains("Invoked")),
                Arg.IsAny<object[]>()),
            Occurs.Never());
    }

    [Test]
    public void Send_with_empty_request_and_switch_on_does_not_throw()
    {
        Mock.Arrange(() => _nrLogger.IsFinestEnabled).Returns(true);
        AuditLog.IsAuditLogEnabled = false;

        Assert.DoesNotThrow(() => NewTransport(ConfigWith(true, 65536)).Send(new ExportProfilesServiceRequest()));
    }

    // ---- BuildPayloadText / ToDiagnosticJson ----

    [Test]
    public void BuildPayloadText_returns_prefixed_base64_that_decodes_to_the_diagnostic_json()
    {
        var request = BuildNonEmptyRequest();

        var text = ProfilesTransport.BuildPayloadText(request, 65536);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("gzip+base64:"));
            Assert.That(Decode(text.Substring("gzip+base64:".Length)), Is.EqualTo(ProfilesTransport.ToDiagnosticJson(request)));
        });
    }

    [Test]
    public void BuildPayloadText_at_exactly_the_cap_is_still_encoded()
    {
        var request = BuildNonEmptyRequest();
        var encodedLength = ProfilesTransport.BuildPayloadText(request, int.MaxValue).Length - ProfilesTransport.EncodedPayloadPrefix.Length;

        Assert.That(ProfilesTransport.BuildPayloadText(request, encodedLength), Does.StartWith("gzip+base64:"));
    }

    [Test]
    public void BuildPayloadText_one_under_the_cap_is_too_large_and_names_sizes_cap_and_setting()
    {
        var request = BuildNonEmptyRequest();
        var encodedLength = ProfilesTransport.BuildPayloadText(request, int.MaxValue).Length - ProfilesTransport.EncodedPayloadPrefix.Length;

        var text = ProfilesTransport.BuildPayloadText(request, encodedLength - 1);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("{payload too large:").And.EndWith("}"));
            Assert.That(text, Does.Contain($"{encodedLength} gzip+base64 chars"));
            Assert.That(text, Does.Contain($"{encodedLength - 1}-char cap"));
            Assert.That(text, Does.Contain("NEW_RELIC_PROFILING_LOG_PAYLOAD_MAX_CHARS"));
        });
    }

    [Test]
    public void ToDiagnosticJson_is_not_truncated()
    {
        var json = ProfilesTransport.ToDiagnosticJson(BuildHugeRequest());

        Assert.That(json.Length, Is.GreaterThan(ProfilesTransport.DefaultMaxDiagnosticPayloadChars));
        Assert.That(json, Does.Not.Contain("truncated"));
    }

    // The Finest "Invoked" log line carries payloadText: the gzip+base64 of ToDiagnosticJson(request), or a
    // placeholder. Testing the JSON serialization directly avoids capturing the static logger while still
    // pinning the payload's shape.

    [Test]
    public void ToDiagnosticJson_is_compact_single_line_valid_json()
    {
        var json = ProfilesTransport.ToDiagnosticJson(BuildNonEmptyRequest());

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("\n"), "Diagnostic JSON must be compact (single line), like other collector payloads.");
            Assert.That(json, Does.Contain("worker-1"), "The sample's thread.name attribute value should be present.");
            // Valid JSON with the proto3 top-level shape (camelCase field names).
            var root = JObject.Parse(json);
            Assert.That(root["resourceProfiles"], Is.Not.Null, "Serialized request should carry resourceProfiles.");
        });
    }

    [Test]
    public void ToDiagnosticJson_emits_frame_name_special_chars_literally_not_unicode_escaped()
    {
        // JsonFormatter emits printable ASCII literally, so the chars common in .NET frame names -- nested
        // '+', closure '<'/'>', generic-arity backtick, byref '&' -- must appear as-is, not \uXXXX escaped.
        const string frameName = "Ns.Outer`1+<>c.<M>b__0(System.Int32&)";

        var dictionary = new ProfilesDictionary();
        dictionary.StringTable.Add(frameName);
        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };

        var json = ProfilesTransport.ToDiagnosticJson(request);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain(frameName), "Frame-name special chars should be emitted literally.");
            Assert.That(json, Does.Not.Contain("\\u00"), "No \\uXXXX HTML-escaping of printable ASCII.");
        });
    }

    [Test]
    public void ToDiagnosticJson_renders_link_ids_as_lowercase_hex_not_base64()
    {
        // trace_id/span_id are proto `bytes` -> proto3 JSON base64. The diagnostic log rewrites them to
        // lowercase hex so they're greppable against the W3C-hex ids used elsewhere in the logs.
        var traceId = new byte[] { 0x1c, 0xb9, 0xb2, 0x2a, 0x7b, 0xfd, 0x43, 0x3d, 0x29, 0xdc, 0xdf, 0xe1, 0x1a, 0xb7, 0xfe, 0x27 };
        var spanId = new byte[] { 0x37, 0x83, 0xcc, 0xde, 0xba, 0x84, 0x13, 0x91 };

        var dictionary = new ProfilesDictionary();
        dictionary.LinkTable.Add(new Link
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(spanId),
        });
        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };

        var json = ProfilesTransport.ToDiagnosticJson(request);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"traceId\":\"1cb9b22a7bfd433d29dcdfe11ab7fe27\""), "traceId should be lowercase hex.");
            Assert.That(json, Does.Contain("\"spanId\":\"3783ccdeba841391\""), "spanId should be lowercase hex.");
            Assert.That(json, Does.Not.Contain("=="), "No base64-padded ids should remain.");
        });
    }

    // Minimal fake so full-rejection tests don't depend on the real ContinuousProfilingSupportabilityMetricCounters
    // wiring (metric builder, publish delegate) -- only RecordFullRejection is exercised here.
    private class FakeContinuousProfilingCounters : IContinuousProfilingSupportabilityMetricCounters
    {
        public int FullRejectionCount { get; private set; }
        public int TimeoutFailureCount { get; private set; }
        public int NetworkFailureCount { get; private set; }
        public int TlsFailureCount { get; private set; }
        public List<int> HttpErrors { get; } = new List<int>();
        public List<TimeSpan> Durations { get; } = new List<TimeSpan>();
        public void RecordExportSuccess() { }
        public void RecordExportRetry() { }
        public void RecordExportFailure() { }
        public void RecordPayloadDropped() { }
        public void RecordFullRejection() => FullRejectionCount++;
        public void RecordTimeoutFailure() => TimeoutFailureCount++;
        public void RecordNetworkFailure() => NetworkFailureCount++;
        public void RecordTlsFailure() => TlsFailureCount++;
        public void RecordHttpError(int statusCode) => HttpErrors.Add(statusCode);
        public void RecordSendDuration(TimeSpan duration) => Durations.Add(duration);
        public void CollectMetrics() { }
        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate) { }
    }

    // Adds a second, minimal Profile to an existing non-empty request's first ScopeProfiles -- used to make
    // a request carry >1 total profile so a partial (not full) rejection can be exercised.
    private static void AddProfile(ExportProfilesServiceRequest request)
    {
        request.ResourceProfiles[0].ScopeProfiles[0].Profiles.Add(new Profile());
    }

    private static ExportProfilesServiceRequest BuildNonEmptyRequest()
    {
        var dictionary = new ProfilesDictionary();
        dictionary.StringTable.Add(string.Empty);
        dictionary.StringTable.Add("thread.name");
        dictionary.StringTable.Add("worker-1");
        dictionary.StringTable.Add("A()");

        dictionary.FunctionTable.Add(new Function());
        dictionary.FunctionTable.Add(new Function { NameStrindex = 3 });

        dictionary.LocationTable.Add(new Location());
        var location = new Location();
        location.Lines.Add(new Line { FunctionIndex = 1 });
        dictionary.LocationTable.Add(location);

        dictionary.StackTable.Add(new Stack());
        var stack = new Stack();
        stack.LocationIndices.Add(1);
        dictionary.StackTable.Add(stack);

        dictionary.AttributeTable.Add(new KeyValueAndUnit());
        dictionary.AttributeTable.Add(new KeyValueAndUnit
        {
            KeyStrindex = 1,
            Value = new AnyValue { StringValue = "worker-1" }
        });

        var sample = new Sample { StackIndex = 1 };
        sample.Values.Add(1L);
        sample.AttributeIndices.Add(1);

        var profile = new Profile();
        profile.Samples.Add(sample);

        var scopeProfiles = new ScopeProfiles();
        scopeProfiles.Profiles.Add(profile);

        var resourceProfiles = new ResourceProfiles { Resource = new Resource() };
        resourceProfiles.ScopeProfiles.Add(scopeProfiles);

        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };
        request.ResourceProfiles.Add(resourceProfiles);
        return request;
    }
}
