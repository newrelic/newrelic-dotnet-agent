/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class AgentLogFile : AgentLogBase
    {
        private readonly string _filePath;
        private readonly string _fileName;

        public AgentLogFile(string logDirectoryPath, string fileName, TimeSpan? timeoutOrZero = null)
        {
            Contract.Assert(logDirectoryPath != null);

            _fileName = fileName;

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();

            var searchPattern = _fileName != string.Empty ? _fileName : "newrelic_agent_*.log";

            do
            {
                var mostRecentlyUpdatedFile = Directory.GetFiles(logDirectoryPath, searchPattern)
                    .Where(file => file != null && !file.Contains("audit"))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (mostRecentlyUpdatedFile != null)
                {
                    _filePath = mostRecentlyUpdatedFile;
                    return;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            } while (timeTaken.Elapsed < timeout);

            throw new Exception("No agent log file found.");
        }

        public override IEnumerable<string> GetFileLines()
        {
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
