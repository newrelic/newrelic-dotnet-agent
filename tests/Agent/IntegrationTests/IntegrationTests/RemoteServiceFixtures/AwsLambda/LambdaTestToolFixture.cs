// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public class LambdaTestToolFixture : RemoteApplicationFixture
    {
        public DotnetTool LambdaTestTool { get; set; }
        public Action AdditionalSetupConfiguration { get; set; }

        public LambdaTestToolFixture(RemoteApplication remoteApplication, string newRelicLambdaHandler, string lambdaHandler, string lambdaName, string lambdaVersion, string lambdaExecutionEnvironment) : base(remoteApplication)
        {
            LambdaTestTool = new DotnetTool("Amazon.Lambda.TestTool-8.0", "lambda-test-tool-8.0", DestinationApplicationDirectoryPath);

            if (string.IsNullOrWhiteSpace(newRelicLambdaHandler) && string.IsNullOrWhiteSpace(lambdaHandler))
            {
                throw new Exception("At least one of newRelicLambdaHandler and lambdaHandler should be specified.");
            }

            Actions(
                setupConfiguration: () =>
                {
                    //Always restore the New Relic config settings even if the lambda test tool is already running
                    SetAdditionalEnvironmentVariable("NEW_RELIC_SERVERLESS_MODE_ENABLED", "1");
                    SetAdditionalEnvironmentVariable("NEW_RELIC_ACCOUNT_ID", TestConfiguration.NewRelicAccountId);
                    SetAdditionalEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{LambdaTestTool.Port}");

                    AddAdditionalEnvironmentVariableIfNotNull("NEW_RELIC_LAMBDA_HANDLER", newRelicLambdaHandler);
                    AddAdditionalEnvironmentVariableIfNotNull("_HANDLER", lambdaHandler);
                    AddAdditionalEnvironmentVariableIfNotNull("AWS_LAMBDA_FUNCTION_NAME", lambdaName);
                    AddAdditionalEnvironmentVariableIfNotNull("AWS_LAMBDA_FUNCTION_VERSION", lambdaVersion);
                    AddAdditionalEnvironmentVariableIfNotNull("AWS_EXECUTION_ENV", lambdaExecutionEnvironment);

                    // Finest level logs are necessary to read the uncompressed payloads from the agent logs
                    remoteApplication.NewRelicConfig.SetLogLevel("finest");

                    AdditionalSetupConfiguration?.Invoke();

                    if (LambdaTestTool.IsRunning)
                    {
                        return;
                    }

                    var lambdaTestToolArguments = $"--port {LambdaTestTool.Port} --no-launch-window";
                    var environmentVariables = new Dictionary<string, string>();
                    LambdaTestTool.TestLogger = new XUnitTestLogger(TestLogger);
                    LambdaTestTool.CopyToRemote();
                    LambdaTestTool.Start(lambdaTestToolArguments, environmentVariables, doProfile: false);

                    WarmUpTestTool();
                }
            );
        }

        private void AddAdditionalEnvironmentVariableIfNotNull(string name, string value)
        {
            if (value != null)
            {
                SetAdditionalEnvironmentVariable(name, value);
            }
        }

        private void WarmUpTestTool()
        {

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var address = $"http://localhost:{LambdaTestTool.Port}";

                    TestLogger?.WriteLine($"[LambdaTestToolFixture] Warming up lambda test tool via: {address} attempt {attempt}.");

                    GetString(address);
                    return;
                }
                catch(Exception e)
                {
                    TestLogger?.WriteLine($"Unable to warm up lambda test tool during attempt {attempt}. Exception: {e}");
                    if (attempt == 3)
                    {
                        // Halt the test because the test tool was unable to start
                        throw;
                    }

                    // Wait a little to give the test tool more time to start
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }
        }

        public override void Initialize()
        {
            try
            {
                base.Initialize();

                AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromMinutes(2));
                LambdaTestTool.Shutdown();
                LambdaTestTool.CapturedOutput?.WriteProcessOutputToLog("LambdaTestToolFixture application:");
            }
            catch (Exception)
            {
                LambdaTestTool.Shutdown();
                LambdaTestTool.CapturedOutput?.WriteProcessOutputToLog("LambdaTestToolFixture application:");
                throw;
            }
        }

        public override void ShutdownRemoteApplication()
        {
            base.ShutdownRemoteApplication();
            AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromMinutes(2));
            LambdaTestTool.Shutdown();
        }

        public override void Dispose()
        {
            LambdaTestTool.Shutdown();
            LambdaTestTool.Dispose();
            base.Dispose();
        }

        public override void WriteProcessOutputToLog()
        {
            LambdaTestTool.CapturedOutput.WriteProcessOutputToLog("LambdaTestToolFixture application:");
            base.WriteProcessOutputToLog();
        }

        protected void EnqueueLambdaEvent(string eventJson)
        {
            var content = new StringContent(eventJson, Encoding.UTF8, "application/json");
            using var response = _httpClient.PostAsync($"http://localhost:{LambdaTestTool.Port}/runtime/test-event", content).Result;
            response.EnsureSuccessStatusCode();
        }
    }
}
