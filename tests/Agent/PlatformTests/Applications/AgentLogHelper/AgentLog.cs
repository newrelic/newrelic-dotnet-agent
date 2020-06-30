/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgentLogHelper
{
    public class AgentLog
    {
        private const string ProfilerLogNamePrefix = "NewRelic.Profiler.";
        private const string ConnectString = @"Invoking ""connect"" with : [";

        private readonly int _processId;
        private readonly string _agentLogPath;
        private readonly string _profilerLogPath;

        public AgentLog(string agentLogFolderPath)
        {
            var searchPattern = "newrelic_agent_*.log";
            var mostRecentlyUpdatedFile = Directory.EnumerateFiles(agentLogFolderPath, searchPattern)
                .Where(file => file != null && !file.Contains("audit"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            _agentLogPath = mostRecentlyUpdatedFile;
            _processId = Process.GetCurrentProcess().Id;
            _profilerLogPath = Path.Combine(agentLogFolderPath, ProfilerLogNamePrefix + _processId + ".log");
        }

        public string GetAgentLog(string hasString = ConnectString)
        {
            var logString = String.Empty;
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (stopWatch.Elapsed < TimeSpan.FromMinutes(3))
            {
                try
                {
                    logString = Readfile(_agentLogPath);
                    var logLines = logString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in logLines)
                    {
                        if (line.Contains(hasString))
                        {
                            return logString;
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {

                }

                Thread.Sleep(40000);
            }

            stopWatch.Stop();
            return logString;
        }

        public string GetProfilerLog()
        {
            return Readfile(_profilerLogPath);
        }


        private string Readfile(string filePath)
        {
            using (var filestream = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return filestream.ReadToEnd();
            }
        }
    }
}
