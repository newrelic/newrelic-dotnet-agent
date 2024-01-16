// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using NewRelic.Core.Logging;
using System.IO;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class LoggerBootstrapperTest
    {
        private Func<string, string> _originalGetEnvironmentVar;
        private Dictionary<string, string> _envVars = new Dictionary<string, string>();

        private void SetEnvironmentVar(string name, string value)
        {
            _envVars[name] = value;
        }

        private void ClearEnvironmentVars() =>_envVars.Clear();

        private string MockGetEnvironmentVar(string name)
        {
            if (_envVars.TryGetValue(name, out var value)) return value;
            return null;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _originalGetEnvironmentVar = ConfigurationLoader.GetEnvironmentVar;
            ConfigurationLoader.GetEnvironmentVar = MockGetEnvironmentVar;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ConfigurationLoader.GetEnvironmentVar = _originalGetEnvironmentVar;
        }

        [SetUp]
        public void Setup()
        {
            ClearEnvironmentVars();
        }

        [Test]
        public static void No_log_levels_are_enabled_when_config_log_is_off()
        {
            ILogConfig config = GetLogConfig("off");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            NrAssert.Multiple(
                () => ClassicAssert.IsFalse(Log.IsFinestEnabled),
                () => ClassicAssert.IsFalse(Log.IsDebugEnabled),
                () => ClassicAssert.IsFalse(Log.IsInfoEnabled),
                () => ClassicAssert.IsFalse(Log.IsWarnEnabled),
                () => ClassicAssert.IsFalse(Log.IsErrorEnabled)
            );
        }

        [Test]
        public static void All_log_levels_are_enabled_when_config_log_is_all()
        {
            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(Log.IsFinestEnabled),
                () => ClassicAssert.IsTrue(Log.IsDebugEnabled),
                () => ClassicAssert.IsTrue(Log.IsInfoEnabled),
                () => ClassicAssert.IsTrue(Log.IsWarnEnabled),
                () => ClassicAssert.IsTrue(Log.IsErrorEnabled)
            );
        }

        [Test]
        public static void IsInfoEnabled_is_true_when_config_log_is_info()
        {
            ILogConfig config = GetLogConfig("info");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Assert.That(Log.IsInfoEnabled);
        }

        [Test]
        public static void IsDebugEnabled_is_false_when_config_log_is_info()
        {
            ILogConfig config = GetLogConfig("info");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            ClassicAssert.IsFalse(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsDebugEnabled_is_true_when_config_log_is_debug()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug"); // just to increase code coverage
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.That(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsEnabledFor_finest_is_false_when_config_log_is_debug()
        {
            ILogConfig config = GetLogConfig("debug");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            ClassicAssert.IsFalse(Log.IsFinestEnabled);

        }

        [Test]
        public static void Config_IsAuditEnabled_for_config_is_true_when_auditLog_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithAuditLogEnabled("debug");
            Assert.That(config.IsAuditLogEnabled);
        }

        [Test]
        public static void Config_IsAuditEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            ClassicAssert.IsFalse(config.IsAuditLogEnabled);
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_true_when_console_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug");
            Assert.That(config.Console);
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            ClassicAssert.IsFalse(config.Console);
        }

        [Test]
        public static void Fatal_exception_can_be_recorded()
        {
            ClassicAssert.IsFalse(Log.FileLoggingHasFailed);
            Log.FileLoggingHasFailed = true;
            ClassicAssert.IsTrue(Log.FileLoggingHasFailed);
        }

        [Test]
        [TestCase(null, false, false)]
        [TestCase("0", true, false)]
        [TestCase("0", false, false)]
        [TestCase("false", true, false)]
        [TestCase("false", false, false)]
        [TestCase("1", true, true)]
        [TestCase("1", false, true)]
        [TestCase("true", true, true)]
        [TestCase("true", false, true)]
        [TestCase("not a valid bool", true, true)]
        [TestCase("not a valid bool", false, false)]
        public void test_ways_to_disable_logging(string envVarValue, bool logsEnabledInConfig, bool expectedLogConfig)
        {
            ILogConfig config;

            if (envVarValue != null)
            {
                SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", envVarValue);
            }
            config = LogConfigFixtureWithLogEnabled(logsEnabledInConfig);
            ClassicAssert.AreEqual(config.Enabled, expectedLogConfig);
        }

        [Test]
        [TestCase(null, false, false)]
        [TestCase("0", true, false)]
        [TestCase("0", false, false)]
        [TestCase("false", true, false)]
        [TestCase("false", false, false)]
        [TestCase("1", true, true)]
        [TestCase("1", false, true)]
        [TestCase("true", true, true)]
        [TestCase("true", false, true)]
        [TestCase("not a valid bool", true, true)]
        [TestCase("not a valid bool", false, false)]
        public void test_ways_to_enable_console_logging(string envVarValue, bool consoleLogEnabledInConfig, bool expectedConsoleLogConfig)
        {
            ILogConfig config;

            config = LogConfigFixtureWithConsoleEnabled(true);
            ClassicAssert.IsTrue(config.Console);

            if (envVarValue != null)
            {
                SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", envVarValue);
            }
            config = LogConfigFixtureWithConsoleEnabled(consoleLogEnabledInConfig);
            ClassicAssert.AreEqual(config.Console, expectedConsoleLogConfig);
        }


        private static ILogConfig GetLogConfig(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\"/>" +
                "</configuration>",
                logLevel);

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return configuration.LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithAuditLogEnabled(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\" auditLog=\"true\"/>" +
                "</configuration>",
                logLevel);

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return configuration.LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithConsoleLogEnabled(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\" console=\"true\"/>" +
                "</configuration>",
                logLevel);

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return configuration.LogConfig;
        }
        private static ILogConfig LogConfigFixtureWithLogEnabled(bool enabled)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" console=\"true\" enabled=\"{0}\" />" +
                "</configuration>",
                enabled.ToString().ToLower());

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return configuration.LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithConsoleEnabled(bool enabled)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" console=\"{0}\" />" +
                "</configuration>",
                enabled.ToString().ToLower());

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return configuration.LogConfig;
        }
    }
}
