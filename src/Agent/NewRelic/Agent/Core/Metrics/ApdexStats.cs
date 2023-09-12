// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.SystemExtensions;

namespace NewRelic.Agent.Core.Metrics
{
    public static class ApdexStats
    {
        private const string ApdexPerfZoneSatisfying = "S";
        private const string ApdexPerfZoneTolerating = "T";
        private const string ApdexPerfZoneFrustrating = "F";

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
