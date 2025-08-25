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
}
