// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.ThreadProfiling;

[JsonConverter(typeof(JsonArrayConverter))]
public class ThreadProfilingModel
{
    [JsonArrayIndex(Index = 0)]
    public readonly int ProfileSessionId;

    [JsonArrayIndex(Index = 1), DateTimeSerializesAsUnixTimeSeconds]
    public readonly DateTime StartTime;

    [JsonArrayIndex(Index = 2), DateTimeSerializesAsUnixTimeSeconds]
    public readonly DateTime StopTime;

    [JsonArrayIndex(Index = 3)]
    public readonly int NumberOfSamples;

    [JsonArrayIndex(Index = 4)]
    public IDictionary<string, object> Samples;

    [JsonArrayIndex(Index = 5)]
    public readonly int TotalThreadCount;

    [JsonArrayIndex(Index = 6)]
    public readonly int RunnableThreadCount;

    public ThreadProfilingModel(int profileSessionId, DateTime startTime, DateTime stopTime, int numberOfSamples, IDictionary<string, object> samples, int totalThreadCount, int runnableThreadCount)
    {
        ProfileSessionId = profileSessionId;
        StartTime = startTime;
        StopTime = stopTime;
        NumberOfSamples = numberOfSamples;
        Samples = samples;
        TotalThreadCount = totalThreadCount;
        RunnableThreadCount = runnableThreadCount;
    }
}