/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Metric
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ApdexStats
    {
        private const string ApdexPerfZoneSatisfying = "S";
        private const string ApdexPerfZoneTolerating = "T";
        private const string ApdexPerfZoneFrustrating = "F";

        private TimeSpan _apdexT = TimeSpan.Zero;
        public static string GetApdexPerfZoneOrNull(TimeSpan? responseTime, TimeSpan? apdexT)
        {
            if (responseTime == null || apdexT == null)
                return null;

            if (responseTime.Value.Ticks <= apdexT.Value.Ticks)
                return ApdexPerfZoneSatisfying;

            return responseTime.Value.Ticks <= apdexT.Value.Multiply(4).Ticks ? ApdexPerfZoneTolerating : ApdexPerfZoneFrustrating;
        }
    }
}
