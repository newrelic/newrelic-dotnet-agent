// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NewRelic.Agent.Core.Config;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using Telerik.JustMock;
using Newtonsoft.Json.Linq;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using System.Reflection;
using NewRelic.Agent.TestUtilities;

namespace NewRelic.Agent.Core.CrossAgentTests.DataTransport
{
    internal class TestDefaultConfiguration : DefaultConfiguration
    {
        public TestDefaultConfiguration(IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration _securityPoliciesConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic) : base(environment, localConfig, serverConfig, runTimeConfiguration, _securityPoliciesConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic) { }
    }

    [TestFixture]
    public class CollectorHostNameTests
    {
        private IEnvironment _environment;

        private IProcessStatic _processStatic;

        private IHttpRuntimeStatic _httpRuntimeStatic;

        private IConfigurationManagerStatic _configurationManagerStatic;

        private configuration _localConfig;

        private ServerConfiguration _serverConfig;

        private RunTimeConfiguration _runTimeConfig;

        private DefaultConfiguration _defaultConfig;

        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;

        private IDnsStatic _dnsStatic;

        public static List<TestCaseData> CollectorHostnameTestData
        {
            get { return GetCollectorHostnameTestData(); }
        }

        [SetUp]
        public void Setup()
        {
            _environment = Mock.Create<IEnvironment>();
            _processStatic = Mock.Create<IProcessStatic>();
            _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
            _localConfig = new configuration();
            _serverConfig = new ServerConfiguration();
            _runTimeConfig = new RunTimeConfiguration();
            _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _defaultConfig = new TestDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

        }

        [TestCaseSource("CollectorHostnameTestData")]
        public void RunCrossAgentCollectorHostnameTests(string configFileKey, string envKey, string configOverrideHost, string envOverrideHost, string hostname)
        {
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey")).Returns<string>(null);

            if (envKey != null)
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns(envKey);
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns(envKey);
            }
            else
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns<string>(null);
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns<string>(null);
            }

            if (envOverrideHost != null)
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_HOST")).Returns(envOverrideHost);
            }
            else
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_HOST")).Returns<string>(null);
            }

            if (configFileKey != null)
            {
                _localConfig.service.licenseKey = configFileKey;
            }

            if (configOverrideHost != null)
            {
                _localConfig.service.host = configOverrideHost;
            }

            var connectionInfo = new ConnectionInfo(_defaultConfig);
            Assert.AreEqual(hostname, connectionInfo.Host);
        }

        private static List<TestCaseData> GetCollectorHostnameTestData()
        {
            var testDatas = new List<TestCaseData>();
            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "DataTransport", "collector_hostname.json");
            var jsonString = File.ReadAllText(jsonPath);
            var objectArray = JArray.Parse(jsonString);

            foreach (var obj in objectArray)
            {
                var testData = new TestCaseData(new string[] { (string)obj["config_file_key"], (string)obj["env_key"], (string)obj["config_override_host"], (string)obj["env_override_host"], (string)obj["hostname"] });
                testData.SetName("RunCrossAgentCollectorHostnameTests: " + (string)obj["name"]);
                testDatas.Add(testData);
            }

            return testDatas;
        }
    }
}

