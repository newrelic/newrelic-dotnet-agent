// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Outcome of an OTLP profiles POST: whether ingest accepted it, the HTTP status code, and the response
/// body. <see cref="OtlpProfilesHttpDispatcher"/> returns this so <see cref="ProfilesTransport"/> can log
/// the send the same way <c>HttpCollectorWire</c> does (payload + response at Debug, plus the audit log).
/// A failed or exception-dropped send is <c>(false, 0, "")</c>.
/// </summary>
public readonly struct ProfilesSendResult
{
    public bool Accepted { get; }
    public int StatusCode { get; }
    public string ResponseContent { get; }

    public ProfilesSendResult(bool accepted, int statusCode, string responseContent)
    {
        Accepted = accepted;
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
