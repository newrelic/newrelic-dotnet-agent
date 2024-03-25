// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
#endif
using System.Collections.Generic;
using NewRelic.Agent.Core.Configuration;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Config
{
    [TestFixture]
    public class BootstrapLogConfigurationTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _originalEnvironment = ConfigLoaderHelpers.EnvironmentVariableProxy;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ConfigLoaderHelpers.EnvironmentVariableProxy = _originalEnvironment;
        }

        [SetUp]
        public void SetUp()
        {
            // A new environment mock needs to be created for each test to work around a weird
            // problem where the mock does not behave as expected when all of the tests are run
            // together.
            var environmentMock = Mock.Create<IEnvironment>();
            Mock.Arrange(() => environmentMock.GetEnvironmentVariable(Arg.IsAny<string>())).Returns(MockGetEnvironmentVar);
            ConfigLoaderHelpers.EnvironmentVariableProxy = environmentMock;

            ClearEnvironmentVars();
            _directoryToFullPathMapping.Clear();
            _localConfiguration = new configuration();
            _webConfigValueWithProvenance = null;
            _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
        }

        [Test]
        public void TestDefaultLogBootstrapConfiguration()
        {
            var config = BootstrapConfiguration.GetDefault().LogConfig;

            Assert.Multiple(() =>
            {
                Assert.That(config, Is.Not.Null);
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.IsAuditLogEnabled, Is.False);
                Assert.That(config.MaxLogFiles, Is.EqualTo(4));
                Assert.That(config.Console, Is.False);
                Assert.That(config.LogLevel, Is.EqualTo("INFO"));
                Assert.That(config.MaxLogFileSizeMB, Is.EqualTo(50));
                Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Size));
                Assert.That(config.GetFullLogFileName(), Does.Match(@".+\.log"));
            });
        }

        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("5", true, ExpectedResult = true)]
        [TestCase("5", false, ExpectedResult = false)]
        public bool TestEnabledValue(string environmentValue, bool localConfigValue)
        {
            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", environmentValue);
            _localConfiguration.log.enabled = localConfigValue;

            var config = CreateLogConfig();

            return config.Enabled;
        }

        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool TestAuditLogEnabledValue(bool localConfigValue)
        {
            _localConfiguration.log.auditLog = localConfigValue;

            var config = CreateLogConfig();

            return config.IsAuditLogEnabled;
        }

        [TestCase(null, 1, ExpectedResult = 1)]
        [TestCase(null, 2, ExpectedResult = 2)]
        [TestCase("3", 1, ExpectedResult = 3)]
        [TestCase("2", 5, ExpectedResult = 2)]
        [TestCase("notanumber", 1, ExpectedResult = 1)]
        public int TesMaxLogFilesValue(string environmentValue, int localConfigValue)
        {
            SetEnvironmentVar("NEW_RELIC_LOG_MAX_FILES", environmentValue);
            _localConfiguration.log.maxLogFiles = localConfigValue;

            var config = CreateLogConfig();

            return config.MaxLogFiles;
        }

        [TestCase(null, 100, ExpectedResult = 100)]
        [TestCase(null, 200, ExpectedResult = 200)]
        [TestCase("300", 100, ExpectedResult = 300)]
        [TestCase("200", 500, ExpectedResult = 200)]
        [TestCase("notanumber", 100, ExpectedResult = 100)]
        public int TesMaxLogFileSizeMBValue(string environmentValue, int localConfigValue)
        {
            SetEnvironmentVar("NEW_RELIC_LOG_MAX_FILE_SIZE_MB", environmentValue);
            _localConfiguration.log.maxLogFileSizeMB = localConfigValue;

            var config = CreateLogConfig();

            return config.MaxLogFileSizeMB;
        }

        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("5", true, ExpectedResult = true)]
        [TestCase("5", false, ExpectedResult = false)]
        public bool TestConsoleValue(string environmentValue, bool localConfigValue)
        {
            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", environmentValue);
            _localConfiguration.log.console = localConfigValue;

            var config = CreateLogConfig();

            return config.Console;
        }

        [Test]
        public void TestLogLevelIsOffWhenNotEnabled()
        {
            _localConfiguration.log.enabled = false;

            var config = CreateLogConfig();

            Assert.That(config.LogLevel, Is.EqualTo("off"));
        }

        [TestCase(null, "info", ExpectedResult = "INFO")]
        [TestCase(null, "debug", ExpectedResult = "DEBUG")]
        [TestCase("error", "info", ExpectedResult = "ERROR")]
        [TestCase("warn", "debug", ExpectedResult = "WARN")]
        public string TestLogLevelValue(string environmentValue, string localConfigValue)
        {
            SetEnvironmentVar("NEWRELIC_LOG_LEVEL", environmentValue);
            _localConfiguration.log.level = localConfigValue;

            var config = CreateLogConfig();

            return config.LogLevel;
        }

        [TestCase(null, configurationLogLogRollingStrategy.size, ExpectedResult = LogRollingStrategy.Size)]
        [TestCase(null, configurationLogLogRollingStrategy.day, ExpectedResult = LogRollingStrategy.Day)]
        [TestCase("size", configurationLogLogRollingStrategy.day, ExpectedResult = LogRollingStrategy.Size)]
        [TestCase("day", configurationLogLogRollingStrategy.size, ExpectedResult = LogRollingStrategy.Day)]
        public LogRollingStrategy TestLogRollingStrategy(string environmentValue, configurationLogLogRollingStrategy localConfigValue)
        {
            SetEnvironmentVar("NEW_RELIC_LOG_ROLLING_STRATEGY", environmentValue);
            _localConfiguration.log.logRollingStrategy = localConfigValue;

            var config = CreateLogConfig();

            return config.LogRollingStrategy;
        }

        [Test]
        public void TestLogRollingStrategyThrowsException()
        {
            SetEnvironmentVar("NEW_RELIC_LOG_ROLLING_STRATEGY", "notvalid");
            _localConfiguration.log.logRollingStrategy = configurationLogLogRollingStrategy.size;

            var config = CreateLogConfig();

            Assert.Throws<ConfigurationLoaderException>(() => config.LogRollingStrategy.Equals(LogRollingStrategy.Size));
        }

        [Test]
        public void LogRollingStrategyValueIsCached()
        {
            _localConfiguration.log.logRollingStrategy = configurationLogLogRollingStrategy.day;

            var config = CreateLogConfig();

            var firstValue = config.LogRollingStrategy;
            _localConfiguration.log.logRollingStrategy = configurationLogLogRollingStrategy.size;
            var secondValue = config.LogRollingStrategy;

            Assert.Multiple(() =>
            {
                Assert.That(firstValue, Is.EqualTo(LogRollingStrategy.Day));
                Assert.That(secondValue, Is.EqualTo(LogRollingStrategy.Day));
            });
        }

        [TestCase(null, null, null)]
        [TestCase("env", null, "env")]
        [TestCase(null, "local", "local")]
        [TestCase("env", "local", "env")]
        public void LogFileNameUsesExpectedPath(string environmentValue, string localConfigValue, string expectedPath)
        {
            SetEnvironmentVar("NEWRELIC_LOG_DIRECTORY", environmentValue);
            _localConfiguration.log.directory = localConfigValue;

            var config = CreateLogConfig();

            if (expectedPath == null)
            {
                Assert.That(config.GetFullLogFileName(), Is.Not.Empty);
            }
            else
            {
                Assert.That(config.GetFullLogFileName(), Does.StartWith(expectedPath));
            }
        }

        [Test]
        public void LogFileFromEnvironmentShouldUseFullPathIfExists()
        {
            SetEnvironmentVar("NEWRELIC_LOG_DIRECTORY", "env");
            _localConfiguration.log.directory = "local";
            _directoryToFullPathMapping["env"] = "fullpathenv";

            var config = CreateLogConfig();

            Assert.That(config.GetFullLogFileName(), Does.StartWith("fullpathenv"));
        }

        [TestCase("path", null, "local.log", ExpectedResult = @"path\local.log")]
        [TestCase(@"path\", null, "local.log", ExpectedResult = @"path\local.log")]
        [TestCase("path", "env.log", "local.log", ExpectedResult = @"path\env.log")]
        [TestCase(@"path\", "env.log", "local.log", ExpectedResult = @"path\env.log")]
        [TestCase("path", "env.log", null, ExpectedResult = @"path\env.log")]
        [TestCase(@"path\", "env.log", null, ExpectedResult = @"path\env.log")]
        [TestCase("path", @"<>env.log", null, ExpectedResult = @"path\__env.log")]
        [TestCase("path", null, "<>local.log", ExpectedResult = @"path\__local.log")]
        public string LogFileNameShouldUseConfiguredValue(string path, string environmentValue, string localConfigValue)
        {
            _localConfiguration.log.directory = path;
            SetEnvironmentVar("NEW_RELIC_LOG", environmentValue);
            _localConfiguration.log.fileName = localConfigValue;

            var config = CreateLogConfig();

            return config.GetFullLogFileName();
        }

#if !NETFRAMEWORK
        [TestCase("path", null, "", ExpectedResult = @"path\newrelic_agent_.log")]
        [TestCase("path", "appdomainname", null, ExpectedResult = @"path\newrelic_agent_appdomainname.log")]
        [TestCase("path", null, "processname", ExpectedResult = @"path\newrelic_agent_processname.log")]
        [TestCase("path", null, "processname<>", ExpectedResult = @"path\newrelic_agent_processname__.log")]
        [TestCase("path", "appdomainname<>", null, ExpectedResult = @"path\newrelic_agent_appdomainname__.log")]
        public string LogFileNameUsesFallbackValueNetStandard(string path, string appDomainValue, string processName)
        {
            var originalAppDomainGetter = ConfigurationLoader.GetAppDomainName;
            try
            {
                var mockProcess = Mock.Create<IProcess>();
                Mock.Arrange(() => mockProcess.ProcessName).Returns(processName);
                var processStatic = Mock.Create<IProcessStatic>();
                Mock.Arrange(() => processStatic.GetCurrentProcess()).Returns(mockProcess);

                ConfigurationLoader.GetAppDomainName = () => appDomainValue ?? throw new Exception("Can't get app domain");

                _localConfiguration.log.directory = path;
                _localConfiguration.log.fileName = null;

                var config = CreateLogConfig(processStatic);

                return config.GetFullLogFileName();
            }
            finally
            {
                ConfigurationLoader.GetAppDomainName = originalAppDomainGetter;
            }
        }
#else
        [TestCase("path", null, "", ExpectedResult = @"path\newrelic_agent_.log")]
        [TestCase("path", "appdomainname", null, ExpectedResult = @"path\newrelic_agent_appdomainname.log")]
        [TestCase("path", null, "processname", ExpectedResult = @"path\newrelic_agent_processname.log")]
        [TestCase("path", null, "processname<>", ExpectedResult = @"path\newrelic_agent_processname__.log")]
        [TestCase("path", "appdomainname<>", null, ExpectedResult = @"path\newrelic_agent_appdomainname__.log")]
        public string LogFileNameUsesFallbackValueNetFramework(string path, string appDomainValue, string processName)
        {
            var originalAppDomainGetter = ConfigurationLoader.GetAppDomainAppId;
            try
            {
                var mockProcess = Mock.Create<IProcess>();
                Mock.Arrange(() => mockProcess.ProcessName).Returns(processName);
                var processStatic = Mock.Create<IProcessStatic>();
                Mock.Arrange(() => processStatic.GetCurrentProcess()).Returns(mockProcess);

                ConfigurationLoader.GetAppDomainAppId = () => appDomainValue;

                _localConfiguration.log.directory = path;
                _localConfiguration.log.fileName = null;

                var config = CreateLogConfig(processStatic);

                return config.GetFullLogFileName();
            }
            finally
            {
                ConfigurationLoader.GetAppDomainAppId = originalAppDomainGetter;
            }
        }
#endif

        [TestCase("home", ExpectedResult = @"home\logs\logfilename.log")]
        [TestCase(@"home\", ExpectedResult = @"home\logs\logfilename.log")]
        [TestCase(null, ExpectedResult = @"logfilename.log")]
        public string LogFileFallsBackToHomeDirectory(string homeDirectory)
        {
            var originalHomeDirectoryGetter = ConfigurationLoader.GetNewRelicHome;

            try
            {
                ConfigurationLoader.GetNewRelicHome = () => homeDirectory;
                _localConfiguration.log.fileName = "logfilename.log";

                var config = CreateLogConfig();

                return config.GetFullLogFileName();
            }
            finally
            {
                ConfigurationLoader.GetNewRelicHome = originalHomeDirectoryGetter;
            }
        }

        private ILogConfig CreateLogConfig()
        {
            return CreateLogConfig(new ProcessStatic());
        }

        private ILogConfig CreateLogConfig(IProcessStatic processStatic)
        {
            var bootstrapConfig = new BootstrapConfiguration(_localConfiguration, TestFileName, _ => _webConfigValueWithProvenance, _configurationManagerStatic, processStatic, DirectoryExists, GetFullPath);
            return bootstrapConfig.LogConfig;
        }

        private void SetEnvironmentVar(string name, string value)
        {
            _envVars[name] = value;
        }

        private void ClearEnvironmentVars() => _envVars.Clear();

        private string MockGetEnvironmentVar(string name)
        {
            if (_envVars.TryGetValue(name, out var value)) return value;
            return null;
        }

        private bool DirectoryExists(string path)
        {
            return _directoryToFullPathMapping.ContainsKey(path);
        }

        private string GetFullPath(string path)
        {
            return _directoryToFullPathMapping[path];
        }

        private IEnvironment _originalEnvironment;
        private Dictionary<string, string> _envVars = new Dictionary<string, string>();
        private Dictionary<string, string> _directoryToFullPathMapping = new Dictionary<string, string>();
        private configuration _localConfiguration;
        private const string TestFileName = "testfilename";
        private ValueWithProvenance<string> _webConfigValueWithProvenance;
        private IConfigurationManagerStatic _configurationManagerStatic;
    }
}
