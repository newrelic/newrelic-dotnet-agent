// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Dispatches a built <see cref="ExportProfilesServiceRequest"/> to the collector.
/// </summary>
public interface IProfilesTransport
{
    void Send(ExportProfilesServiceRequest request);

    /// <summary>
    /// Swaps the endpoint subsequent <see cref="Send"/> calls POST to. No-op for a null/empty value.
    /// </summary>
    void UpdateEndpoint(string endpoint);
}
