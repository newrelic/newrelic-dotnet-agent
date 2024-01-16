// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture]
    public class ConfigurationSubscriberTests
    {
        [Test]
        public void notices_configuration_changes()
        {
            var subscriber = new ConfigurationSubscriber();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);

            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            ClassicAssert.AreEqual(2, subscriber.Configuration.ConfigurationVersion);
        }

        [Test]
        public void notices_multiple_configuration_changes()
        {
            var subscriber = new ConfigurationSubscriber();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(3);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            ClassicAssert.AreEqual(3, subscriber.Configuration.ConfigurationVersion);
        }

        [Test]
        public void old_config_updates_are_ignored()
        {
            var subscriber = new ConfigurationSubscriber();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(2);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(1);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));

            ClassicAssert.AreEqual(2, subscriber.Configuration.ConfigurationVersion);
        }
    }
}
