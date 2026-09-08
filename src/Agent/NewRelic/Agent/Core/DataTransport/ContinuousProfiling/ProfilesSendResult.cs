// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// Outcome of an OTLP profiles POST: whether ingest accepted it, the HTTP status code, and the response
/// body. <see cref="OtlpProfilesHttpDispatcher"/> returns this so <see cref="ProfilesTransport"/> can log
/// the send the same way <c>HttpCollectorWire</c> does (payload + response at Debug, plus the audit log).
/// A failed or exception-dropped send is <c>(false, 0, "")</c>.
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

    public ProfilesSendResult(bool accepted, int statusCode, string responseContent, long rejectedProfiles = 0, string partialSuccessErrorMessage = "")
    {
        Accepted = accepted;
        StatusCode = statusCode;
        ResponseContent = responseContent;
        RejectedProfiles = rejectedProfiles;
        PartialSuccessErrorMessage = partialSuccessErrorMessage ?? string.Empty;
    }
}
