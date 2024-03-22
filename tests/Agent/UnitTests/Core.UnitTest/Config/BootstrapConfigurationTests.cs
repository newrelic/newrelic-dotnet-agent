// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using NewRelic.Agent.Core.Configuration;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Config
{
    [TestFixture]
    public class BootstrapConfigurationTests
    {
        [SetUp]
        public void SetUp()
        {
            _localConfiguration = new configuration();
            _webConfigValueWithProvenance = null;
            _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
        }

        [Test]
        public void TestDefaultBootstrapConfiguration()
        {
            var config = BootstrapConfiguration.GetDefault();

            Assert.Multiple(() =>
            {
                Assert.That(config.ConfigurationFileName, Is.Null);
                Assert.That(config.AgentEnabled, Is.True);
                Assert.That(config.AgentEnabledAt, Is.EqualTo("Default value"));
                Assert.That(config.DebugStartupDelaySeconds, Is.EqualTo(0));
                Assert.That(config.ServerlessModeEnabled, Is.False);
            });
        }

        [Test]
        public void TestDebugStartupDelaySecondsDefaultValue()
        {
            var config = CreateBootstrapConfiguration();

            Assert.That(config.DebugStartupDelaySeconds, Is.EqualTo(0));
        }

        [Test]
        public void TestDebugStartupDelaySecondsWithValue()
        {
            _localConfiguration.debugStartupDelaySeconds = 30;

            var config = CreateBootstrapConfiguration();

            Assert.That(config.DebugStartupDelaySeconds, Is.EqualTo(30));
        }

        [Test]
        public void TestConfigurationFileName()
        {
            var config = CreateBootstrapConfiguration();

            Assert.That(config.ConfigurationFileName, Is.EqualTo(TestFileName));
        }

        [TestCase(true, true, true, true, TestWebConfigProvenance)]
        [TestCase(true, true, false, true, TestWebConfigProvenance)]
        [TestCase(true, false, true, true, TestWebConfigProvenance)]
        [TestCase(true, false, false, true, TestWebConfigProvenance)]
        [TestCase(true, null, true, true, TestWebConfigProvenance)]
        [TestCase(true, null, false, true, TestWebConfigProvenance)]
        [TestCase(false, true, true, false, TestWebConfigProvenance)]
        [TestCase(false, true, false, false, TestWebConfigProvenance)]
        [TestCase(false, false, true, false, TestWebConfigProvenance)]
        [TestCase(false, false, false, false, TestWebConfigProvenance)]
        [TestCase(false, null, true, false, TestWebConfigProvenance)]
        [TestCase(false, null, false, false, TestWebConfigProvenance)]
        [TestCase(null, true, true, true, TestAppSettingProvenance)]
        [TestCase(null, true, false, true, TestAppSettingProvenance)]
        [TestCase(null, false, true, false, TestAppSettingProvenance)]
        [TestCase(null, false, false, false, TestAppSettingProvenance)]
        [TestCase(null, null, true, true, TestFileName)]
        [TestCase(null, null, false, false, TestFileName)]
        [TestCase(null, true, true, true, TestAppSettingProvenance)]
        [TestCase(null, true, false, true, TestAppSettingProvenance)]
        [TestCase(null, false, true, false, TestAppSettingProvenance)]
        [TestCase(null, false, false, false, TestAppSettingProvenance)]
        [TestCase(null, null, true, true, TestFileName)]
        [TestCase(null, null, false, false, TestFileName)]
        public void AgentEnabledWithProvenanceTests(bool? webConfigValue, bool? appSettingValue, bool localConfigValue, bool expectedValue, string expectedProvenance)
        {
            if (webConfigValue.HasValue)
            {
                _webConfigValueWithProvenance = new ValueWithProvenance<string>(webConfigValue.Value.ToString(), TestWebConfigProvenance);
            }

            if (appSettingValue.HasValue)
            {
                Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Arg.AnyString)).Returns(appSettingValue.ToString());
                Mock.Arrange(() => _configurationManagerStatic.AppSettingsFilePath).Returns(TestAppSettingProvenance);
            }

            _localConfiguration.agentEnabled = localConfigValue;

            var config = CreateBootstrapConfiguration();

            Assert.Multiple(() =>
            {
                Assert.That(config.AgentEnabled, Is.EqualTo(expectedValue));
                Assert.That(config.AgentEnabledAt, Is.EqualTo(expectedProvenance));
            });
        }

        [Test]
        public void CheckingAgentEnabledAtBeforeAgentEnabled()
        {
            _localConfiguration.agentEnabled = true;
            var config = CreateBootstrapConfiguration();

            Assert.That(config.AgentEnabledAt, Is.EqualTo(TestFileName));
        }

        [Test]
        public void AgentEnabledSettingsDoNotChangeOnceSet()
        {
            string appSettingValue = "false";
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Arg.AnyString)).Returns(_ => appSettingValue);
            _localConfiguration.agentEnabled = true;

            var config = CreateBootstrapConfiguration();

            var agentEnabledFromFirstCall = config.AgentEnabled;
            appSettingValue = "true";
            var agentEnabledFromSecondCall = config.AgentEnabled;

            Assert.Multiple(() =>
            {
                Assert.That(agentEnabledFromFirstCall, Is.False);
                Assert.That(agentEnabledFromSecondCall, Is.False);
            });
        }

        [Test]
        public void DoesNotThrowWhenExceptionOccursWhileReadingAppSettings()
        {
            _localConfiguration.agentEnabled = true;
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Arg.AnyString)).Throws(new Exception("Test exception"));

            var config = CreateBootstrapConfiguration();

            Assert.That(config.AgentEnabled, Is.True);
        }

        private BootstrapConfiguration CreateBootstrapConfiguration()
        {
            return new BootstrapConfiguration(_localConfiguration, TestFileName, _ => _webConfigValueWithProvenance, _configurationManagerStatic, new ProcessStatic(), Directory.Exists, Path.GetFullPath);
        }

        private configuration _localConfiguration;
        private const string TestFileName = "testfilename";
        private ValueWithProvenance<string> _webConfigValueWithProvenance;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private const string TestWebConfigProvenance = "web.config";
        private const string TestAppSettingProvenance = "app setting";
    }
}
