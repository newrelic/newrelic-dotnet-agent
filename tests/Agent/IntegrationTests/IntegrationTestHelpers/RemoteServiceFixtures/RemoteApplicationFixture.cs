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
        private HashSet<uint> _errorsToRetryOn = new HashSet<uint>
        {
            0xC000_0005     // System.AccessViolationException. This is a .NET bug that
                            // is supposed to be fixed but we're still seeing
                            // https://github.com/dotnet/runtime/issues/62145
        };

        public Dictionary<string, string> EnvironmentVariables;

        private bool _initialized;

        protected readonly HttpClient _httpClient = new HttpClient();

        public void SetTestClassType(Type testClassType)
        {
            RemoteApplication?.SetTestClassType(testClassType);
        }

        public int? ExitCode => RemoteApplication?.ExitCode;

        private readonly object _initializeLock = new object();

        public readonly RemoteApplication RemoteApplication;

        public string UniqueFolderName { get { return RemoteApplication.UniqueFolderName; } }
        private string AgentLogFileName { get { return CommonUtils.GetAgentLogFileNameFromNewRelicConfig(DestinationNewRelicConfigFilePath); } }


        private AgentLogFile _agentLogFile;
        public bool AgentLogExpected { get; set; } = true;

        public AgentLogFile AgentLog => _agentLogFile ?? (_agentLogFile = new AgentLogFile(DestinationNewRelicLogFileDirectoryPath, TestLogger, AgentLogFileName, Timing.TimeToWaitForLog, AgentLogExpected));


        public ProfilerLogFile ProfilerLog { get { return RemoteApplication.ProfilerLog; } }

        public virtual string DestinationServerName { get { return RemoteApplication.DestinationServerName; } }

        public string DestinationDomainName => System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;

        public string DestinationHostAndDomain
        {
            get
            {
                var domain = DestinationDomainName;
                if (string.IsNullOrEmpty(domain))
                {
                    return DestinationServerName;
                }

                return $"{DestinationServerName}.{domain}";
            }
        }

        public int Port => RemoteApplication.Port;

        public string CommandLineArguments { get; set; }

        public string DestinationNewRelicConfigFilePath { get { return RemoteApplication.DestinationNewRelicConfigFilePath; } }

        public string DestinationNewRelicLogFileDirectoryPath { get { return RemoteApplication.DestinationNewRelicLogFileDirectoryPath; } }

        public string DestinationApplicationDirectoryPath { get { return RemoteApplication.DestinationApplicationDirectoryPath; } }

        public string DestinationNewRelicExtensionsDirectoryPath => RemoteApplication.DestinationNewRelicExtensionsDirectoryPath;

        public ITestOutputHelper TestLogger { get; set; }

        public bool UseLocalConfig
        {
            get { return RemoteApplication.UseLocalConfig; }
            set { RemoteApplication.UseLocalConfig = value; }
        }
        public bool KeepWorkingDirectory
        {
            get { return RemoteApplication.KeepWorkingDirectory; }
            set { RemoteApplication.KeepWorkingDirectory = value; }
        }

        // Tests are only retried if they return a known error not related to the test
        protected virtual int MaxTries => 3;

        public void DisableAsyncLocalCallStack()
        {
            var filesToDelete = new List<string>
            {
                DestinationNewRelicExtensionsDirectoryPath + @"\NewRelic.Providers.Storage.AsyncLocal.dll",
                DestinationNewRelicExtensionsDirectoryPath + @"\NewRelic.Providers.Storage.CallContext.dll"
            };

            foreach (var file in filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
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
            EnvironmentVariables = new Dictionary<string, string>();
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
            _setupConfiguration?.Invoke();
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

        public RemoteApplicationFixture SetAdditionalEnvironmentVariables(IDictionary<string, string> envVars)
        {
            RemoteApplication.SetAdditionalEnvironmentVariables(envVars);
            return this;
        }

        public RemoteApplicationFixture SetAdditionalEnvironmentVariable(string key, string value)
        {
            RemoteApplication.SetAdditionalEnvironmentVariable(key, value);
            return this;
        }

        public void AddErrorToRetryOn(uint error)
        {
            _errorsToRetryOn.Add(error);
        }

        private void ExerciseApplication()
        {
            _exerciseApplication?.Invoke();
        }

        private string FormatExitCode(int? exitCode)
        {
            if (exitCode == null) return "[null]";
            if (Math.Abs(exitCode.Value) < 10) return exitCode.Value.ToString();
            return exitCode.Value.ToString("X8");
        }

        public virtual void Initialize()
        {
            lock (_initializeLock)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;

                TestLogger?.WriteLine(RemoteApplication.AppName);


                var numberOfTries = 0;

                try
                {
                    var retryTest = false;
                    var retryMessage = "";
                    var applicationHadNonZeroExitCode = false;

                    do
                    {
                        TestLogger?.WriteLine("Test Home: " + RemoteApplication.DestinationNewRelicHomeDirectoryPath);

                        // reset these for each loop iteration
                        applicationHadNonZeroExitCode = false;
                        retryTest = false;

                        RemoteApplication.TestLogger = new XUnitTestLogger(TestLogger);

                        RemoteApplication.DeleteWorkingSpace();

                        RemoteApplication.CopyToRemote();

                        SetupConfiguration();

                        var captureStandardOutput = RemoteApplication.CaptureStandardOutput;

                        RemoteApplication.Start(CommandLineArguments, EnvironmentVariables, captureStandardOutput);

                        try
                        {
                            ExerciseApplication();
                        }
                        catch (Exception ex)
                        {
                            TestLogger?.WriteLine("Exception occurred in try number " + (numberOfTries + 1) + " : " + ex.ToString());
                            retryMessage = "Exception thrown.";
                        }
                        finally
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
                                TestLogger?.WriteLine("Note: child process application does not redirect output because _remoteApplication.CaptureStandardOutput = false. HostedWebCore validation cannot take place without the standard output. This is common for non-web and self-hosted applications.");
                            }

                            RemoteApplication.WaitForExit();

                            applicationHadNonZeroExitCode = RemoteApplication.ExitCode != 0;
                            var formattedExitCode = FormatExitCode(RemoteApplication.ExitCode);

                            TestLogger?.WriteLine($"Remote application exited with a {(applicationHadNonZeroExitCode ? "failure" : "success")} exit code of {formattedExitCode}.");

                            if (applicationHadNonZeroExitCode && _errorsToRetryOn.Contains((uint)RemoteApplication.ExitCode.Value))
                            {
                                retryMessage = $"{formattedExitCode} is a known error.";
                                retryTest = true;
                            }

                            if (retryTest && (numberOfTries < MaxTries))
                            {
                                TestLogger?.WriteLine(retryMessage + " Retrying test.");
                                Thread.Sleep(1000);
                                numberOfTries++;
                            }
                        }

                    } while (retryTest && numberOfTries < MaxTries);

                    if (retryTest)
                    {
                        var message = ($"Test failed after {MaxTries} tries.");
                        TestLogger?.WriteLine(message);
                        throw new Exception(message);
                    }
                }

                finally
                {
                    if (AgentLogExpected)
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
        }

        public virtual void ShutdownRemoteApplication()
        {
            RemoteApplication.Shutdown();
        }

        public virtual void Dispose()
        {
            RemoteApplication.Shutdown();
            RemoteApplication.Dispose();
        }

        public virtual void WriteProcessOutputToLog()
        {
            RemoteApplication.CapturedOutput.WriteProcessOutputToLog("Remote application:");
        }

        public virtual string ReturnProcessOutput()
        {
            return RemoteApplication.CapturedOutput.ReturnProcessOutput();
        }

        protected string GetStringAndAssertEqual(string address, string expectedResult, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            string result;

            if (headers == null)
            {
                result = _httpClient.GetStringAsync(address).Result; // throws an AggregateException if there's a problem making the call
            }
            else
            {
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, address))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            requestMessage.Headers.Add(header.Key, header.Value);
                        }
                    }

                    using (var response = _httpClient.SendAsync(requestMessage).Result)
                    {
                        result = response.Content.ReadAsStringAsync().Result;
                    }
                }
            }

            Assert.NotNull(result);
            if (expectedResult != null)
            {
                Assert.Equal(expectedResult, result);
            }

            return result;

        }

        protected string GetStringAndAssertContains(string address, string expectedResult, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, address))
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        requestMessage.Headers.Add(header.Key, header.Value);
                    }
                }

                using (var response = _httpClient.SendAsync(requestMessage).Result)
                {
                    var result = response.Content.ReadAsStringAsync().Result;

                    Assert.NotNull(result);

                    if (expectedResult != null)
                    {
                        Assert.Contains(expectedResult, result);
                    }

                    return result;
                }
            }
        }

        protected T GetJsonAndAssertEqual<T>(string address, T expectedResult, List<KeyValuePair<string, string>> headers = null)
        {
            var result = GetJson<T>(address, headers);

            Assert.NotEqual(default(T), result);

            if (expectedResult != null)
            {
                Assert.Equal(expectedResult, result);
            }

            return result;
        }

        protected string GetStringAndAssertIsNotNull(string address)
        {
            var result = _httpClient.GetStringAsync(address).Result;
            Assert.NotNull(result);
            return result;
        }

        protected void GetStringAndIgnoreResult(string address, List<KeyValuePair<string, string>> headers = null)
        {
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, address))
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        requestMessage.Headers.Add(header.Key, header.Value);
                    }
                }

                _httpClient.SendAsync(requestMessage).Wait();
            }
        }

        protected string GetString(string address) => _httpClient.GetStringAsync(address).Result;

        protected T GetJson<T>(string address, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            if (headers == null)
            {
                var result = _httpClient.GetStringAsync(address).Result;
                var jsonResult = JsonConvert.DeserializeObject<T>(result);
                return jsonResult;
            }
            else
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, address))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }
                    }

                    using (var response = _httpClient.SendAsync(request).Result)
                    {
                        var result = response.Content.ReadAsStringAsync().Result;
                        var jsonResult = JsonConvert.DeserializeObject<T>(result);
                        return jsonResult;
                    }
                }
            }
        }

        protected void GetAndAssertStatusCode(string address, HttpStatusCode expectedStatusCode, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, address))
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    Assert.Equal(expectedStatusCode, response.StatusCode);
                }
            }
        }

        protected void GetAndAssertSuccessStatus(string address, bool expectedSuccessStatus, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, address))
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    Assert.Equal(expectedSuccessStatus, response.IsSuccessStatusCode);
                }
            }
        }
    }
}
