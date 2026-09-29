// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// Why a send never produced a real HTTP status (i.e. <see cref="ProfilesSendResult.StatusCode"/> is 0).
/// A real HTTP rejection (401/403/404/413/5xx/etc.) carries its status and reports <see cref="None"/>;
/// these three cases all short-circuit to status 0 for distinct, differently-actionable reasons, so the
/// rejection warning can name which one actually happened instead of an ambiguous "status 0".
/// </summary>
public enum ProfilesSendFailureReason
{
    /// <summary>The send produced a real HTTP status (or succeeded); no status-0 cause applies.</summary>
    None = 0,

    /// <summary>The configured endpoint was null/empty or not a well-formed absolute URI; nothing was sent.</summary>
    InvalidEndpoint,

    /// <summary>The compressed payload exceeded the configured max payload size and was dropped client-side without a send.</summary>
    OversizedPayloadDropped,

    /// <summary>The send threw at the transport layer (network/proxy/TLS/timeout), including retries being exhausted by an exception.</summary>
    TransportException,
}

/// <summary>
/// Outcome of an OTLP profiles POST: whether ingest accepted it, the HTTP status code, and the response
/// body. <see cref="OtlpProfilesHttpDispatcher"/> returns this so <see cref="ProfilesTransport"/> can log
/// the send the same way <c>HttpCollectorWire</c> does (payload + response at Debug, plus the audit log).
/// A failed or exception-dropped send is <c>(false, 0, "")</c>, with <see cref="FailureReason"/> naming
/// which status-0 cause it was.
///
/// <see cref="RejectedProfiles"/> / <see cref="PartialSuccessErrorMessage"/> surface OTLP
/// <c>ExportProfilesPartialSuccess</c> when the response body is protobuf -- diagnostics only. Per the
/// OTLP spec, partial success is not a delivery failure, so <see cref="Accepted"/> stays HTTP-status-only,
/// matching every other send path in the agent (<c>HttpCollectorWire</c>, the OTLP Metrics bridge).
/// </summary>
public readonly struct ProfilesSendResult
{
    public bool Accepted { get; }
    public int StatusCode { get; }
    public string ResponseContent { get; }
    public long RejectedProfiles { get; }
    public string PartialSuccessErrorMessage { get; }
    public ProfilesSendFailureReason FailureReason { get; }

    /// <summary>
    /// Bytes actually written to the wire for this send -- the gzip-compressed body
    /// <see cref="OtlpProfilesHttpDispatcher.BuildRequestMessage"/> POSTs, not the pre-compression protobuf
    /// size. <see cref="ProfilesTransport"/> reports this (not the serialized request's uncompressed
    /// length) to <c>ReportSupportabilityDataUsage</c> so egress-volume supportability metrics reflect
    /// what was actually sent.
    /// </summary>
    public long SentBytes { get; }

    public ProfilesSendResult(bool accepted, int statusCode, string responseContent, long rejectedProfiles = 0, string partialSuccessErrorMessage = "", ProfilesSendFailureReason failureReason = ProfilesSendFailureReason.None, long sentBytes = 0)
    {
        Accepted = accepted;
        StatusCode = statusCode;
        ResponseContent = responseContent;
        RejectedProfiles = rejectedProfiles;
        PartialSuccessErrorMessage = partialSuccessErrorMessage ?? string.Empty;
        FailureReason = failureReason;
        SentBytes = sentBytes;
    }
}
