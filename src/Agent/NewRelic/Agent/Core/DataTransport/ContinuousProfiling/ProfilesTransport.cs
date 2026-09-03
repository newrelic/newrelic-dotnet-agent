// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Google.Protobuf;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// Serializes an <see cref="ExportProfilesServiceRequest"/> and dispatches it to the collector
/// via an injected HTTP POST delegate. Reports a data-usage supportability metric on acceptance,
/// same as every other collector/OTLP send path (<c>HttpCollectorWire.SendData</c>, <c>OtlpAuditHandler</c>).
/// </summary>
public class ProfilesTransport : IProfilesTransport
{
    // Collector "method" token for the payload log lines, so CP reads like every other collector payload.
    // No real collector method exists for an OTLP POST; this is the stable identifier the integration test
    // greps for.
    private const string ProfilesMethodName = "continuous_profiling";

    // Destination/area for ReportSupportabilityDataUsage -- mirrors OtlpAuditHandler's ("OTLP", "Metrics")
    // for the Meter bridge, CP's closest sibling.
    private const string DataUsageApi = "OTLP";
    private const string DataUsageArea = "Profiles";

    // Cap on the rendered diagnostic JSON -- a full request (every frame/thread name in the batch) can
    // reach multiple MB; this bounds the allocation and what gets written to the Debug/audit logs.
    public const int MaxDiagnosticJsonLength = 64 * 1024;

    // A non-2xx (bad/expired license key, path not enabled, oversized payload, schema rejection, etc.)
    // fails identically every drain until the underlying cause is fixed -- rate-limit the Warn to once
    // per window so a persistently-rejected send doesn't flood the log.
    private static readonly long RejectionWarnIntervalStopwatchTicks = (long)(TimeSpan.FromMinutes(5).TotalSeconds * Stopwatch.Frequency);

    // Compact, single-line protobuf-JSON (proto3 rules: bytes -> base64, enums -> names; default values emitted
    // so the shape matches the OTel dump). No indentation -- like every other collector payload we log.
    private static readonly JsonFormatter DiagnosticJsonFormatter =
        new JsonFormatter(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private readonly Func<byte[], string, ProfilesSendResult> _httpPost;
    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly Func<long> _nowTicks;

    // volatile: swapped by UpdateEndpoint (e.g. on AgentConnectedEvent) on a different thread than the
    // scheduler thread that reads it in Send; a plain field would risk a stale read across cores.
    private volatile string _endpoint;

    // Stopwatch ticks of the last rejection Warn; long.MinValue = never warned yet. Interlocked, not a
    // plain field, in case a future caller sends concurrently -- matches ContinuousProfilingService's own
    // Interlocked.Read/CompareExchange convention for cross-thread timestamp fields (avoids a torn 64-bit
    // read on 32-bit hosts).
    private long _lastRejectionWarnTicks = long.MinValue;

    public ProfilesTransport(Func<byte[], string, ProfilesSendResult> httpPost, string endpoint, IAgentHealthReporter agentHealthReporter)
        : this(httpPost, endpoint, agentHealthReporter, Stopwatch.GetTimestamp)
    {
    }

    // Test seam: lets a test fast-forward past RejectionWarnIntervalStopwatchTicks without a real
    // 5-minute sleep, e.g. to prove the rate-limited Warn fires again once the window elapses.
    public ProfilesTransport(Func<byte[], string, ProfilesSendResult> httpPost, string endpoint, IAgentHealthReporter agentHealthReporter, Func<long> nowTicks)
    {
        _httpPost = httpPost;
        _endpoint = endpoint;
        _agentHealthReporter = agentHealthReporter;
        _nowTicks = nowTicks;
    }

    public void UpdateEndpoint(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
            return;

        _endpoint = endpoint;
    }

    public bool Send(ExportProfilesServiceRequest request)
    {
        var bytes = request.ToByteArray();
        var requestGuid = Guid.NewGuid();

        // Quick per-drain summary (byte count) at Debug -- the once-per-session "Session started" line is Info.
        var profile = request.ResourceProfiles?.Count > 0 ? "built" : "empty";
        Log.Debug("[ContinuousProfiling] Posting profile ({0}); {1} bytes to {2}.", profile, bytes.Length, _endpoint);

        // Log + audit exactly like HttpCollectorWire.SendData so CP payloads are observable the same way.
        Log.Finest("Request({0}): Invoking \"{1}\"", requestGuid, ProfilesMethodName);

        // Built once, only when a sink is listening -- the JSON render is not free: at a 1s drain interval,
        // rendering the full protobuf-to-JSON-DOM-to-string pipeline (and only then truncating it) on every
        // drain would allocate several MB per drain purely for a Debug-level log line. Gated on Finest
        // (stricter/rarer than plain Debug, which routine CP troubleshooting runs at) rather than Debug.
        var payloadJson = (Log.IsFinestEnabled || AuditLog.IsAuditLogEnabled) ? ToDiagnosticJson(request) : null;

        var result = _httpPost(bytes, _endpoint);

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, _endpoint);
        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, payloadJson);

