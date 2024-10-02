// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities
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
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        //To/FromUnixTimeMilliseconds implementation taken from:
        //https://github.com/dotnet/coreclr/blob/85374ceaed177f71472cc4c23c69daf7402e5048/src/System.Private.CoreLib/shared/System/DateTimeOffset.cs
        // Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license.
        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;
        private const long TicksPerMinute = TicksPerSecond * 60;
        private const long TicksPerHour = TicksPerMinute * 60;
        private const long TicksPerDay = TicksPerHour * 24;


        private const int DaysPerYear = 365;                            // Number of days in a non-leap year
        private const int DaysPer4Years = DaysPerYear * 4 + 1;          // 1461, Number of days in 4 years
        private const int DaysPer100Years = DaysPer4Years * 25 - 1;     // 36524, Number of days in 100 years
        private const int DaysPer400Years = DaysPer100Years * 4 + 1;    // 146097, Number of days in 400 years

        private const int DaysTo1970 = DaysPer400Years * 4 + DaysPer100Years * 3 + DaysPer4Years * 17 + DaysPerYear; // 719,162, Number of days from 1/1/0001 to 12/31/1969
        public const long UnixEpochTicks = DaysTo1970 * TicksPerDay;

        private const long UnixEpochMilliseconds = UnixEpochTicks / TicksPerMillisecond; // 62,135,596,800,000

        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            var milliseconds = dateTime.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond;
            return milliseconds - UnixEpochMilliseconds;
        }

        public static DateTime FromUnixTimeMilliseconds(this long unixTimeMilliseconds)
        {
            return new DateTime(unixTimeMilliseconds * TicksPerMillisecond + UnixEpochTicks, DateTimeKind.Utc);
        }

        public static double ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (dateTime - Epoch).TotalSeconds;
        }

        public static DateTime ToDateTime(this double secondsSinceEpoch)
        {
            return Epoch.AddSeconds(secondsSinceEpoch);
        }
    }
}
