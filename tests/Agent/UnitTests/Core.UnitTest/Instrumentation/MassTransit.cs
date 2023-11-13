// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MT = NewRelic.Providers.Wrapper.MassTransit;
using MTLegacy = NewRelic.Providers.Wrapper.MassTransitLegacy;
using NUnit.Framework;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Instrumentation
{
    [TestFixture]
    public class MassTransitHelperTests
    {

        [Test]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename?temporary=true", "myqueuename", true)]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename?temporary=true", "myqueuename", false)]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename", "myqueuename", true)]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename", "myqueuename", false)]
        [TestCase("rabbitmq://localhost/bogus", "Unknown", true)]
        [TestCase("rabbitmq://localhost/bogus", "Unknown", false)]
        [TestCase(null, "Unknown", true)]
        [TestCase(null, "Unknown", false)]
        public void GetQueueName(Uri uri, string expectedQueueName, bool isLegacy)
        {
            // Act
            var queueName = isLegacy ? MTLegacy.MassTransitHelpers.GetQueueData(uri).QueueName : MT.MassTransitHelpers.GetQueueData(uri).QueueName;

            // Assert
            Assert.AreEqual(expectedQueueName, queueName, "Did not get expected queue name");
        }

        [Test]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=true", MessageBrokerDestinationType.TempQueue, true)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=true", MessageBrokerDestinationType.TempQueue, false)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=false", MessageBrokerDestinationType.Queue, true)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=false", MessageBrokerDestinationType.Queue, false)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename", MessageBrokerDestinationType.Queue, true)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename", MessageBrokerDestinationType.Queue, false)]
        [TestCase(null, MessageBrokerDestinationType.Queue, true)]
        [TestCase(null, MessageBrokerDestinationType.Queue, false)]
        public void GetBrokerDestinationType(Uri uri, MessageBrokerDestinationType expectedDestType, bool isLegacy)
        {
            // Act
            var destType = isLegacy ? MTLegacy.MassTransitHelpers.GetQueueData(uri).DestinationType : MT.MassTransitHelpers.GetQueueData(uri).DestinationType;

            // Assert
            Assert.AreEqual(expectedDestType, destType, "Did not get expected queue type");
        }
    }

}
