// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public interface ISamplingParameters
{
    /// <summary>
    /// The trace ID for the sampling decision. Trace ID is a 16-character hexadecimal string.
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// The initial priority. This is used to adjust the priority if the sample is sampled.
    /// </summary>
    float Priority { get; }

    /// <summary>
    /// The W3C Trace Context information, if available.
    /// </summary>
    W3CTraceContext TraceContext { get; }

    /// <summary>
    /// Indicates whether the New Relic Trace Context was accepted. Applies to W3C Trace Context only.
    /// </summary>
    bool NewRelicTraceContextWasAccepted { get; }

    /// <summary>
    /// The New Relic distributed trace payload, if available
    /// If TraceContext is available, this will be null.
    /// </summary>
    DistributedTracePayload NewRelicPayload { get; }

    /// <summary>
    /// Indicates whether the New Relic distributed trace payload was accepted.
    /// Applies to New Relic distributed trace payload only.
    /// </summary>
    bool NewRelicPayloadWasAccepted { get; }
}
