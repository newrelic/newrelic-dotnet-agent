// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Plan-A placeholder sample source: never produces data, so the continuous-profiling drain loop
/// stays inert. Plan B replaces this with the native sampler-backed implementation.
/// </summary>
public class NoOpSampleSource : ISampleSource
{
    public int ReadBatch(byte[] destination) => 0;
}
