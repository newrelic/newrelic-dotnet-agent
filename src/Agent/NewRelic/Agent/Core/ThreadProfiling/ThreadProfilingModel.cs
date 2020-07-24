using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ThreadProfilingModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly Int32 ProfileSessionId;

        [JsonArrayIndex(Index = 1), DateTimeSerializesAsUnixTime]
        public readonly DateTime StartTime;

        [JsonArrayIndex(Index = 2), DateTimeSerializesAsUnixTime]
        public readonly DateTime StopTime;

        [JsonArrayIndex(Index = 3)]
        public readonly Int32 NumberOfSamples;

        [JsonArrayIndex(Index = 4)]
        public IDictionary<String, Object> Samples;

        [JsonArrayIndex(Index = 5)]
        public readonly Int32 TotalThreadCount;

        [JsonArrayIndex(Index = 6)]
        public readonly Int32 RunnableThreadCount;

        public ThreadProfilingModel(Int32 profileSessionId, DateTime startTime, DateTime stopTime, Int32 numberOfSamples, IDictionary<String, Object> samples, Int32 totalThreadCount, Int32 runnableThreadCount)
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
}
