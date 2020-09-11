// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public abstract class RemoteApplicationFixture : IDisposable
    {
        public virtual string TestSettingCategory { get { return "Default"; } }

        private Action _setupConfiguration;
        private Action _exerciseApplication;

        private bool _initialized;

        public int? ExitCode => RemoteApplication?.ExitCode;

        private readonly object _initializeLock = new object();

        public readonly RemoteApplication RemoteApplication;

        public AgentLogFile AgentLog { get { return RemoteApplication.AgentLog; } }

        public string DestinationServerName { get { return RemoteApplication.DestinationServerName; } }

        public string Port { get { return RemoteApplication.Port; } }

        public string CommandLineArguments { get; set; }

        public string DestinationNewRelicConfigFilePath { get { return RemoteApplication.DestinationNewRelicConfigFilePath; } }

        public string DestinationApplicationDirectoryPath { get { return RemoteApplication.DestinationApplicationDirectoryPath; } }

        public string DestinationNewRelicExtensionsDirectoryPath => RemoteApplication.DestinationNewRelicExtensionsDirectoryPath;

        public ITestOutputHelper TestLogger { get; set; }


        public bool DelayKill;

        public bool BypassAgentConnectionErrorLineRegexCheck;

        private const int MaxTries = 2;

        public void DisableAsyncLocalCallStack()
        {
            var deletingFile = DestinationNewRelicExtensionsDirectoryPath + @"\NewRelic.Providers.CallStack.AsyncLocal.dll";
            if (File.Exists(deletingFile))
            {
                File.Delete(deletingFile);
            }
        }

        private IntegrationTestConfiguration _testConfiguration;

        public IntegrationTestConfiguration TestConfiguration
        {
            get
            {
                if (_testConfiguration == null)
                {
                    _testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration(TestSettingCategory);
                }

                return _testConfiguration;
            }
        }

        protected RemoteApplicationFixture(RemoteApplication remoteApplication)
        {
            RemoteApplication = remoteApplication;
        }

        public void Actions(Action setupConfiguration = null, Action exerciseApplication = null)
        {
            if (setupConfiguration != null)
                _setupConfiguration = setupConfiguration;

            if (exerciseApplication != null)
                _exerciseApplication = exerciseApplication;
        }

        public void AddActions(Action setupConfiguration = null, Action exerciseApplication = null)
        {
            if (setupConfiguration != null)
            {
                var oldSetupConfiguration = _setupConfiguration;
                _setupConfiguration = () =>
                {
                    oldSetupConfiguration?.Invoke();
                    setupConfiguration();
                };
            }

            if (exerciseApplication != null)
            {
                var oldExerciseApplication = _exerciseApplication;
                _exerciseApplication = () =>
                {
                    oldExerciseApplication?.Invoke();
                    exerciseApplication();
                };
            }
        }

        private void SetupConfiguration()
        {
            SetSecrets(DestinationNewRelicConfigFilePath);

            if (_setupConfiguration != null)
                _setupConfiguration();
        }

        protected void SetSecrets(string destinationNewRelicConfigFilePath)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "licenseKey", TestConfiguration.LicenseKey);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "host", TestConfiguration.CollectorUrl);
            if (TestSettingCategory == "CSP")
            {
                var securityPoliciesToken = "ffff-ffff-ffff-ffff";
                CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration" }, "securityPoliciesToken", securityPoliciesToken);
            }
        }

        private void ExerciseApplication()
        {
            if (_exerciseApplication != null)
                _exerciseApplication();
        }

        public virtual void Initialize()
        {
            lock (_initializeLock)
            {
                if (_initialized)
                    return;

                _initialized = true;

                TestLogger?.WriteLine(RemoteServiceFixtures.RemoteApplication.AppName);

                var numberOfTries = 0;

                try
                {
                    var appIsExercisedNormally = true;

                    do
                    {
                        TestLogger?.WriteLine("Test Home" + RemoteApplication.DestinationNewRelicHomeDirectoryPath);

                        appIsExercisedNormally = true;
                        RemoteApplication.TestLogger = new XUnitTestLogger(TestLogger);

                        RemoteApplication.DeleteWorkingSpace();

                        RemoteApplication.CopyToRemote();

                        SetupConfiguration();

                        var captureStandardOutput = RemoteApplication.CaptureStandardOutputRequired;

                        RemoteApplication.Start(CommandLineArguments, captureStandardOutput);

                        
                        try
                        {
                            ExerciseApplication();
                        }
                        catch (Exception ex)
                        {
                            appIsExercisedNormally = false;
                            TestLogger?.WriteLine("Exception occurred in try number " + (numberOfTries + 1) + " : " + ex.Message);

                        }
                        finally
                        {
                            if (!DelayKill)
                            {
                                ShutdownRemoteApplication();

                                if (captureStandardOutput)
                                {
                                    RemoteApplication.CapturedOutput.WriteProcessOutputToLog("RemoteApplication:");

                                    // Most of our tests run in HostedWebCore, but some don't, e.g. the self-hosted
                                    // WCF tests. For the HWC tests we carefully validate the console output in order
                                    // to detect process-level failures that may cause test flickers. For the self-
                                    // hosted tests, unfortunately, we just punt that.
                                    if (RemoteApplication.ValidateHostedWebCoreOutput)
                                    {
                                        SubprocessLogValidator.ValidateHostedWebCoreConsoleOutput(RemoteApplication.CapturedOutput.StandardOutput, TestLogger);
                                    }
                                    else
                                    {
                                        TestLogger?.WriteLine("Note: child process is not required for log validation because _remoteApplication.ValidateHostedWebCoreOutput = false");
                                    }

                                }
                                else
                                {
                                    TestLogger?.WriteLine("Note: child process is not required for log validation because it is running an application that test runner doesn't redirect its standard output.");
                                }

                                RemoteApplication.WaitForExit();

                                appIsExercisedNormally = RemoteApplication.ExitCode == 0;

                                TestLogger?.WriteLine($"Remote application exited with a {(appIsExercisedNormally ? "success" : "failure")} exit code of {RemoteApplication.ExitCode}.");
                            }
                            else
                            {
                                TestLogger?.WriteLine("Note: Due to DelayKill being used, no process output or agent log validation was performed to verify that the application started and ran successfully.");
                            }

                            if (!appIsExercisedNormally && DelayKill)
                            {
                                RemoteApplication.Kill();

                                if (captureStandardOutput)
                                {
                                    RemoteApplication.CapturedOutput.WriteProcessOutputToLog("[RemoteApplicationFixture]: Initialize");
                                }

                                RemoteApplication.WaitForExit();
                            }

                            Thread.Sleep(1000);

                            numberOfTries++;

                        }
                    } while (!appIsExercisedNormally && numberOfTries < MaxTries);

                    if (!appIsExercisedNormally)
                    {
                        TestLogger?.WriteLine($"Test App wasn't exercised normally after {MaxTries} tries.");
                        throw new Exception($"Test App wasn't exercised normally after {MaxTries} tries.");
                    }
                }
                finally
                {
                    TestLogger?.WriteLine("===== Begin Agent log file =====");
                    try
                    {
                        TestLogger?.WriteLine(AgentLog.GetFullLogAsString());
                    }
                    catch (Exception)
                    {
                        TestLogger?.WriteLine("No log file found.");
                    }
                    TestLogger?.WriteLine("----- End of Agent log file -----");
                }
            }
        }

        public virtual void ShutdownRemoteApplication()
        {
            RemoteApplication.Shutdown();
        }

        private bool AgentDidStartupWithoutLoggedErrors()
        {

            //It is possisble that no log file is generated  and calling AgentLog property throws exception in that case.
            try
            {
                AgentLogFile agentLog = AgentLog;
                return agentLog.TryGetLogLine(AgentLogFile.AgentInvokingMethodErrorLineRegex) == null &&
                    (BypassAgentConnectionErrorLineRegexCheck || agentLog.TryGetLogLine(AgentLogFile.AgentConnectionErrorLineRegex) == null);
            }
            catch (Exception ex)
            {
                TestLogger?.WriteLine("An exception occurred while checking Agent log file: " + ex.Message);
                return false;
            }
        }

        public virtual void Dispose()
        {
            RemoteApplication.Shutdown();
            RemoteApplication.Dispose();
        }

        protected string DownloadStringAndAssertEqual(string address, string expectedResult, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            var webClient = new WebClient();

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    webClient.Headers.Add(header.Key, header.Value);
                }
            }

            var result = webClient.DownloadString(address);

            Assert.NotNull(result);
            if (expectedResult != null)
            {
                Assert.Equal(expectedResult, result);
            }

            return result;
        }

        protected string DownloadStringAndAssertContains(string address, string expectedResult, IEnumerable<KeyValuePair<string, string>> headers)
        {
            using (var httpClient = new HttpClient())
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, address);

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        requestMessage.Headers.Add(header.Key, header.Value);
                    }
                }

                var result = httpClient.SendAsync(requestMessage).Result;
                var body = result.Content.ReadAsStringAsync().Result;

                Assert.NotNull(result);

                if (expectedResult != null)
                {
                    Assert.Contains(expectedResult, body);
                }

                return body;
            }
        }

        protected string DownloadStringAndAssertContains(string address, string expectedResult)
        {
            return DownloadStringAndAssertContains(address, expectedResult, null);
        }

        protected T DownloadJsonAndAssertEqual<T>(string address, T expectedResult)
        {
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<T>(resultJson);

            Assert.NotEqual(default(T), result);

            if (expectedResult != null)
            {
                Assert.Equal(expectedResult, result);
            }

            return result;
        }

        public virtual void WriteProcessOutputToLog()
        {
            RemoteApplication.CapturedOutput.WriteProcessOutputToLog("Remote application:");
        }
    }
}
