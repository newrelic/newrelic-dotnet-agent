// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentHealth
{
    [TestFixture]
    public class AgentTimerServiceTests
    {
        private ConfigurationAutoResponder _configAutoResponder;
        private IAgentHealthReporter _healthReporter;
        private AgentTimerService _timerSvc;
        private int _configVersion;


        [SetUp]
        public void SetUp()
        {
            _configAutoResponder = new ConfigurationAutoResponder(DefaultConfiguration.Instance);

            _healthReporter = Mock.Create<IAgentHealthReporter>();
            _timerSvc = new AgentTimerService(_healthReporter);

            PushConfiguration(GetNewConfiguration());
        }

        [TearDown]
        public void Teardown()
        {
            _configAutoResponder.Dispose();
            _healthReporter = null;
            _configVersion = 0;
            _timerSvc.Dispose();

        }

        private IConfiguration GetNewConfiguration()
        {
            var config = Mock.Create<IConfiguration>();
            _configVersion++;
            var ver = _configVersion;
            Mock.Arrange(() => config.ConfigurationVersion).Returns(ver);
            return config;
        }

        private void PushConfiguration(IConfiguration newConfig)
        {
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(newConfig, ConfigurationUpdateSource.Local));
        }


        [Test]
        public void Configuration_EnabledAndFrequencySize
        ([Values(true, false)]    bool isEnabled,
            [Values(1, 10000, 0, -1)]  int frequency
        )
        {
            var config = GetNewConfiguration();

            Mock.Arrange(() => config.DiagnosticsCaptureAgentTiming).Returns(isEnabled);
            Mock.Arrange(() => config.DiagnosticsCaptureAgentTimingFrequency).Returns(frequency);

            PushConfiguration(config);

            var timer = _timerSvc.StartNew("Test");

            var expectedIsTimed = isEnabled && frequency > 0;

            Assert.That(timer != null, Is.EqualTo(expectedIsTimed), $"IsEnabled={isEnabled}; Frequency={frequency}; should be {expectedIsTimed}.");
        }

        [TestCase(true, 1, 5, 5)]
        [TestCase(true, 2, 5, 3)]
        [TestCase(true, 0, 5, 0)]
        [TestCase(true, -1, 5, 0)]
        [TestCase(false, 1, 5, 0)]
        [TestCase(false, 2, 5, 0)]
        public void Configuration_FrequencyImplementedForEachEventType(bool isEnabled, int frequency, int countEvents, int expectedCount)
        {
            var config = GetNewConfiguration();

            Mock.Arrange(() => config.DiagnosticsCaptureAgentTiming).Returns(isEnabled);
            Mock.Arrange(() => config.DiagnosticsCaptureAgentTimingFrequency).Returns(frequency);

            PushConfiguration(config);

            var actualCountRealTimersA = 0;
            var actualCountRealTimersB = 0;

            for (var i = 0; i < countEvents; i++)
            {
                var timerA = _timerSvc.StartNew("Test", "EventA");
                if (timerA != null)
                {
                    actualCountRealTimersA++;
                }

                var timerB = _timerSvc.StartNew("Test", "EventB");
                if (timerB != null)
                {
                    actualCountRealTimersB++;
                }
            }

            NrAssert.Multiple
            (
                () => Assert.That(actualCountRealTimersA, Is.EqualTo(expectedCount), "Count Timers A"),
                () => Assert.That(actualCountRealTimersA, Is.EqualTo(expectedCount), "Count Timers B")
            );
        }


    }
}
