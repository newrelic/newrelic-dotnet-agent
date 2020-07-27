using System;
using System.Collections.Generic;
namespace NewRelic.Agent.IntegrationTestHelpers.JsonConverters
{
    public enum TimeUnit
    {
        Ticks,
        Milliseconds,
        Seconds,
        Minutes,
        Hours,
        Days,
        Years,
    }

    public static class DateTimeExtensions
    {
        private readonly static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static double ToUnixTime(this DateTime dateTime)
        {
            return (dateTime - Epoch).TotalSeconds;
        }

        public static DateTime ToDateTime(this double secondsSinceEpoch)
        {
            return Epoch.AddSeconds(secondsSinceEpoch);
        }
    }
}
