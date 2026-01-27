// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Time;

public static class DateTimeMath
{
    public static DateTime Min(DateTime dateTime1, DateTime dateTime2)
    {
        return dateTime1 >= dateTime2 ? dateTime2 : dateTime1;
    }

    public static DateTime Max(DateTime dateTime1, DateTime dateTime2)
    {
        return dateTime1 <= dateTime2 ? dateTime2 : dateTime1;
    }

    public static TimeSpan Min(TimeSpan timespan1, TimeSpan timespan2)
    {
        return timespan1 >= timespan2 ? timespan2 : timespan1;
    }

    public static TimeSpan Max(TimeSpan timespan1, TimeSpan timespan2)
    {
        return timespan1 <= timespan2 ? timespan2 : timespan1;
    }
}
