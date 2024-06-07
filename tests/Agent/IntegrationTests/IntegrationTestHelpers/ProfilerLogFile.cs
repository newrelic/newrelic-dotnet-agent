// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    /// <summary>
    /// This class contains methods to parse the profiler log for an application.
    /// </summary>
    public class ProfilerLogFile
    {
        public const string InstrumentationRefreshComplete = @"^.*? Leave: InstrumentationRefresh";

        private readonly string _filePath;

        public bool Found => File.Exists(_filePath);

        public ProfilerLogFile(string logDirectoryPath, TimeSpan? timeoutOrZero = null, bool logFileExpected = true)
        {
            Contract.Assert(logDirectoryPath != null);

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();

            if (logFileExpected)
            {
                do
                {
                    var mostRecentlyUpdatedFile = Directory.Exists(logDirectoryPath) ?
                        Directory.EnumerateFiles(logDirectoryPath, "NewRelic.Profiler.*.log")
                            .Where(file => file != null)
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .FirstOrDefault() : null;

                    if (mostRecentlyUpdatedFile != null)
                    {
                        _filePath = mostRecentlyUpdatedFile;
                        return;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                } while (timeTaken.Elapsed < timeout);

                throw new Exception("No profiler log file found.");
            }

        }

        #region Log Lines


        public IEnumerable<Match> WaitForLogLines(string regularExpression, TimeSpan? timeoutOrZero = null)
        {
            Contract.Assert(regularExpression != null);

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();
            do
            {
                var matches = TryGetLogLines(regularExpression).ToList();
                if (matches.Any())
                    return matches;
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);

            var message = string.Format("Log line did not appear within {0} seconds.  Expected line expression: {1}", timeout.TotalSeconds, regularExpression);
            throw new Exception(message);
        }


        public Match WaitForLogLine(string regularExpression, TimeSpan? timeoutOrZero = null)
        {
            return WaitForLogLines(regularExpression, timeoutOrZero).First();
        }


        public IEnumerable<Match> TryGetLogLines(string regularExpression)
        {
            var regex = new Regex(regularExpression);
            return GetFileLines()
                .Where(line => line != null)
                .Select(line => regex.Match(line))
                .Where(match => match != null)
                .Where(match => match.Success);
        }


        public Match TryGetLogLine(string regularExpression)
        {
            return TryGetLogLines(regularExpression).LastOrDefault();
        }


        public IEnumerable<string> GetFileLines()
        {
            if (!Found)
                yield break;

            string line;
            using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream))
                while ((line = streamReader.ReadLine()) != null)
                {
                    yield return line;
                }
        }


        public string GetFullLogAsString()
        {
            using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream))
            {
                return streamReader.ReadToEnd();
            }
        }

        #endregion Log Lines

        #region Rejit

        /// <summary>
        /// The method checks if Rejit has been completed in the profiler logs.
        /// </summary>
        /// <param name="timeOut">How long to wait before timing out. Default is 20 seconds.</param>
        /// <returns></returns>

        public bool GetInstrumentationRefreshComplete(int timeOut = 80)
        {
            var match = WaitForLogLine(InstrumentationRefreshComplete, TimeSpan.FromSeconds(timeOut));
            return match.Success;
        }

        #endregion

        public void ClearLog(TimeSpan? timeoutOrZero = null)
        {
            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();
            do
            {
                if (!CommonUtils.IsFileLocked(_filePath))
                {
                    File.WriteAllText(_filePath, string.Empty);
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
                if (!CommonUtils.IsFileLocked(_filePath))
                {
                    File.Delete(_filePath);
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);
        }
    }
}
