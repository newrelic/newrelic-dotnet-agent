// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Google.Protobuf;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Serializes an <see cref="ExportProfilesServiceRequest"/> and dispatches it to the collector
/// via an injected HTTP POST delegate.
/// </summary>
public class ProfilesTransport : IProfilesTransport
{
    // Collector "method" token for the payload log lines, so CP reads like every other collector payload
    // (HttpCollectorWire's `Invoked "<method>"`). No real collector method exists for an OTLP POST; this is
    // the stable identifier tools grep on -- the integration test matches this literal.
    private const string ProfilesMethodName = "continuous_profiling";

    // Compact, single-line protobuf-JSON (proto3 rules: bytes -> base64, enums -> names; default values emitted
    // so the shape matches the OTel dump). No indentation -- like every other collector payload we log.
    private static readonly JsonFormatter DiagnosticJsonFormatter =
        new JsonFormatter(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private readonly Func<byte[], string, ProfilesSendResult> _httpPost;
    private readonly string _endpoint;

    public ProfilesTransport(Func<byte[], string, ProfilesSendResult> httpPost, string endpoint)
    {
        _httpPost = httpPost;
        _endpoint = endpoint;
    }

    public void Send(ExportProfilesServiceRequest request)
    {
        var bytes = request.ToByteArray();
        var requestGuid = Guid.NewGuid();

        // Quick per-drain summary (byte count) at Debug -- the once-per-session "Session started" line is Info.
        var profile = request.ResourceProfiles?.Count > 0 ? "built" : "empty";
        Log.Debug("[ContinuousProfiling] Posting profile ({0}); {1} bytes to {2}.", profile, bytes.Length, _endpoint);

        // Log + audit the send exactly like HttpCollectorWire.SendData so CP payloads are observable like
        // every other collector payload (tools scrape these lines): Finest "Invoking" before the send, the
        // payload and response at Debug, and the audit log for Sent/Received. One requestGuid threads them.
        Log.Finest("Request({0}): Invoking \"{1}\"", requestGuid, ProfilesMethodName);

        // Serialized-payload analog of HttpCollectorWire's serializedData, built once for the Debug line and
        // the audit log -- and only when a sink is listening (the JSON render is not free).
        var payloadJson = (Log.IsDebugEnabled || AuditLog.IsAuditLogEnabled) ? ToDiagnosticJson(request) : null;

        var result = _httpPost(bytes, _endpoint);

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, _endpoint);
        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, payloadJson);

        Log.Debug("Request({0}): Invoked \"{1}\" with : {2}", requestGuid, ProfilesMethodName, payloadJson);
        Log.Debug("Request({0}): Invocation of \"{1}\" yielded response : {2}", requestGuid, ProfilesMethodName, result.ResponseContent);
        if (!result.Accepted)
            Log.Debug("Request({0}): Invocation of \"{1}\" was not accepted (status {2}).", requestGuid, ProfilesMethodName, result.StatusCode);

        DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Received, DataTransportAuditLogger.AuditLogSource.Collector, result.ResponseContent);
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
