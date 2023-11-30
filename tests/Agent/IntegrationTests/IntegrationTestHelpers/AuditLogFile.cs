// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class AuditLogFile : AgentLogBase
    {
        private readonly string _filePath;
        private readonly string _fileName;

        public bool Found => File.Exists(_filePath);

        public AuditLogFile(string logDirectoryPath, ITestOutputHelper testLogger, string fileName = "", TimeSpan? timeoutOrZero = null, bool throwIfNotFound = true)
            : base(testLogger)
        {
            Contract.Assert(logDirectoryPath != null);

            _fileName = fileName;

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();

            var searchPattern = _fileName != string.Empty ? _fileName : "*_audit.log";

            do
            {
                var mostRecentlyUpdatedFile = Directory.Exists(logDirectoryPath) ?
                    Directory.GetFiles(logDirectoryPath, searchPattern)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault() : null;

                if (mostRecentlyUpdatedFile != null)
                {
                    _filePath = mostRecentlyUpdatedFile;
                    return;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            } while (timeTaken.Elapsed < timeout);

            if (throwIfNotFound)
                throw new Exception($"Waited {timeout.TotalSeconds:N0}s but didn't find an audit log matching {Path.Combine(logDirectoryPath, searchPattern)}.");
        }

        public override IEnumerable<string> GetFileLines()
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

        public const string AuditLogLinePrefixRegex = @"^.*?NewRelic Audit: ";
        public const string AuditDataSentLogLineRegex = AuditLogLinePrefixRegex + "Data Sent from the InstrumentedApp : .*";
        public const string AuditDataReceivedLogLineRegex = AuditLogLinePrefixRegex + "Data Received from the Collector : .*";
    }
}
