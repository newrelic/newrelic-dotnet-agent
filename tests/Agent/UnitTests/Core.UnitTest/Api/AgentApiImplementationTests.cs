// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
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

        [SetUp]
        public void Setup()
        {
            _configuration = Mock.Create<IConfiguration>();
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _wrapperApi = Mock.Create<IAgent>();

            _errorGroupCallbackUpdateEvents = new List<ErrorGroupCallbackUpdateEvent>();
            EventBus<ErrorGroupCallbackUpdateEvent>.Subscribe(OnRaisedErrorGroupCallbackUpdateEvent);

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
            ClassicAssert.IsEmpty(result);
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
            ClassicAssert.IsNotNull(result);
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
            ClassicAssert.IsEmpty(result);
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
            ClassicAssert.IsNotNull(result);
        }

        [Test]
        public void SetErrorGroupCallbackShouldRaiseEvent()
        {
            Func<IReadOnlyDictionary<string, object>, string> myCallback = ex => "mygroup";

            _agentApi.SetErrorGroupCallback(myCallback);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(1, _errorGroupCallbackUpdateEvents.Count, "Expected only one update event to be triggered."),
                () => ClassicAssert.AreSame(myCallback, _errorGroupCallbackUpdateEvents[0].ErrorGroupCallback, "Expected the callback in the event to match the callback passed to the API.")
                );
        }

        private void OnRaisedErrorGroupCallbackUpdateEvent(ErrorGroupCallbackUpdateEvent updateEvent)
        {
            _errorGroupCallbackUpdateEvents.Add(updateEvent);
        }
    }
}
