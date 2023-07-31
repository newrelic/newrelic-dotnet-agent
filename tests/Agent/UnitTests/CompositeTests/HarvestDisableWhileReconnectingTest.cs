// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using System;
using Telerik.JustMock;

namespace CompositeTests
{
    [TestFixture]
    public class HarvestDisableWhileReconnectingTest
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        //This test makes sure that all aggregators stop their Harvest, by executing _scheduler.StopExecuting(Harvest, TimeSpan.FromSeconds(2)), when agent restart event is fired. This makes sure that no data is sent during configuration update period.
        [Test]
        public void HarvestIsDisabledWhileReconnectingTest()
        {
            var connectionHandler = Mock.Create<IConnectionHandler>();

            _compositeTestAgent.Container.ReplaceInstanceRegistration(connectionHandler);
#if NET
            _compositeTestAgent.Container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
#endif
            _compositeTestAgent.Container.Resolve<IConnectionManager>();

            var numExistingAggregators = 9; //We currently have 9 different aggregators.

            var scheduler = _compositeTestAgent.Container.Resolve<IScheduler>();

            EventBus<RestartAgentEvent>.Publish(new RestartAgentEvent());

            Mock.Assert(() => scheduler.StopExecuting(Arg.IsAny<Action>(), TimeSpan.FromSeconds(2)), Occurs.Exactly(numExistingAggregators));

        }
    }
}
