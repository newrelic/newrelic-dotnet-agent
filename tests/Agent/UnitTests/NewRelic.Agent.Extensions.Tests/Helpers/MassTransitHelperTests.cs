// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class MassTransitHelperTests
{

    [Test]
    // RabbitMQ (backward compatibility)
    [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename?temporary=true", "myqueuename")]
    [TestCase("rabbitmq://localhost/SomeHostname_MassTransitTest_bus_myqueuename", "myqueuename")]
    [TestCase("rabbitmq://localhost/bogus", "bogus")]
    // RabbitMQ SSL
    [TestCase("rabbitmqs://host/vhost/Hostname_App_bus_myqueue?temporary=true", "myqueue")]
    [TestCase("rabbitmqs://host/my-queue", "my-queue")]
    // Kafka
    [TestCase("kafka://broker:9092/kafka/my-topic", "my-topic")]
    [TestCase("kafka://broker/kafka/orders.events.v1", "orders.events.v1")]
    // Azure Service Bus
    [TestCase("sb://myns.servicebus.windows.net/my-queue", "my-queue")]
    [TestCase("sb://myns.servicebus.windows.net/scope/my-queue", "my-queue")]
    [TestCase("sb://myns.servicebus.windows.net/event-hub/telemetry", "telemetry")]
    // Amazon SQS
    [TestCase("amazonsqs://us-east-1/my-queue", "my-queue")]
    [TestCase("amazonsqs://us-east-1/orders.fifo", "orders.fifo")]
    // ActiveMQ
    [TestCase("activemq://broker/my-queue", "my-queue")]
    [TestCase("amqp://broker/vhost/my-queue", "my-queue")]
    // In-Memory / Loopback
    [TestCase("loopback://localhost/my-queue", "my-queue")]
    // Rider prefix with loopback bus
    [TestCase("loopback://localhost/kafka/my-topic", "my-topic")]
    [TestCase("loopback://localhost/event-hub/my-hub", "my-hub")]
    // Rider prefix with RabbitMQ bus
    [TestCase("rabbitmq://rabbitmq/kafka/my-topic", "my-topic")]
    [TestCase("rabbitmq://rabbitmq/event-hub/my-hub", "my-hub")]
    // Short-form addressing schemes
    [TestCase("queue:my-queue", "my-queue")]
    [TestCase("topic:my-topic", "my-topic")]
    [TestCase("exchange:my-exchange", "my-exchange")]
    // Null
    [TestCase(null, "Unknown")]
    public void GetQueueName(Uri uri, string expectedQueueName)
    {
        // Act
        var queueName = MassTransitHelpers.GetQueueData(uri).QueueName;

        // Assert
        Assert.That(queueName, Is.EqualTo(expectedQueueName), "Did not get expected queue name");
    }

    [Test]
    // RabbitMQ temporary queue (backward compatibility)
    [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=true", MessageBrokerDestinationType.TempQueue)]
    [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename?temporary=false", MessageBrokerDestinationType.Queue)]
    [TestCase("rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_myqueuename", MessageBrokerDestinationType.Queue)]
    // RabbitMQ without underscores + temporary
    [TestCase("rabbitmq://localhost/myqueue?temporary=true", MessageBrokerDestinationType.TempQueue)]
    // RabbitMQ SSL
    [TestCase("rabbitmqs://host/vhost/Hostname_App_bus_myqueue?temporary=true", MessageBrokerDestinationType.TempQueue)]
    [TestCase("rabbitmqs://host/vhost/Hostname_App_bus_myqueue", MessageBrokerDestinationType.Queue)]
    [TestCase("rabbitmqs://host/my-queue", MessageBrokerDestinationType.Queue)]
    // Kafka (always Topic)
    [TestCase("kafka://broker:9092/kafka/my-topic", MessageBrokerDestinationType.Topic)]
    [TestCase("kafka://broker/kafka/orders.events.v1", MessageBrokerDestinationType.Topic)]
    // Azure Event Hubs (always Topic)
    [TestCase("sb://myns.servicebus.windows.net/event-hub/my-hub", MessageBrokerDestinationType.Topic)]
    // Azure Service Bus with type=topic query param
    [TestCase("sb://myns.servicebus.windows.net/my-topic?type=topic", MessageBrokerDestinationType.Topic)]
    [TestCase("sb://myns.servicebus.windows.net/my-queue", MessageBrokerDestinationType.Queue)]
    // Azure Service Bus autodelete → TempQueue
    [TestCase("sb://myns.servicebus.windows.net/my-queue?autodelete=300", MessageBrokerDestinationType.TempQueue)]
    // Amazon SQS
    [TestCase("amazonsqs://us-east-1/my-topic?type=topic", MessageBrokerDestinationType.Topic)]
    [TestCase("amazonsqs://us-east-1/my-queue", MessageBrokerDestinationType.Queue)]
    [TestCase("amazonsqs://us-east-1/my-queue?temporary=true", MessageBrokerDestinationType.TempQueue)]
    // ActiveMQ
    [TestCase("activemq://broker/my-topic?type=topic", MessageBrokerDestinationType.Topic)]
    [TestCase("activemq://broker/my-queue?temporary=true", MessageBrokerDestinationType.TempQueue)]
    [TestCase("amqp://broker/my-queue", MessageBrokerDestinationType.Queue)]
    // In-Memory / Loopback (always Queue)
    [TestCase("loopback://localhost/my-queue", MessageBrokerDestinationType.Queue)]
    // Rider prefix with loopback bus (Topic)
    [TestCase("loopback://localhost/kafka/my-topic", MessageBrokerDestinationType.Topic)]
    [TestCase("loopback://localhost/event-hub/my-hub", MessageBrokerDestinationType.Topic)]
    // Rider prefix with RabbitMQ bus (Topic)
    [TestCase("rabbitmq://rabbitmq/kafka/my-topic", MessageBrokerDestinationType.Topic)]
    [TestCase("rabbitmq://rabbitmq/event-hub/my-hub", MessageBrokerDestinationType.Topic)]
    // Short-form addressing schemes
    [TestCase("topic:my-topic", MessageBrokerDestinationType.Topic)]
    [TestCase("queue:my-queue", MessageBrokerDestinationType.Queue)]
    // Null
    [TestCase(null, MessageBrokerDestinationType.Queue)]
    public void GetBrokerDestinationType(Uri uri, MessageBrokerDestinationType expectedDestType)
    {
        // Act
        var destType = MassTransitHelpers.GetQueueData(uri).DestinationType;

        // Assert
        Assert.That(destType, Is.EqualTo(expectedDestType), "Did not get expected queue type");
    }

    [Test]
    // Primary yields Unknown, fallback has the real destination
    [TestCase(null, "loopback://localhost/kafka/my-topic", "my-topic", MessageBrokerDestinationType.Topic)]
    [TestCase("loopback://localhost/", "loopback://localhost/kafka/my-topic", "my-topic", MessageBrokerDestinationType.Topic)]
    // Primary has valid data — fallback is ignored
    [TestCase("kafka://broker:9092/kafka/my-topic", "loopback://localhost/something-else", "my-topic", MessageBrokerDestinationType.Topic)]
    // Both null
    [TestCase(null, null, "Unknown", MessageBrokerDestinationType.Queue)]
    public void GetQueueData_FallbackAddress(Uri primary, Uri fallback, string expectedQueueName, MessageBrokerDestinationType expectedDestType)
    {
        var data = MassTransitHelpers.GetQueueData(primary, fallback);

        Assert.Multiple(() =>
        {
            Assert.That(data.QueueName, Is.EqualTo(expectedQueueName));
            Assert.That(data.DestinationType, Is.EqualTo(expectedDestType));
        });
    }

    [Test]
    public void GetQueueData_ReturnsDefaults_WhenParsingThrows()
    {
        // A relative Uri passes the null check but throws InvalidOperationException
        // when .Scheme is accessed, exercising the catch block.
        var relativeUri = new Uri("relative-path", UriKind.Relative);

        var data = MassTransitHelpers.GetQueueData(relativeUri);

        Assert.Multiple(() =>
        {
            Assert.That(data.QueueName, Is.EqualTo("Unknown"));
            Assert.That(data.DestinationType, Is.EqualTo(MessageBrokerDestinationType.Queue));
        });
    }
}
