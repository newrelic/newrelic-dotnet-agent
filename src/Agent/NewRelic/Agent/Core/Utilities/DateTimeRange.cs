// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities;

public struct DateTimeRange
{
    public readonly TimeSpan StartTime;
    public readonly TimeSpan EndTime;

    public TimeSpan Duration => EndTime - StartTime;

    public DateTimeRange(TimeSpan startTime, TimeSpan endTime) : this()
    {
        if (endTime < startTime)
            throw new ArgumentException($"{nameof(endTime)} ({endTime}) is not after {nameof(startTime)} ({startTime})");

        this.StartTime = startTime;
        this.EndTime = endTime;
    }

    public bool Overlaps(DateTimeRange other)
    {
        return StartTime <= other.EndTime && other.StartTime <= EndTime;
    }
}