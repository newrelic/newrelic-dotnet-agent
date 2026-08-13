// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using Google.Protobuf;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Serializes an <see cref="ExportProfilesServiceRequest"/> and dispatches it to the collector
/// via an injected HTTP POST delegate. Reports a data-usage supportability metric on acceptance,
/// same as every other collector/OTLP send path (<c>HttpCollectorWire.SendData</c>, <c>OtlpAuditHandler</c>).
/// </summary>
public class ProfilesTransport : IProfilesTransport
{
    // Collector "method" token for the payload log lines, so CP reads like every other collector payload
    // (HttpCollectorWire's `Invoked "<method>"`). No real collector method exists for an OTLP POST; this is
    // the stable identifier tools grep on -- the integration test matches this literal.
    private const string ProfilesMethodName = "continuous_profiling";

    // Destination/area for ReportSupportabilityDataUsage -- mirrors OtlpAuditHandler's ("OTLP", "Metrics")
    // for the Meter bridge, CP's closest sibling (same OTLP send shape). Produces a parallel
    // Supportability/DotNET/OTLP/Profiles/Output/Bytes metric alongside the existing .../OTLP/Metrics one.
    private const string DataUsageApi = "OTLP";
    private const string DataUsageArea = "Profiles";

    /// <summary>
    /// Hard ceiling on one serialized profiles POST. A drain used to carry a single allocation sample -- a
    /// measured 2101-byte request -- and once allocation samples batch properly it carries as many as the
    /// configured budget allows (a measured 9784 bytes for ~100 samples, i.e. ~88 bytes marginal each), so
    /// this path needed an upper bound rather than trusting the producer. Conservative on purpose: OTLP
    /// ingest endpoints commonly reject requests around this size, and a profile is disposable (never
    /// retried), so refusing to ship an oversized one costs one drain and nothing else.
    ///
    /// This is the LAST line of defence, not the primary one -- <c>ContinuousProfilingService</c> caps
    /// samples per drain before building, so a payload reaching this check means that cap is mis-sized for
    /// the workload's stack diversity (the per-sample byte cost differs by ~25x between a repeated stack and
    /// wholly distinct ones). Public so the cap and this ceiling can be reasoned about (and tested) together.
    /// </summary>
    public const int MaxPayloadBytes = 1024 * 1024;

    /// <summary>
    /// How many samples per profile the DIAGNOSTIC JSON dump includes. That dump is rendered whenever
    /// Debug logging is on -- which is routine support advice, not an exotic setting -- and it used to be
    /// one sample per drain. Rendering (and writing to the log file) an unbounded document per drain is a
    /// real cost paid inside the customer's process, so the dump is now sample-bounded: enough to show the
    /// shape, the dictionary, the link table and the attributes, without scaling with throughput.
    /// The AUDIT log is deliberately exempt (see <see cref="Send"/>).
    /// </summary>
    public const int MaxDiagnosticSamplesPerProfile = 25;

    private const string SupportabilityPayloadTooLargeMetric = "Supportability/DotNET/ContinuousProfiling/PayloadTooLarge";

