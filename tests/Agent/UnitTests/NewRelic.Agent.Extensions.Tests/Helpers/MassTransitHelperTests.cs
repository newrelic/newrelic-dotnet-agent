// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Helpers;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    public class MassTransitHelperTests
    {

        [Test]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename?temporary=true", "myqueuename")]
        [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename", "myqueuename")]
        [TestCase("rabbitmq://localhost/bogus", "Unknown")]
        [TestCase(null, "Unknown")]
        public void GetQueueName(Uri uri, string expectedQueueName)
        {
            // Act
            var queueName = MassTransitHelpers.GetQueueData(uri).QueueName;

            // Assert
            Assert.AreEqual(expectedQueueName, queueName, "Did not get expected queue name");
        }

        [Test]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=true", MessageBrokerDestinationType.TempQueue)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=false", MessageBrokerDestinationType.Queue)]
        [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename", MessageBrokerDestinationType.Queue)]
        [TestCase(null, MessageBrokerDestinationType.Queue)]
        public void GetBrokerDestinationType(Uri uri, MessageBrokerDestinationType expectedDestType)
        {
            // Act
            var destType = MassTransitHelpers.GetQueueData(uri).DestinationType;

            // Assert
            Assert.AreEqual(expectedDestType, destType, "Did not get expected queue type");
        }
    }

}
