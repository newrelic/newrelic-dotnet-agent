// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Lambda;
using NewRelic.Agent.Api;
using System.Collections.Generic;
using System;

namespace Agent.Extensions.Tests.Lambda;

[TestFixture]
public class LambdaEventHelpersTests
{
    private IAgent _agent;
    private ITransaction _transaction;
    private Dictionary<string, string> _attributes;

    [SetUp]
    public void SetUp()
    {
        _agent = Mock.Create<IAgent>();
        _transaction = Mock.Create<ITransaction>();
        _attributes = new Dictionary<string, string>();
    }

    // APIGatewayProxyRequest
    [Test]
    public void AddEventTypeAttributes_APIGatewayProxyRequest_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
        {
            RequestContext = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.RequestContext
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                ResourceId = "testResourceId",
                ResourcePath = "testResourcePath",
                Stage = "testStage"
            },
            Headers = new Dictionary<string, string>
            {
                { "header1", "value1" },
                { "header2", "value2" }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, and SetRequestParameters methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.accountId"], Is.EqualTo("testAccountId"));
            Assert.That(_attributes["aws.lambda.eventSource.apiId"], Is.EqualTo("testApiId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourceId"], Is.EqualTo("testResourceId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourcePath"], Is.EqualTo("testResourcePath"));
            Assert.That(_attributes["aws.lambda.eventSource.stage"], Is.EqualTo("testStage"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, and SetRequestParameters methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.Headers, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));
        });
    }

    // ApplicationLoadBalancerRequest
    [Test]
    public void AddEventTypeAttributes_ApplicationLoadBalancerRequest_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.ApplicationLoadBalancerRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest
        {
            RequestContext = new NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents.RequestContext
            {
                Elb = new NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents.RequestContextElb
                {
                    TargetGroupArn = "testTargetGroupArn"
                }
            },
            Headers = new Dictionary<string, string>
            {
                { "header1", "value1" },
                { "header2", "value2" }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, and SetRequestParameters methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testTargetGroupArn"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, and SetRequestParameters methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.Headers, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));
        });
    }

    // CloudWatchScheduledEvent
    [Test]
    public void AddEventTypeAttributes_CloudWatchScheduledEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.CloudWatchScheduledEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent
        {
            Account = "testAccount",
            Id = "testId",
            Region = "testRegion",
            Resources = ["testResource"],
            Time = "testTime"
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testResource"));
            Assert.That(_attributes["aws.lambda.eventSource.account"], Is.EqualTo("testAccount"));
            Assert.That(_attributes["aws.lambda.eventSource.id"], Is.EqualTo("testId"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.resource"], Is.EqualTo("testResource"));
            Assert.That(_attributes["aws.lambda.eventSource.time"], Is.EqualTo("testTime"));
        });
    }

    // KinesisStreamingEvent
    [Test]
    public void AddEventTypeAttributes_KinesisStreamingEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.KinesisStreamingEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.KinesisEvents.KinesisEvent
        {
            Records =
            [
                new() { EventSourceArn = "testArn", AwsRegion = "testRegion" }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
        });
    }

    // KinesisFirehoseEvent
    [Test]
    public void AddEventTypeAttributes_KinesisFirehoseEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.KinesisFirehoseEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent
        {
            DeliveryStreamArn = "testDeliveryStreamArn",
            Region = "testRegion",
            Records =
            [
                new() { Data = "testData" }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testDeliveryStreamArn"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
        });
    }

    // S3Event
    [Test]
    public void AddEventTypeAttributes_S3Event_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.S3Event;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.S3Events.S3Event
        {
            Records = [
                new() {
                    AwsRegion = "testAwsRegion",
                    EventName = "testEventName",
                    EventTime = "testEventTime",
                    ResponseElements = new NewRelic.Mock.Amazon.Lambda.S3Events.S3ResponseElements {
                        XAmzId2 = "testXAmzId2",
                    },
                    S3 = new NewRelic.Mock.Amazon.Lambda.S3Events.S3Entity {
                        Bucket = new NewRelic.Mock.Amazon.Lambda.S3Events.S3BucketEntity {
                            Name = "testName",
                            Arn = "testArn"
                        },
                        Object = new NewRelic.Mock.Amazon.Lambda.S3Events.S3ObjectEntity {
                            Key = "testKey",
                            Sequencer = "testSequencer",
                            Size = "123"
                        },
                    }
                }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testAwsRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.eventName"], Is.EqualTo("testEventName"));
            Assert.That(_attributes["aws.lambda.eventSource.eventTime"], Is.EqualTo("testEventTime"));
            Assert.That(_attributes["aws.lambda.eventSource.xAmzId2"], Is.EqualTo("testXAmzId2"));
            Assert.That(_attributes["aws.lambda.eventSource.bucketName"], Is.EqualTo("testName"));
            Assert.That(_attributes["aws.lambda.eventSource.objectKey"], Is.EqualTo("testKey"));
            Assert.That(_attributes["aws.lambda.eventSource.objectSequencer"], Is.EqualTo("testSequencer"));
            Assert.That(_attributes["aws.lambda.eventSource.objectSize"], Is.EqualTo("123"));
        });
    }

    // SimpleEmailEvent
    [Test]
    public void AddEventTypeAttributes_SimpleEmailEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SimpleEmailEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent
        {
            Records = [
                new() {
                    Ses = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SesEntity {
                        Mail = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SimpleEmailEntity {
                            CommonHeaders = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SimpleEmailCommonHeadersEntity
                            {
                                Date = "testDate",
                                MessageId = "testMessageId",
                                ReturnPath = "testReturnPath",
                            }
                        }
                    }
                }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.date"], Is.EqualTo("testDate"));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));
            Assert.That(_attributes["aws.lambda.eventSource.returnPath"], Is.EqualTo("testReturnPath"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
        });
    }

    // SNSEvent
    [Test]
    public void AddEventTypeAttributes_SNSEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SNSEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SNSEvents.SNSEvent
        {
            Records =
            [
                new() {
                    EventSubscriptionArn = "testEventSubscriptionArn",
                    Sns = new NewRelic.Mock.Amazon.Lambda.SNSEvents.SNS {
                        MessageId = "testMessageId",
                        TopicArn = "testTopicArn",
                        Timestamp = "testTimestamp",
                        Type = "testType"
                    }
                }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSubscriptionArn"));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
            Assert.That(_attributes["aws.lambda.eventSource.timestamp"], Is.EqualTo("testTimestamp"));
            Assert.That(_attributes["aws.lambda.eventSource.topicArn"], Is.EqualTo("testTopicArn"));
            Assert.That(_attributes["aws.lambda.eventSource.type"], Is.EqualTo("testType"));
        });
    }

    // SQSEvent
    [Test]
    public void AddEventTypeAttributes_SQSEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SQSEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SQSEvents.SQSEvent
        {
            Records = [
                new() {
                    EventSourceArn = "testEventSourceArn",
                }]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject, _attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSourceArn"));
                Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo("1"));
        });
    }
}