    // Compact, single-line protobuf-JSON (proto3 rules: bytes -> base64, enums -> names; default values emitted
    // so the shape matches the OTel dump). No indentation -- like every other collector payload we log.
    private static readonly JsonFormatter DiagnosticJsonFormatter =
        new JsonFormatter(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private readonly Func<byte[], string, ProfilesSendResult> _httpPost;
    private readonly IAgentHealthReporter _agentHealthReporter;

    // volatile: swapped by UpdateEndpoint (e.g. on AgentConnectedEvent) on a different thread than the
    // scheduler thread that reads it in Send; a plain field would risk a stale read across cores.
    private volatile string _endpoint;

    public ProfilesTransport(Func<byte[], string, ProfilesSendResult> httpPost, string endpoint, IAgentHealthReporter agentHealthReporter)
    {
        _httpPost = httpPost;
        _endpoint = endpoint;
        _agentHealthReporter = agentHealthReporter;
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

        // Refuse rather than ship an oversized POST. Reported as a failed send on purpose: that feeds the
        // service's send-failure backoff, which pauses sampling briefly -- exactly the right reflex for "we
        // are producing more than the wire will take", and it self-corrects because the next drain's payload
        // is built from a fresh (smaller) sweep.
        if (bytes.Length > MaxPayloadBytes)
        {
            Log.Debug("[ContinuousProfiling] Dropping profile: {0} bytes exceeds the {1}-byte payload ceiling.", bytes.Length, MaxPayloadBytes);
            _agentHealthReporter?.ReportSupportabilityCountMetric(SupportabilityPayloadTooLargeMetric);
            return false;
        }

        // Log + audit the send exactly like HttpCollectorWire.SendData so CP payloads are observable like
        // every other collector payload (tools scrape these lines): Finest "Invoking" before the send, the
        // payload and response at Debug, and the audit log for Sent/Received. One requestGuid threads them.
        Log.Finest("Request({0}): Invoking \"{1}\"", requestGuid, ProfilesMethodName);

        // Serialized-payload analog of HttpCollectorWire's serializedData, built once for the Debug line and
        // the audit log -- and only when a sink is listening (the JSON render is not free).
        //
        // Two sinks, two policies, deliberately:
        //   * AUDIT LOG is an explicit "capture the exact payloads" switch, so it still gets the whole thing.
        //   * DEBUG logging is routine support advice, so it gets a sample-bounded copy. Before allocation
        //     samples batched, a drain's dump held one sample; it now holds as many as the budget delivers,
        //     and rendering + writing that per drain is overhead inside the customer's process for a log
        //     nobody reads to the end. The dictionary/stringTable/linkTable are NOT truncated, so the dump
        //     stays a valid, greppable document with every frame name and trace id still in it.
        var omittedSamples = 0;
        string payloadJson = null;
        if (AuditLog.IsAuditLogEnabled)
            payloadJson = ToDiagnosticJson(request);
        else if (Log.IsDebugEnabled)
            payloadJson = ToDiagnosticJson(TruncateSamplesForDiagnostics(request, MaxDiagnosticSamplesPerProfile, out omittedSamples));

        var result = _httpPost(bytes, _endpoint);

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, _endpoint);
        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, payloadJson);

        Log.Debug("Request({0}): Invoked \"{1}\" with : {2}", requestGuid, ProfilesMethodName, payloadJson);
        if (omittedSamples > 0)
            Log.Debug("Request({0}): the logged payload omits {1} sample(s) per the {2}-sample-per-profile diagnostic cap; the POSTed payload carried all of them.",
                requestGuid, omittedSamples, MaxDiagnosticSamplesPerProfile);
        Log.Debug("Request({0}): Invocation of \"{1}\" yielded response : {2}", requestGuid, ProfilesMethodName, result.ResponseContent);
        if (!result.Accepted)
            Log.Debug("Request({0}): Invocation of \"{1}\" was not accepted (status {2}).", requestGuid, ProfilesMethodName, result.StatusCode);

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Received, DataTransportAuditLogger.AuditLogSource.Collector, result.ResponseContent);

        // Data-usage supportability metric, same as every other OTLP/collector send (HttpCollectorWire.
        // SendData, OtlpAuditHandler) -- reported on acceptance only, matching both of those.
        if (result.Accepted)
        {
            var bytesReceived = Encoding.UTF8.GetByteCount(result.ResponseContent ?? string.Empty);
            _agentHealthReporter?.ReportSupportabilityDataUsage(DataUsageApi, DataUsageArea, bytes.Length, bytesReceived);
        }

        return result.Accepted;
    }

    /// <summary>
    /// Returns a copy of <paramref name="request"/> with each profile's sample list capped at
    /// <paramref name="maxSamplesPerProfile"/>, for DIAGNOSTIC rendering only -- the real payload is never
    /// touched. <paramref name="omitted"/> reports how many samples were left out across all profiles.
    /// Returns the original instance (and 0) when nothing needs truncating, so the common case copies
    /// nothing. Public + static so it is unit-testable without the static logger.
    /// </summary>
    public static ExportProfilesServiceRequest TruncateSamplesForDiagnostics(ExportProfilesServiceRequest request, int maxSamplesPerProfile, out int omitted)
    {
        omitted = 0;
        if (request == null)
            return null;

        // Count first: cloning a large request is the expensive part, so do it only if it buys something.
        foreach (var resourceProfiles in request.ResourceProfiles)
            foreach (var scopeProfiles in resourceProfiles.ScopeProfiles)
                foreach (var profile in scopeProfiles.Profiles)
                    if (profile.Samples.Count > maxSamplesPerProfile)
                        omitted += profile.Samples.Count - maxSamplesPerProfile;

        if (omitted == 0)
            return request;

        var truncated = request.Clone();
        foreach (var resourceProfiles in truncated.ResourceProfiles)
            foreach (var scopeProfiles in resourceProfiles.ScopeProfiles)
                foreach (var profile in scopeProfiles.Profiles)
                    while (profile.Samples.Count > maxSamplesPerProfile)
                        profile.Samples.RemoveAt(profile.Samples.Count - 1);

        return truncated;
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

        return root.ToString(Formatting.None);
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
