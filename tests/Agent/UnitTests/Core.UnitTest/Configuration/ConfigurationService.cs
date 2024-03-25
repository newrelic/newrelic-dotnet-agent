// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration.UnitTest
{
    public class Class_ConfigurationService
    {
        // Responding to ConfigurationDeserializedEvent is an implementation detail. These tests aren't terribly valuable at the moment, but give us at least some light coverage.
        [TestFixture, Category("Configuration")]
        public class Event_ConfigurationDeserialized
        {
            private ConfigurationService _configurationService;

            [SetUp]
            public void SetUp()
            {
                _configurationService = new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(),
                    Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());
            }

            [TearDown]
            public void TearDown()
            {
                _configurationService.Dispose();
            }

            [Test]
            public void publishes_ConfigurationUpdatedEvent()
            {
                var wasCalled = false;
                using (new EventSubscription<ConfigurationUpdatedEvent>(_ => wasCalled = true))
                {
                    EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(new configuration()));
                }

                Assert.That(wasCalled, Is.True);
            }
        }

        // Responding to ServerConfigurationUpdatedEvent is an implementation detail. These tests aren't terribly valuable at the moment, but give us at least some light coverage.
        [TestFixture, Category("Just My Code"), Category("Configuration")]
        public class Event_ConnectedToCollector
        {
            [Test]
            public void publishes_ConfigurationUpdatedEvent()
            {
                var wasCalled = false;
                using (new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>()))
                using (new EventSubscription<ConfigurationUpdatedEvent>(_ => wasCalled = true))
                {
                    EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(new ServerConfiguration
                    {
                        AgentRunId = "123"
                    }));
                }

                Assert.That(wasCalled, Is.True);
            }
        }

        // Responding to AppNameUpdateEvent is an implementation detail. These tests aren't terribly valuable at the moment, but give us at least some light coverage.
        [TestFixture]
        public class Event_AppNameUpdateEvent
        {
            [Test]
            public void publishes_AppNameUpdateEvent()
            {
                var wasCalled = false;
                using (new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>()))
                using (new EventSubscription<ConfigurationUpdatedEvent>(_ => wasCalled = true))
                {
                    EventBus<AppNameUpdateEvent>.Publish(new AppNameUpdateEvent(new[] { "NewAppName" }));
                }

                Assert.That(wasCalled, Is.True);
            }

        }

        // Responding to ErrorGroupCallbackUpdateEvent is an implementation detail. These tests aren't terribly valuable at the moment, but give us at least some light coverage.
        [TestFixture]
        public class Event_ErrorGroupCallbackUpdateEvent
        {
            private ConfigurationService _configurationService;
            private Func<IReadOnlyDictionary<string, object>, string> _callback = dict => "errorGroup";

            [SetUp]
            public void SetUp()
            {
                _configurationService = new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());
            }

            [TearDown]
            public void TearDown()
            {
                _configurationService.Dispose();
            }

            [Test]
            public void publishes_ErrorGroupCallbackUpdateEvent()
            {
                var wasCalled = false;
                using (new EventSubscription<ConfigurationUpdatedEvent>(_ => wasCalled = true))
                {
                    EventBus<ErrorGroupCallbackUpdateEvent>.Publish(new ErrorGroupCallbackUpdateEvent(_callback));
                }

                Assert.That(wasCalled, Is.True);
            }

            [Test]
            public void publishes_ErrorGroupCallbackUpdateEvent_SameCallback_DoesNotUpdateConfig()
            {
                // call this test to setup the initial callback
                publishes_ErrorGroupCallbackUpdateEvent();

                var wasCalled = false;
                using (new EventSubscription<ConfigurationUpdatedEvent>(_ => wasCalled = true))
                {
                    EventBus<ErrorGroupCallbackUpdateEvent>.Publish(new ErrorGroupCallbackUpdateEvent(_callback));
                }

                Assert.That(wasCalled, Is.False);
            }
        }

        [TestFixture, Category("Configuration")]
        public class Request_GetCurrentConfiguration
        {
            private ConfigurationService _configurationService;

            [SetUp]
            public void SetUp()
            {
                _configurationService = new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());
            }

            [TearDown]
            public void TearDown()
            {
                _configurationService.Dispose();
            }

            [Test]
            public void responds_to_request()
            {
                // ACT
                var configuration = RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest());

                // ASSERT
                Assert.That(configuration, Is.Not.Null);
            }

            [Test]
            public void responds_with_latest_configuration()
            {
                // ARRANGE
                EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(new ServerConfiguration
                {
                    AgentRunId = 24,
                    ApdexT = 42
                }));

                // ACT
                var configuration = RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest());

                // ASSERT
                Assert.That(configuration, Is.Not.Null);
                Assert.That(configuration.TransactionTraceApdexT, Is.EqualTo(TimeSpan.FromSeconds(42)));
            }
        }

        [TestFixture, Category("Configuration")]
        public class ConfigurationSeviceUpdatesLogLevel
        {
            private ConfigurationService _configurationService;
            private bool _logLevelChanged;
            private string _newLogLevel;

            [SetUp]
            public void SetUp()
            {
                _configurationService = new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());

                _logLevelChanged = false;
                _newLogLevel = null;
                _configurationService.ChangeLogLevelAction = newLevel =>
                {
                    _logLevelChanged = true;
                    _newLogLevel = newLevel;
                };
            }

            [TearDown]
            public void TearDown()
            {
                _configurationService.Dispose();
            }

            [Test]
            public void UpdatesLogLevel()
            {
                var updatedLocalConfig = new configuration();
                updatedLocalConfig.log.level = "finest";

                EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(updatedLocalConfig));

                Assert.Multiple(() =>
                {
                    Assert.That(_logLevelChanged, Is.EqualTo(true));
                    Assert.That(_newLogLevel, Is.EqualTo("FINEST"));
                });
            }

            [Test]
            public void DoesNotUpdateLogLevelWhenTheLevelIsUnchanged()
            {
                // Set the current log level to finest
                var updatedLocalConfig = new configuration();
                updatedLocalConfig.log.level = "finest";
                EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(updatedLocalConfig));

                // Reset the log level tracking variables
                _newLogLevel = null;
                _logLevelChanged = false;

                // Public a config update with the log level unchanged
                EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(updatedLocalConfig));

                Assert.Multiple(() =>
                {
                    Assert.That(_logLevelChanged, Is.False);
                    Assert.That(_newLogLevel, Is.Null);
                });
            }
        }
    }
}
