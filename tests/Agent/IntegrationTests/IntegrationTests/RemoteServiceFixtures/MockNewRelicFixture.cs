// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class MockNewRelicFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"MockNewRelic";
        private const string ExecutableName = @"MockNewRelic.exe";
        private const string TargetFramework = "net6";

        public RemoteService MockNewRelicApplication { get; set; }

        public MockNewRelicFixture(RemoteApplication remoteApplication) : base(remoteApplication)
        {
            MockNewRelicApplication = new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true);

            Actions(
                setupConfiguration: () =>
                {
                    //Always restore the New Relic config settings even if the mock collector is already running
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "host", "localhost");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "port", MockNewRelicApplication.Port.ToString(CultureInfo.InvariantCulture));

                    //Increase the timeout for requests to the mock collector to 5 seconds - default is 2 seconds.
                    //This assists in timing issues when spinning up both the mock collector and test application.
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "requestTimeout", "5000");

                    if (MockNewRelicApplication.IsRunning)
                    {
                        return;
                    }

                    var environmentVariables = new Dictionary<string, string>();

                    MockNewRelicApplication.TestLogger = new XUnitTestLogger(TestLogger);
                    MockNewRelicApplication.DeleteWorkingSpace();
                    MockNewRelicApplication.CopyToRemote();
                    MockNewRelicApplication.Start(string.Empty, environmentVariables, doProfile: false);

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    ServicePointManager.ServerCertificateValidationCallback = delegate
                    {
                        //force trust on all certificates for simplicity
                        return true;
                    };

                    //Core apps need the collector warmed up before the core app is started so we cannot
                    //wait until exerciseApplication is called to call these methods
                    WarmUpCollector();
                    LogSslNegotiationMessage();
                }
            );
        }

        public string WarmUpCollector()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/WarmUpCollector";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Warming up collector via: {address}");

            return GetString(address);
        }

        public IEnumerable<CollectedRequest> GetCollectedRequests()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/CollectedRequests";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Get collected requests via: {address}");

            return GetJson<List<CollectedRequest>>(address);
        }

        public void TriggerThreadProfile()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/TriggerThreadProfile";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Trigger thread profile via: {address}");

            GetStringAndIgnoreResult(address);
        }

        public void TriggerCustomInstrumentationEditorAgentCommand()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/TriggerCustomInstrumentationEditorAgentCommand";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Trigger custom instrumentation editor via: {address}");

            GetStringAndIgnoreResult(address);
        }

        public void SetCustomInstrumentationEditorOnConnect()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/SetCustomInstrumentationEditorOnConnect";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Set custom instrumentation editor on connect via: {address}");

            GetStringAndIgnoreResult(address);
        }

        public HeaderValidationData GetRequestHeaderMapValidationData()
        {
            var address = $"https://localhost:{MockNewRelicApplication.Port}/agent_listener/HeaderValidation";

            TestLogger?.WriteLine($"[MockNewRelicFixture] Get request_header_map HeaderValidation via: {address}");

            return GetJson<HeaderValidationData>(address);
        }

        private void LogSslNegotiationMessage()
        {
            TestLogger?.WriteLine($"[MockNewRelicFixture] The MockNewRelic application uses a self-signed cert that will not be trusted " +
                                  $"on most machines. Test applications leveraging this will need to have the proper TLS types enabled and " +
                                  $"will need to override cert validation checks to let the untrusted cert pass. If you encounter an error " +
                                  $"such as 'System.Net.WebException: The underlying connection was closed: Could not establish trust " +
                                  $"relationship for the SSL/TLS secure channel. ---> System.Security.Authentication.AuthenticationException: " +
                                  $"The remote certificate is invalid according to the validation procedure.', you'll need to take those steps. " +
                                  $"See the readme in the root folder of the MockNewRelic application");
        }

        public override void Initialize()
        {
            try
            {
                base.Initialize();

                AgentLog.WaitForLogLine(AgentLogFile.ShutdownLogLineRegex, TimeSpan.FromMinutes(2));
                MockNewRelicApplication.Shutdown();
                MockNewRelicApplication.CapturedOutput?.WriteProcessOutputToLog("MockNewRelic application:");
            }
            catch (Exception)
            {
                MockNewRelicApplication.Shutdown();
                MockNewRelicApplication.CapturedOutput?.WriteProcessOutputToLog("MockNewRelic application:");
                throw;
            }
        }

        public override void ShutdownRemoteApplication()
        {
            base.ShutdownRemoteApplication();
            AgentLog.WaitForLogLine(AgentLogFile.ShutdownLogLineRegex, TimeSpan.FromMinutes(2));
            MockNewRelicApplication.Shutdown();
        }

        public override void Dispose()
        {
            MockNewRelicApplication.Shutdown();
            MockNewRelicApplication.Dispose();
            base.Dispose();
        }

        public override void WriteProcessOutputToLog()
        {
            MockNewRelicApplication.CapturedOutput.WriteProcessOutputToLog("MockNewRelic application:");
            base.WriteProcessOutputToLog();
        }
    }
}
