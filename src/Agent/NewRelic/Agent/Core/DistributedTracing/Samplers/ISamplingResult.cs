// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public interface ISamplingResult
{
    /// <summary>
    /// True if the sample was sampled, false if it was not.
    /// </summary>
    bool Sampled { get; }

    /// <summary>
    /// Priority of the sample. Will be the original priority if not sampled, or an adjusted priority if sampled.
    /// </summary>
    float Priority { get; }
}
