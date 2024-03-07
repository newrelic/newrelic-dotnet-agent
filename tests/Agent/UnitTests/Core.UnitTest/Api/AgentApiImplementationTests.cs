// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Api
{
    [TestFixture]
    public class AgentApiImplementationTests
    {
        private IConfiguration _configuration;
        private IAgent _wrapperApi;
        private IAgentApi _agentApi;
        private List<ErrorGroupCallbackUpdateEvent> _errorGroupCallbackUpdateEvents;
        private List<LlmTokenCountingCallbackUpdateEvent> _llmTokenCountingCallbackUpdateEvents;

        [SetUp]
        public void Setup()
        {
            _configuration = Mock.Create<IConfiguration>();
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _wrapperApi = Mock.Create<IAgent>();

            _errorGroupCallbackUpdateEvents = new List<ErrorGroupCallbackUpdateEvent>();
            EventBus<ErrorGroupCallbackUpdateEvent>.Subscribe(OnRaisedErrorGroupCallbackUpdateEvent);

            _llmTokenCountingCallbackUpdateEvents = new List<LlmTokenCountingCallbackUpdateEvent>();
            EventBus<LlmTokenCountingCallbackUpdateEvent>.Subscribe(updateEvent => _llmTokenCountingCallbackUpdateEvents.Add(updateEvent));

            _agentApi = new AgentApiImplementation(null, null, null, null, null, null, null, configurationService, _wrapperApi, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<ErrorGroupCallbackUpdateEvent>.Unsubscribe(OnRaisedErrorGroupCallbackUpdateEvent);
        }


        [Test]
        public void GetRequestMetadataShouldBeEmptylWhenDistributedTracingEnabled()
        {
            //Arrange
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

            var transaction = Mock.Create<ITransaction>();

            Mock.Arrange(() => _wrapperApi.CurrentTransaction).Returns(transaction);
            Mock.Arrange(() => transaction.GetRequestMetadata()).Returns(new Dictionary<string, string> { { "X-NewRelic-ID", "Test" } });

            //Act
            var result = _agentApi.GetRequestMetadata();

            //Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetRequestMetadataShouldNotBeNullWhenDistributedTracingDisabled()
        {
            //Arrange
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(false);

            var transaction = Mock.Create<ITransaction>();

            Mock.Arrange(() => _wrapperApi.CurrentTransaction).Returns(transaction);
            Mock.Arrange(() => transaction.GetRequestMetadata()).Returns(new Dictionary<string, string> { { "X-NewRelic-ID", "Test" } });

            //Act
            var result = _agentApi.GetRequestMetadata();

            //Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void GetResponseMetadataShouldBeNullWhenDistributedTracingEnabled()
        {
            //Arrange
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

            var transaction = Mock.Create<ITransaction>();

            Mock.Arrange(() => _wrapperApi.CurrentTransaction).Returns(transaction);
            Mock.Arrange(() => transaction.GetResponseMetadata()).Returns(new Dictionary<string, string> { { "X-NewRelic-App-Data", "Test" } });

            //Act
            var result = _agentApi.GetResponseMetadata();

            //Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetResponseMetadataShouldNotBeNullWhenDistributedTracingDisabled()
        {
            //Arrange
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(false);

            var transaction = Mock.Create<ITransaction>();

            Mock.Arrange(() => _wrapperApi.CurrentTransaction).Returns(transaction);
            Mock.Arrange(() => transaction.GetResponseMetadata()).Returns(new Dictionary<string, string> { { "X-NewRelic-App-Data", "Test" } });

            //Act
            var result = _agentApi.GetResponseMetadata();

            //Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void SetErrorGroupCallbackShouldRaiseEvent()
        {
            Func<IReadOnlyDictionary<string, object>, string> myCallback = ex => "mygroup";

            _agentApi.SetErrorGroupCallback(myCallback);

            NrAssert.Multiple(
                () => Assert.That(_errorGroupCallbackUpdateEvents, Has.Count.EqualTo(1), "Expected only one update event to be triggered."),
                () => Assert.That(_errorGroupCallbackUpdateEvents[0].ErrorGroupCallback, Is.SameAs(myCallback), "Expected the callback in the event to match the callback passed to the API.")
                );
        }

        [Test]
        public void SetLlmTokenCountingCallbackShouldRaiseEvent()
        {
            Func<string, string, int> myCallback = (_, _) => 42;

            _agentApi.SetLlmTokenCountingCallback(myCallback);

            NrAssert.Multiple(
                () => Assert.That(_llmTokenCountingCallbackUpdateEvents, Has.Count.EqualTo(1), "Expected only one update event to be triggered."),
                 () => Assert.That(_llmTokenCountingCallbackUpdateEvents[0].LlmTokenCountingCallback, Is.SameAs(myCallback), "Expected the callback in the event to match the callback passed to the API.")
            );
        }

        private void OnRaisedErrorGroupCallbackUpdateEvent(ErrorGroupCallbackUpdateEvent updateEvent)
        {
            _errorGroupCallbackUpdateEvents.Add(updateEvent);
        }
    }
}
