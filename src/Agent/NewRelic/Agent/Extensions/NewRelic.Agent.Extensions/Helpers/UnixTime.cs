// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Canonical Unix-epoch conversions shared across the agent. Lives in the Extensions
/// assembly so lower-level code (such as <see cref="QueueTimeHeaderParser"/>) and the
/// agent Core can share a single definition of the epoch and the seconds-based
/// conversions instead of each keeping its own copy.
/// </summary>
public static class UnixTime
{
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Seconds elapsed since the Unix epoch for <paramref name="dateTime"/>.</summary>
    public static double ToSeconds(DateTime dateTime) => (dateTime - Epoch).TotalSeconds;

    /// <summary>The UTC <see cref="DateTime"/> that is <paramref name="secondsSinceEpoch"/> seconds after the Unix epoch.</summary>
    public static DateTime FromSeconds(double secondsSinceEpoch) => Epoch.AddSeconds(secondsSinceEpoch);
}
