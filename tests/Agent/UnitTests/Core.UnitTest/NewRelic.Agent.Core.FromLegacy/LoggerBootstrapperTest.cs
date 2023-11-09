// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;
using NewRelic.Core.Logging;
using NUnit.Framework;
using System.IO;
using NewRelic.Testing.Assertions;
using System.Collections.Generic;

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
                () => Assert.IsFalse(Log.IsFinestEnabled),
                () => Assert.IsFalse(Log.IsDebugEnabled),
                () => Assert.IsFalse(Log.IsInfoEnabled),
                () => Assert.IsFalse(Log.IsWarnEnabled),
                () => Assert.IsFalse(Log.IsErrorEnabled)
            );
        }

        [Test]
        public static void All_log_levels_are_enabled_when_config_log_is_all()
        {
            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            NrAssert.Multiple(
                () => Assert.IsTrue(Log.IsFinestEnabled),
                () => Assert.IsTrue(Log.IsDebugEnabled),
                () => Assert.IsTrue(Log.IsInfoEnabled),
                () => Assert.IsTrue(Log.IsWarnEnabled),
                () => Assert.IsTrue(Log.IsErrorEnabled)
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
            Assert.IsFalse(Log.IsDebugEnabled);
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
            Assert.IsFalse(Log.IsFinestEnabled);

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
            Assert.IsFalse(config.IsAuditLogEnabled);
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
            Assert.IsFalse(config.Console);
        }

        [Test]
        public static void Fatal_exception_can_be_recorded()
        {
            Assert.IsFalse(Log.FileLoggingHasFailed);
            Log.FileLoggingHasFailed = true;
            Assert.IsTrue(Log.FileLoggingHasFailed);
        }

        [Test]
        public void test_ways_to_disable_logging()
        {
            ILogConfig config;

            config = GetLogConfig("debug");
            Assert.IsTrue(config.Enabled);

            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsFalse(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "0");
            config = LogConfigFixtureWithLogEnabled(true);
            Assert.IsFalse(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "false");
            config = LogConfigFixtureWithLogEnabled(true);
            Assert.IsFalse(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "1");
            config = LogConfigFixtureWithLogEnabled(true);
            Assert.IsTrue(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "true");
            config = LogConfigFixtureWithLogEnabled(true);
            Assert.IsTrue(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "not a valid bool");
            config = LogConfigFixtureWithLogEnabled(true);
            Assert.IsTrue(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "0");
            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsFalse(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "false");
            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsFalse(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "1");
            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsTrue(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "true");
            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsTrue(config.Enabled);

            SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", "not a valid bool");
            config = LogConfigFixtureWithLogEnabled(false);
            Assert.IsFalse(config.Enabled);
        }

        [Test]
        public void test_ways_to_enable_console_logging()
        {
            ILogConfig config;

            config = GetLogConfig("debug");
            Assert.IsFalse(config.Console);

            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "0");
            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsFalse(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "false");
            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsFalse(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "1");
            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "true");
            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "not a valid bool");
            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "0");
            config = LogConfigFixtureWithConsoleEnabled(false);
            Assert.IsFalse(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "false");
            config = LogConfigFixtureWithConsoleEnabled(false);
            Assert.IsFalse(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "1");
            config = LogConfigFixtureWithConsoleEnabled(false);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "true");
            config = LogConfigFixtureWithConsoleEnabled(false);
            Assert.IsTrue(config.Console);

            SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", "not a valid bool");
            config = LogConfigFixtureWithConsoleEnabled(false);
            Assert.IsFalse(config.Console);
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