        Log.Debug("Request({0}): Invoked \"{1}\" with : {2}", requestGuid, ProfilesMethodName, payloadJson);
        Log.Debug("Request({0}): Invocation of \"{1}\" yielded response : {2}", requestGuid, ProfilesMethodName, result.ResponseContent);
        if (!result.Accepted)
        {
            Log.Debug("Request({0}): Invocation of \"{1}\" was not accepted (status {2}).", requestGuid, ProfilesMethodName, result.StatusCode);

            WarnOnRejectionRateLimited(result.StatusCode);
        }

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Received, DataTransportAuditLogger.AuditLogSource.Collector, result.ResponseContent);

        // Diagnostics only -- an OTLP partial_success does not change Accepted (see ProfilesSendResult).
        if (result.RejectedProfiles > 0 || !string.IsNullOrEmpty(result.PartialSuccessErrorMessage))
        {
            Log.Finest("Request({0}): Invocation of \"{1}\" reported a partial success: {2} rejected profile(s); {3}",
                requestGuid, ProfilesMethodName, result.RejectedProfiles, result.PartialSuccessErrorMessage);
        }

        // Data-usage supportability metric, same as every other OTLP/collector send -- acceptance only.
        if (result.Accepted)
        {
            var bytesReceived = Encoding.UTF8.GetByteCount(result.ResponseContent ?? string.Empty);
            _agentHealthReporter?.ReportSupportabilityDataUsage(DataUsageApi, DataUsageArea, bytes.Length, bytesReceived);
        }

        return result.Accepted;
    }

    // See RejectionWarnIntervalStopwatchTicks for the rate-limit rationale. CompareExchange, not a plain
    // read-then-write, so two callers racing on the window boundary can't both pass the staleness check
    // and double-log -- exactly one wins the swap and warns. Covers every non-2xx status, not just
    // auth failures -- a 404 (path not enabled), 413 (payload too large), or 400 (schema rejected) is
    // just as silent otherwise, and just as actionable once surfaced.
    private void WarnOnRejectionRateLimited(int statusCode)
    {
        var now = _nowTicks();
        var last = Interlocked.Read(ref _lastRejectionWarnTicks);

        if (last != long.MinValue && now - last < RejectionWarnIntervalStopwatchTicks)
            return;

        if (Interlocked.CompareExchange(ref _lastRejectionWarnTicks, now, last) != last)
            return;

        if (statusCode == 401 || statusCode == 403)
            Log.Warn("[ContinuousProfiling] Profile send rejected with status {0} -- check that the configured license key is valid. This warning is rate-limited; subsequent occurrences are logged at Debug.", statusCode);
        else
            Log.Warn("[ContinuousProfiling] Profile send rejected with status {0}; profiles are not being delivered. This warning is rate-limited; subsequent occurrences are logged at Debug.", statusCode);
    }

    // Compact single-line protobuf-JSON for the payload log line + audit log. Public + static so it can be
    // unit-tested without capturing the static logger. Google.Protobuf's JsonFormatter
    // HTML-escapes `<`/`>` (-> </>), which litters the common .NET closure frames (`<>c`, `<M>d__`);
    // we round-trip through Newtonsoft (a first-party agent dependency present on every TFM) to re-emit
    // compact with those characters literal. NB: System.Text.Json is deliberately NOT used here -- its
    // System.Text.Encodings.Web dependency binds to a version that fails to load on older runtimes (net8),
    // which threw inside the drain loop.
    //
    // Also DIAGNOSTIC-ONLY: proto3 JSON renders the `bytes` trace_id/span_id as base64
    // (e.g. "HLmyKnv9Qz0p3N/hGrf+Jw=="), which is unsearchable against the W3C-hex ids used everywhere else in
    // the logs. We rewrite the linkTable ids to lowercase hex (-> "1cb9b22a...") so the log is greppable. The
    // real wire payload is unaffected (raw bytes); a STANDARD OTLP JSON would keep these base64.
    public static string ToDiagnosticJson(ExportProfilesServiceRequest request)
    {
        var root = JToken.Parse(DiagnosticJsonFormatter.Format(request));

        if (root["dictionary"]?["linkTable"] is JArray links)
        {
            foreach (var link in links)
            {
                RewriteBase64BytesAsHex(link, "traceId");
                RewriteBase64BytesAsHex(link, "spanId");
            }
        }

        var json = root.ToString(Formatting.None);
        if (json.Length > MaxDiagnosticJsonLength)
            json = json.Substring(0, MaxDiagnosticJsonLength) + $"...(truncated, {json.Length} chars total)";

        return json;
    }

    // In-place: if the named property is a base64 string (proto3 `bytes` rendering), replace it with
    // lowercase hex. Leaves non-base64/empty values untouched; never throws.
    private static void RewriteBase64BytesAsHex(JToken owner, string propertyName)
    {
        if (owner[propertyName] is not JValue value || value.Type != JTokenType.String)
            return;

        var base64 = (string)value.Value;
        if (string.IsNullOrEmpty(base64))
            return;

        try
        {
            value.Value = BitConverter.ToString(Convert.FromBase64String(base64)).Replace("-", string.Empty).ToLowerInvariant();
        }
        catch (FormatException)
        {
            // Not base64 -> leave as-is.
        }
    }
}
