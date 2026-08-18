// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// Dispatches a built <see cref="ExportProfilesServiceRequest"/> to the collector.
/// </summary>
public interface IProfilesTransport
{
    /// <summary>Sends the request. Returns whether it was accepted, so callers can react to failures.</summary>
    bool Send(ExportProfilesServiceRequest request);

    /// <summary>
    /// Swaps the endpoint subsequent <see cref="Send"/> calls POST to. No-op for a null/empty value.
    /// </summary>
    void UpdateEndpoint(string endpoint);
}
