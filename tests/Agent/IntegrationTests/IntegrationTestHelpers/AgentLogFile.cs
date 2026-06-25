// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers;

public class AgentLogFile : AgentLogBase
{
    public string FilePath { get; }
    private readonly string _fileName;

    public bool Found => File.Exists(FilePath);

    public AgentLogFile(string logDirectoryPath, ITestOutputHelper testLogger, string fileName = "", TimeSpan? timeoutOrZero = null, bool logFileExpected = true)
        : base(testLogger)
    {
        Contract.Assert(logDirectoryPath != null);

        _fileName = fileName;

        var timeout = timeoutOrZero ?? TimeSpan.Zero;

        var timeTaken = Stopwatch.StartNew();

        var searchPattern = _fileName != string.Empty ? _fileName : "newrelic_agent_*.log";

        if (logFileExpected)
        {
            do
            {
                var mostRecentlyUpdatedFile = Directory.Exists(logDirectoryPath) ?
                    Directory.GetFiles(logDirectoryPath, searchPattern)
                        .Where(file => file != null && !file.Contains("audit"))
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault() : null;

                if (mostRecentlyUpdatedFile != null)
                {
                    FilePath = mostRecentlyUpdatedFile;
                    return;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            } while (timeTaken.Elapsed < timeout);

            throw new Exception($"Waited {timeout.TotalSeconds:N0}s but didn't find an agent log matching {Path.Combine(logDirectoryPath, searchPattern)}.");
        }
    }

    public override IEnumerable<string> GetFileLines()
    {
        if (!Found)
            yield break;

        string line;
        using (var fileStream = OpenLogFileWithRetry())
        using (var streamReader = new StreamReader(fileStream))
            while ((line = streamReader.ReadLine()) != null)
            {
                yield return line;
            }
    }

    // The agent writes to this log concurrently. Even though we open for shared read/write, the
    // writer can momentarily hold the file with an incompatible share mode, causing a transient
    // IOException on open ("being used by another process"). Retry the open briefly so a read
    // that races a write doesn't abort the caller (e.g. WaitForLogLines mid-exercise).
    private FileStream OpenLogFileWithRetry()
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }
    }

    public string GetFullLogAsString()
    {
        using (var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var streamReader = new StreamReader(fileStream))
        {
            return streamReader.ReadToEnd();
        }
    }

    public void ClearLog(TimeSpan? timeoutOrZero = null)
    {
        var timeout = timeoutOrZero ?? TimeSpan.Zero;

        var timeTaken = Stopwatch.StartNew();
        do
        {
            if (!CommonUtils.IsFileLocked(FilePath))
            {
                File.WriteAllText(FilePath, string.Empty);
                return;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        } while (timeTaken.Elapsed < timeout);
    }

    public void DeleteLog(TimeSpan? timeoutOrZero = null)
    {
        var timeout = timeoutOrZero ?? TimeSpan.Zero;

        var timeTaken = Stopwatch.StartNew();
        do
        {
            if (!CommonUtils.IsFileLocked(FilePath))
            {
                File.Delete(FilePath);
                return;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        } while (timeTaken.Elapsed < timeout);
    }

    // Matches every collector request the agent logs at debug level. Group 1 is the collector
    // method (e.g. log_event_data, metric_data, analytic_event_data); group 2 is the raw JSON
    // payload sent for that method.
    private const string PayloadInvocationRegex = @"Request\(.{36}\): Invoked ""([^""]+)"" with : (.*)";

    /// <summary>
    /// Returns every payload sent to the collector as (category, json) pairs, where category is
    /// the collector method name and json is the raw serialized payload, regardless of data type.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetCollectorPayloads()
    {
        var regex = new Regex(PayloadInvocationRegex);

        foreach (var line in GetFileLines())
        {
            var match = regex.Match(line);
            if (match.Success && match.Groups.Count > 2)
            {
                yield return new KeyValuePair<string, string>(match.Groups[1].Value, match.Groups[2].Value);
            }
        }
    }

    public long GetTotalPayloadBytes()
    {
        return GetCollectorPayloads().Sum(payload => (long)Encoding.UTF8.GetByteCount(payload.Value));
    }

    public Dictionary<string, long> GetPayloadBytesByCategory()
    {
        var payloadBytesByCategory = new Dictionary<string, long>();

        foreach (var payload in GetCollectorPayloads())
        {
            var byteCount = Encoding.UTF8.GetByteCount(payload.Value);

            if (payloadBytesByCategory.ContainsKey(payload.Key))
            {
                payloadBytesByCategory[payload.Key] += byteCount;
            }
            else
            {
                payloadBytesByCategory[payload.Key] = byteCount;
            }
        }

        return payloadBytesByCategory;
    }
}