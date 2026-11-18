// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Globalization;

namespace ArtifactBuilder;

/// <summary>
/// The single timestamp stamped on every entry of the archives and packages we produce, so that
/// two builds of the same commit are byte-identical. Order of precedence: the SOURCE_DATE_EPOCH
/// environment variable (which must be valid when set), the HEAD commit time, then a fixed constant.
/// It is never the current time. The chosen source and value are written to the build log once, and
/// falling back to the constant (git unavailable or failing) is reported as a warning.
/// </summary>
public static class DeterministicTimestamp
{
    public const string SourceDateEpochVariable = "SOURCE_DATE_EPOCH";

    public static readonly DateTimeOffset FallbackTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Zip stores local DOS time: 2 second resolution, years 1980 through 2107.
    private static readonly DateTimeOffset _zipMinimum = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _zipMaximum = new DateTimeOffset(2107, 12, 31, 23, 59, 58, TimeSpan.Zero);

    private static DateTimeOffset? _current;

    public static DateTimeOffset Current => _current ??= Resolve(Environment.GetEnvironmentVariable, GetHeadCommitTime, Console.WriteLine);

    /// <summary>
    /// Throws <see cref="FormatException"/> when SOURCE_DATE_EPOCH is set to something that is not a valid
    /// count of seconds. <paramref name="getHeadCommitTime"/> returns the raw output of the commit time
    /// lookup and throws when the lookup fails.
    /// </summary>
    public static DateTimeOffset Resolve(Func<string, string> getEnvironmentVariable, Func<string> getHeadCommitTime, Action<string> log)
    {
        var epoch = getEnvironmentVariable(SourceDateEpochVariable);
        if (!string.IsNullOrWhiteSpace(epoch))
        {
            var fromEpoch = ParseEpochSeconds(epoch)
                ?? throw new FormatException($"{SourceDateEpochVariable} is set to '{epoch}', which is not a valid number of seconds since 1970-01-01 (0 through 253402300799). Fix or unset it.");

            return Announce(log, $"the {SourceDateEpochVariable} environment variable", fromEpoch);
        }

        string commitTime;
        try
        {
            commitTime = getHeadCommitTime();
        }
        catch (Exception exception)
        {
            return Fallback(log, $"the HEAD commit time could not be read ({exception.Message})");
        }

        var fromCommit = ParseEpochSeconds(commitTime);
        if (fromCommit == null)
        {
            return Fallback(log, $"the HEAD commit time '{commitTime?.Trim()}' is not a valid number of seconds");
        }

        return Announce(log, "the HEAD commit time", fromCommit.Value);
    }

    private static DateTimeOffset Announce(Action<string> log, string source, DateTimeOffset timestamp)
    {
        log($"Deterministic package timestamp: {timestamp:yyyy-MM-dd'T'HH:mm:ss'Z'} (from {source})");

        return timestamp;
    }

    private static DateTimeOffset Fallback(Action<string> log, string reason)
    {
        log($"WARNING: Deterministic package timestamp: using the fixed fallback {FallbackTimestamp:yyyy-MM-dd'T'HH:mm:ss'Z'} because {reason}. "
            + $"Builds of this commit made where git works will not match this one; set {SourceDateEpochVariable} to make it explicit.");

        return FallbackTimestamp;
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
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "log -1 --no-show-signature --format=%ct",
            WorkingDirectory = FileHelpers.GetRepoRootDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        var errorTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();

        if (!process.WaitForExit(10000))
        {
            process.Kill();
            throw new TimeoutException("git log did not finish within 10 seconds");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git log exited with code {process.ExitCode}: {errorTask.Result.Trim()}");
        }

        return output;
    }
}
