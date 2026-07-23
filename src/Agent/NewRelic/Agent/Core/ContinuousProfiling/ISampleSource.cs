// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

public interface ISampleSource
{
    /// <summary>Copies one available sample batch into <paramref name="destination"/>; returns bytes written (0 if none available).</summary>
    int ReadBatch(byte[] destination);
}
