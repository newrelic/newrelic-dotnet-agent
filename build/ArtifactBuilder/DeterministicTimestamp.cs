// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Globalization;

namespace ArtifactBuilder;

/// <summary>
/// The single timestamp stamped on every entry of the archives and packages we produce, so that
/// two builds of the same commit are byte-identical. Order of precedence: the SOURCE_DATE_EPOCH
/// environment variable, the HEAD commit time, then a fixed constant. It is never the current time.
/// </summary>
public static class DeterministicTimestamp
{
    public const string SourceDateEpochVariable = "SOURCE_DATE_EPOCH";

    public static readonly DateTimeOffset FallbackTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Zip stores local DOS time: 2 second resolution, years 1980 through 2107.
    private static readonly DateTimeOffset _zipMinimum = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _zipMaximum = new DateTimeOffset(2107, 12, 31, 23, 59, 58, TimeSpan.Zero);

    private static DateTimeOffset? _current;

    public static DateTimeOffset Current => _current ??= Resolve(Environment.GetEnvironmentVariable, GetHeadCommitTime);

    public static DateTimeOffset Resolve(Func<string, string> getEnvironmentVariable, Func<string> getHeadCommitTime)
    {
        return ParseEpochSeconds(getEnvironmentVariable(SourceDateEpochVariable))
            ?? ParseEpochSeconds(getHeadCommitTime())
            ?? FallbackTimestamp;
    }

    /// <summary>
    /// The timestamp adjusted to what a zip entry can store (rounded down to an even second, clamped
    /// to the supported years), expressed in UTC so no time zone conversion can change the stored value.
    /// </summary>
    public static DateTimeOffset ToZipTimestamp(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        if (utc < _zipMinimum) return _zipMinimum;
        if (utc > _zipMaximum) return _zipMaximum;

        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second - utc.Second % 2, TimeSpan.Zero);
    }

    private static DateTimeOffset? ParseEpochSeconds(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (!long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return null;

        // 253402300799 is 9999-12-31T23:59:59Z, the last second DateTimeOffset can represent
        if (seconds > 253402300799) return null;

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static string GetHeadCommitTime()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "log -1 --format=%ct",
                WorkingDirectory = FileHelpers.GetRepoRootDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(10000);

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
