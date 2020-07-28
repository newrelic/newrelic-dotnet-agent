using System;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Metric
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ApdexStats
    {
        private const String ApdexPerfZoneSatisfying = "S";
        private const String ApdexPerfZoneTolerating = "T";
        private const String ApdexPerfZoneFrustrating = "F";

        private TimeSpan _apdexT = TimeSpan.Zero;
        public static String GetApdexPerfZoneOrNull(TimeSpan? responseTime, TimeSpan? apdexT)
        {
            if (responseTime == null || apdexT == null)
                return null;

            if (responseTime.Value.Ticks <= apdexT.Value.Ticks)
                return ApdexPerfZoneSatisfying;

            return responseTime.Value.Ticks <= apdexT.Value.Multiply(4).Ticks ? ApdexPerfZoneTolerating : ApdexPerfZoneFrustrating;
        }
    }
}
