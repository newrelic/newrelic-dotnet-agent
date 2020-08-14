// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class TracingChainFixture : RemoteApplicationFixture
    {
        private string _applicationDirectoryName;
        private string _executableName;
        private string _targetFramework;

        public RemoteApplication ReceiverApplication { get; set; }
        public RemoteWebApplication SenderApplication => (RemoteWebApplication)RemoteApplication;

        public TracingChainFixture(string ApplicationDirectoryName, string ExecutableName, string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
            _applicationDirectoryName = ApplicationDirectoryName;
            _executableName = ExecutableName;
            _targetFramework = TargetFramework;
        }

        public TracingChainFixture(string ApplicationDirectoryName) : base(new RemoteWebApplication(ApplicationDirectoryName, ApplicationType.Bounded))
        {
            _applicationDirectoryName = ApplicationDirectoryName;
        }

        public override void Initialize()
        {
            base.Initialize();

            WriteApplicationAgentLogToTestLogger(nameof(ReceiverApplication), ReceiverApplication);
        }

        public override void ShutdownRemoteApplication()
        {
            ReceiverApplication.Shutdown();
            ReceiverApplication.CapturedOutput?.WriteProcessOutputToLog($"{nameof(ReceiverApplication)} application:");

            base.ShutdownRemoteApplication();
        }

        public override void Dispose()
        {
            ReceiverApplication.Dispose();
            base.Dispose();
        }

        private void WriteApplicationAgentLogToTestLogger(string applicationName, RemoteApplication application)
        {
            TestLogger?.WriteLine("");
            TestLogger?.WriteLine($"===== Begin {applicationName} log file =====");

            try
            {
                TestLogger?.WriteLine(application.AgentLog.GetFullLogAsString());
            }
            catch (Exception)
            {
                TestLogger?.WriteLine($"No log file found for {applicationName}.");
            }

            TestLogger?.WriteLine("----- End of Agent log file -----");
        }

        public RemoteApplication SetupReceiverApplication(bool isDistributedTracing, bool isWebApplication)
        {
            RemoteApplication receiverApplication;

            if (isWebApplication)
            {
                receiverApplication = new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded);
            }
            else
            {
                receiverApplication = new RemoteService(_applicationDirectoryName, _executableName, _targetFramework, ApplicationType.Bounded, createsPidFile: true, isCoreApp: false, publishApp: false);
            }
            receiverApplication.TestLogger = new XUnitTestLogger(TestLogger);
            receiverApplication.DeleteWorkingSpace();
            receiverApplication.CopyToRemote();

            SetSecrets(receiverApplication.DestinationNewRelicConfigFilePath);

            var configModifier = new NewRelicConfigModifier(receiverApplication.DestinationNewRelicConfigFilePath);
            configModifier.SetLogLevel("all");

            if (isDistributedTracing)
            {
                configModifier.SetOrDeleteDistributedTraceEnabled(true);
                configModifier.SetOrDeleteSpanEventsEnabled(true);
            }
            else
            {
                configModifier.SetOrDeleteDistributedTraceEnabled(false);
                configModifier.SetOrDeleteSpanEventsEnabled(false);
            }

            return receiverApplication;
        }

        public HttpResponseHeaders ExecuteTraceRequestChainHttpWebRequest(IEnumerable<KeyValuePair<string, string>> headers)
        {
            const string action = "Index";
            var queryString = $"?chainedServerName={ReceiverApplication.DestinationServerName}&chainedPortNumber={ReceiverApplication.Port}&chainedAction={action}";

            var address = $"http://{DestinationServerName}:{Port}/Default/Chained{queryString}";

            using (var httpClient = new HttpClient())
            {
                var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
                foreach (var header in headers)
                {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }

                return Task.Run(() => httpClient.SendAsync(httpRequestMessage)).Result.Headers;
            }
        }

        public void ExecuteTraceRequestChainHttpClient(IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            // the test calls the senderUrl, passing in the receiverUrl as a parameter
            var senderBaseUrl = $"http://localhost:{RemoteApplication.Port}";
            var receiverBaseUrl = $"http://localhost:{ReceiverApplication.Port}";

            var receiverUrl = $"{receiverBaseUrl}/api/CallEnd";
            var senderUrl = $"{senderBaseUrl}/api/CallNext?nextUrl={receiverUrl}";

            TestLogger?.WriteLine($"[{nameof(OwinTracingChainFixture)}]: Starting A -> B request chain with URL: {senderUrl}");

            DownloadStringAndAssertContains(senderUrl, "Worked", headers);
        }

        public void ExecuteTraceRequestChainRestSharp(IEnumerable<KeyValuePair<string, string>> headers)
        {
            var secondCallUrl = $"http://localhost:{ReceiverApplication.Port}/api/RestAPI";
            var firstCallUrl = $"http://localhost:{SenderApplication.Port}/DistributedTracing/MakeExternalCallUsingRestClient?externalCallUrl={secondCallUrl}";

            DownloadStringAndAssertEqual(firstCallUrl, "Worked", headers);
        }
    }
}
