// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using FunctionalTests.Helpers;
using NUnit.Framework;

namespace FunctionalTests
{
    public class TestApplication
    {
        public enum LogEntry { fullyConnected, harvestComplete, transactionTraceData };

        private String _applicationName;
        private string _hostName;

        private String _baseUrlFormatter;

        private String _applicationNameBase;

        private String _accountId;
        public String AccountId { get { return _accountId; } }

        private String _server;

        private String _agentLog;
        public String AgentLog { get { return _agentLog; } }

        private TestServer _tServer;
        public TestServer TServer { get { return _tServer; } }

        public TestApplication(Application application, bool resetServer = false)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var key = new StackTrace().GetFrame(1).GetMethod().DeclaringType.UnderlyingSystemType.FullName;

            if (Common.TestServerContainer.ContainsKey(key))
            {
                _tServer = Common.TestServerContainer[key];
            }
            else
            {
                _tServer = new TestServer(factoryReset: resetServer);
            }
            _server = _tServer.Address;

            _applicationNameBase = _applicationName = application.Name;
            Common.Log($"_applicationNameBase={_applicationNameBase}");
            _baseUrlFormatter = application.BaseUrlFormatter;

            _hostName = _tServer.Address.Replace(".pdx.vm.datanerd.us", String.Empty);
            _applicationName = String.Format("{0}_{1}", _applicationNameBase, _hostName);
            Common.Log($"_applicationName={_applicationName}");

            _accountId = Common.StagingAccountId;
        }

        /// <summary>
        /// Collects the agent-related logs on the test server.
        /// </summary>
        public void GetAgentLogs()
        {
            Console.WriteLine("Attempting to collect logs on '{0}'.", _server);
            var logsPath = (_server != null && (Settings.Environment != Enumerations.EnvironmentSetting.Developer))
                ? String.Format(@"\\{0}\C$\ProgramData\New Relic\.NET Agent\Logs", _server)
                : String.Format(@"{0}\Logs", _tServer.DataPath);

            var searchPatternAgentLog = String.Format("newrelic_agent_*{0}.log", _applicationNameBase);

            var maxWait = TimeSpan.FromMinutes(1.0);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < maxWait)
            {
                if (Directory.Exists(logsPath))
                {
                    Console.WriteLine("-- Logs directory '{0}' found.", logsPath);
                    var agentLogPath = Directory.EnumerateFiles(logsPath, searchPatternAgentLog).Where(log => log != null).FirstOrDefault();

                    _agentLog = agentLogPath == null
                        ? null
                        : FileOperations.ParseTextFile(agentLogPath);

                    if (_agentLog != null)
                    {
                        Console.WriteLine("-- Took {0} seconds for logs to be available.", timer.Elapsed.Seconds);
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("-- '{0}' does not yet exist on '{1}'.", logsPath, _server);
                }

                Thread.Sleep(2500);
                Console.WriteLine("Waiting for logs to be available.");
            }
        }

        public void WaitForLog(LogEntry waitFor)
        {
            var action = String.Empty;
            var lookFor = String.Empty;
            switch (waitFor)
            {
                case LogEntry.fullyConnected:
                    action = "Fully connected";
                    lookFor = " Agent fully connected.";
                    break;
                case LogEntry.harvestComplete:
                    action = "Harvest finished";
                    lookFor = " Metric harvest finished.";
                    break;
                case LogEntry.transactionTraceData:
                    action = "Transaction trace data";
                    lookFor = " TransactionTraceData: ";
                    break;
                default:
                    break;
            }

            var maxWait = TimeSpan.FromMinutes(1.0);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < maxWait)
            {
                Console.WriteLine("Waiting for '{0}' on '{1}'.", action, _applicationName);
                Common.Impersonate(() => GetAgentLogs());

                Assert.NotNull(_agentLog, $"Agent log was not detected in time ({maxWait}) when waiting for: \"{lookFor}\"");

                if (_agentLog.Contains(lookFor))
                    break;
                Thread.Sleep(2000);
            }
            Console.WriteLine("Took {0} seconds for '{1}' on '{2}'.", timer.Elapsed.Seconds, action, _applicationName);
        }

        /// <summary>
        /// Gets an endpoint URI for a given application and resource.
        /// </summary>
        public String GetEndpoint(String resource)
        {
            var baseUrl = String.Format(_baseUrlFormatter, _server);

            return !String.IsNullOrEmpty(resource)
                ? String.Format("{0}{1}", baseUrl, resource)
                : baseUrl;
        }

        /// <summary>
        /// Makes a simple request against a test site.
        /// </summary>
        /// <param name="application">The target application.</param>
        /// <param name="resource">The target resource.</param>
        /// <returns>Response.</returns>
        public String SimpleTestRequest(String resource = null, bool waitForHarvest = false, int? delay = null)
        {
            var client = new WebClient();
            var uri = GetEndpoint(resource);
            Console.WriteLine("Making test request to: {0}", uri);
            var response = client.DownloadString(uri);

            if (waitForHarvest)
                WaitForLog(LogEntry.harvestComplete);

            if (delay != null)
                Thread.Sleep(Convert.ToInt32(delay));

            return response;
        }
    }
}
