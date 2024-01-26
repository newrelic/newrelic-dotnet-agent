// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class ConfigurationBasedServiceTests
    {
        private class TestConfigurationBasedService : ConfigurationBasedService
        {
            public uint ConfigUpdateCount { get; private set; }
            public IConfiguration Configuration { get { return _configuration; } }

            protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
            {
                ConfigUpdateCount++;
            }
        }

        [Test]
        public void notices_configuration_changes()
        {
            var testService = new TestConfigurationBasedService();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);

            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            Assert.Multiple(() =>
            {
                Assert.That(testService.ConfigUpdateCount, Is.EqualTo(1));
                Assert.That(testService.Configuration.ConfigurationVersion, Is.EqualTo(2));
            });
        }

        [Test]
        public void notices_multiple_configuration_changes()
        {
            var testService = new TestConfigurationBasedService();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(3);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            Assert.Multiple(() =>
            {
                Assert.That(testService.ConfigUpdateCount, Is.EqualTo(2));
                Assert.That(testService.Configuration.ConfigurationVersion, Is.EqualTo(3));
            });
        }

        [Test]
        public void old_config_updates_are_ignored()
        {
            var testService = new TestConfigurationBasedService();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(1);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            Assert.Multiple(() =>
            {
                Assert.That(testService.ConfigUpdateCount, Is.EqualTo(1));
                Assert.That(testService.Configuration.ConfigurationVersion, Is.EqualTo(2));
            });
        }
    }
}
