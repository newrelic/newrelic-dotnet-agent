// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Lambda;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System;
using NewRelic.Mock.Amazon.Lambda.SQSEvents;

namespace Agent.Extensions.Tests.Lambda;

[TestFixture]
public class LambdaEventHelpersTests
{
    private IAgent _agent;
    private ITransaction _transaction;
    private Dictionary<string, object> _attributes;
    private IDictionary<string, string> _parsedHeaders;
    IDictionary<string, IList<string>> _parsedMultiValueHeaders = new Dictionary<string, IList<string>>();
    private TransportType _transportType;

    private const string NewRelicDistributedTraceKey = "newrelic";
    private const string NewRelicDistributedTracePayload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiYWNjb3VudElkIiwiYXAiOiJhcHBJZCIsInRyIjoiMGFmNzY1MTkxNmNkNDNkZDg0NDhlYjIxMWM4MDMxOWMiLCJwciI6MC42NSwic2EiOnRydWUsInRpIjoxNzEzOTc3NjM3MDcxLCJ0ayI6IjMzIiwidHgiOiJ0cmFuc2FjdGlvbklkIiwiaWQiOiI1NTY5MDY1YTViMTMxM2JkIn19";
    private const string NewRelicDistributedTracePayload2 = "eyAidiI6WzIsNV0sImQiOnsidHkiOiJIVFRQIiwiYWMiOiJhY2NvdW50SWQiLCJhcCI6ImFwcElkIiwidHIiOiJ0cmFjZUlkIiwicHIiOjAuNjUsInNhIjp0cnVlLCJ0aSI6MCwidGsiOiJ0cnVzdEtleSIsInR4IjoidHJhbnNhY3Rpb25JZCIsImlkIjoiZ3VpZCJ9fQ==";

    private const string W3CTraceParentKey = "traceparent";
    private const string W3CTraceStateKey = "tracestate";
    private const string W3CTraceParentPayload = "00-da8bc8cc6d062849b0efcf3c169afb5a-7d3efb1b173fecfa-01";
    private const string W3CTraceStatePayload = "33@nr=0-0-33-2827902-7d3efb1b173fecfa-e8b91a159289ff74-1-1.23456-1518469636035";

    private const string SnsBodyJson = $$"""
        {
            "Type": "Notification",
            "MessageId": "773af62d-cff4-5340-b73c-a88942c8b7b0",
            "TopicArn": "arn:aws:sns:us-west-2:342444490463:CoolSnsTopic",
            "Subject": "MessageSubject",
            "Message": "Hello, world.",
            "Timestamp": "2024-04-25T16:55:24.199Z",
            "SignatureVersion": "1",
            "Signature": "asdflkasdjflkasdjlkfas",
            "SigningCertURL": "https://some.host.com/some/path",
            "UnsubscribeURL": "https://sns.us-west-2.amazonaws.com/?goaway",
            "MessageAttributes": {
                "{{NewRelicDistributedTraceKey}}": {
                    "Type": "String",
                    "Value": "{{NewRelicDistributedTracePayload}}"
                },
                "{{W3CTraceParentKey}}": {
                    "Type": "String",
                    "Value": "{{W3CTraceParentPayload}}"
                },
                "{{W3CTraceStateKey}}": {
                    "Type": "String",
                    "Value": "{{W3CTraceStatePayload}}"
                },
                "ExtraAttribute": {
                    "Type": "String",
                    "Value": "SomethingExtra"
                }
            }
        }
        """;

    [SetUp]
    public void SetUp()
    {
        _attributes = new Dictionary<string, object>();
        _agent = Mock.Create<IAgent>();
        _transaction = Mock.Create<ITransaction>();
        Mock.Arrange(() => _transaction.AddLambdaAttribute(Arg.IsAny<string>(), Arg.IsAny<object>()))
            .DoInstead((string key, object value) => _attributes.Add(key, value));

        Mock.Arrange(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>()))
            .DoInstead((IDictionary<string, string> headers, Func<IDictionary<string, string>, string, IEnumerable<string>> getter, TransportType transportType) => { _parsedHeaders = headers; _transportType = transportType; });
        Mock.Arrange(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>()))
            .DoInstead((IDictionary<string, IList<string>> headers, Func<IDictionary<string, IList<string>>, string, IEnumerable<string>> getter, TransportType transportType) => { _parsedMultiValueHeaders = headers; _transportType = transportType; });

    }

    // APIGatewayProxyRequest
    [Test]
    public void AddEventTypeAttributes_APIGatewayProxyRequest_AddsCorrectAttributes_MultiValueHeaders()
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
        {
            RequestContext = new()
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                ResourceId = "testResourceId",
                ResourcePath = "testResourcePath",
                Stage = "testStage"
            },
            MultiValueHeaders = new()
            {
                { "header1", new [] {"value1", "value1a" } },
                { "header2", new [] {"value2" } },
                { NewRelicDistributedTraceKey, new [] { NewRelicDistributedTracePayload, NewRelicDistributedTracePayload2} }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.accountId"], Is.EqualTo("testAccountId"));
            Assert.That(_attributes["aws.lambda.eventSource.apiId"], Is.EqualTo("testApiId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourceId"], Is.EqualTo("testResourceId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourcePath"], Is.EqualTo("testResourcePath"));
            Assert.That(_attributes["aws.lambda.eventSource.stage"], Is.EqualTo("testStage"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.MultiValueHeaders, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));

            Assert.That(_parsedMultiValueHeaders[NewRelicDistributedTraceKey], Is.EqualTo(new List<string>() { NewRelicDistributedTracePayload, NewRelicDistributedTracePayload2 }));
            Assert.That(_transportType, Is.EqualTo(TransportType.Unknown));
        });
    }

    [Test]
    [TestCase("http", TransportType.HTTP)]
    [TestCase("https", TransportType.HTTPS)]
    [TestCase("gibberish", TransportType.Unknown)]
    public void AddEventTypeAttributes_APIGatewayProxyRequest_SetsDistributedTransportType_FromXForwardedProtoHeader_MultiValueHeaders(string forwardedProto, TransportType expecteTransportType)
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
        {
            RequestContext = new()
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                ResourceId = "testResourceId",
                ResourcePath = "testResourcePath",
                Stage = "testStage"
            },
            MultiValueHeaders = new()
            {
                { "header1", new [] {"value1", "value1a" } },
                { "header2", new [] {"value2" } },
                { NewRelicDistributedTraceKey, new [] { NewRelicDistributedTracePayload, NewRelicDistributedTracePayload2} },
                { "X-Forwarded-Proto", new [] {forwardedProto} }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject);

        // Assert
        Assert.That(_transportType, Is.EqualTo(expecteTransportType));
    }

    [Test]
    public void AddEventTypeAttributes_APIGatewayProxyRequest_AddsCorrectAttributes_SingleValueHeaders()
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
        {
            RequestContext = new()
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                ResourceId = "testResourceId",
                ResourcePath = "testResourcePath",
                Stage = "testStage"
            },
            Headers = new()
            {
                { "header1", "value1" },
                { "header2", "value2" },
                { NewRelicDistributedTraceKey, NewRelicDistributedTracePayload }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.accountId"], Is.EqualTo("testAccountId"));
            Assert.That(_attributes["aws.lambda.eventSource.apiId"], Is.EqualTo("testApiId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourceId"], Is.EqualTo("testResourceId"));
            Assert.That(_attributes["aws.lambda.eventSource.resourcePath"], Is.EqualTo("testResourcePath"));
            Assert.That(_attributes["aws.lambda.eventSource.stage"], Is.EqualTo("testStage"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.Headers, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));

            Assert.That(_transportType, Is.EqualTo(TransportType.Unknown));
        });
    }

    [Test]
    [TestCase("http", TransportType.HTTP)]
    [TestCase("https", TransportType.HTTPS)]
    [TestCase("gibberish", TransportType.Unknown)]
    public void AddEventTypeAttributes_APIGatewayProxyRequest_SetsDistributedTransportType_FromXForwardedProtoHeader_SingleValueHeaders(string forwardedProto, TransportType expecteTransportType)
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
        {
            RequestContext = new()
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                ResourceId = "testResourceId",
                ResourcePath = "testResourcePath",
                Stage = "testStage"
            },
            Headers = new()
            {
                { "header1", "value1" },
                { "header2", "value2" },
                { NewRelicDistributedTraceKey, NewRelicDistributedTracePayload },
                { "X-Forwarded-Proto", forwardedProto }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject);

        // Assert
        Assert.That(_transportType, Is.EqualTo(expecteTransportType));
    }

    // APIGatewayHttpApiV2ProxyRequest
    [Test]
    public void AddEventTypeAttributes_APIGatewayHttpApiV2ProxyRequest_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest
        {
            RequestContext = new()
            {
                AccountId = "testAccountId",
                ApiId = "testApiId",
                RouteId = "testRouteId",
                RouteKey = "testRouteKey",
                Stage = "testStage",
                Http = new()
                {
                    Path = "testPath",
                    Method = "testMethod",
                }
            },
            Headers = new Dictionary<string, string>()
            {
                { "header1", "value1,value1a" },
                { "header2", "value2" },
                { NewRelicDistributedTraceKey, $"{NewRelicDistributedTracePayload}, {NewRelicDistributedTracePayload2}" }
            },
            QueryStringParameters = new Dictionary<string, string>()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, (dynamic)inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.accountId"], Is.EqualTo("testAccountId"));
            Assert.That(_attributes["aws.lambda.eventSource.apiId"], Is.EqualTo("testApiId"));
            Assert.That(_attributes["aws.lambda.eventSource.stage"], Is.EqualTo("testStage"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.Headers, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.RequestContext.Http.Method));
            Mock.Assert(() => _transaction.SetUri(inputObject.RequestContext.Http.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));

            Assert.That(_parsedHeaders[NewRelicDistributedTraceKey], Is.EqualTo($"{NewRelicDistributedTracePayload}, {NewRelicDistributedTracePayload2}"));
            Assert.That(_transportType, Is.EqualTo(TransportType.Unknown));
        });
    }

    // ApplicationLoadBalancerRequest
    [Test]
    public void AddEventTypeAttributes_ApplicationLoadBalancerRequest_AddsCorrectAttributes_SingleValueHeaders()
    {
        // Arrange
        var eventType = AwsLambdaEventType.ApplicationLoadBalancerRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest
        {
            RequestContext = new()
            {
                Elb = new()
                {
                    TargetGroupArn = "testTargetGroupArn"
                }
            },
            Headers = new()
            {
                { "header1", "value1" },
                { "header2", "value2" },
                { NewRelicDistributedTraceKey, NewRelicDistributedTracePayload }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testTargetGroupArn"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.Headers, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));

            Assert.That(_transportType, Is.EqualTo(TransportType.Unknown));
        });
    }
    [Test]
    public void AddEventTypeAttributes_ApplicationLoadBalancerRequest_AddsCorrectAttributes_MultiValueHeaders()
    {
        // Arrange
        var eventType = AwsLambdaEventType.ApplicationLoadBalancerRequest;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest
        {
            RequestContext = new()
            {
                Elb = new()
                {
                    TargetGroupArn = "testTargetGroupArn"
                }
            },
            MultiValueHeaders = new()
            {
                { "header1", new [] {"value1", "value1a" } },
                { "header2", new [] {"value2" } },
                { NewRelicDistributedTraceKey, new [] { NewRelicDistributedTracePayload, NewRelicDistributedTracePayload2} }
            },
            HttpMethod = "GET",
            Path = "/test/path",
            QueryStringParameters = new()
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };

        // Mock the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods
        Mock.Arrange(() => _transaction.SetRequestHeaders(Arg.IsAny<IDictionary<string, IList<string>>>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestMethod(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetUri(Arg.IsAny<string>())).DoNothing();
        Mock.Arrange(() => _transaction.SetRequestParameters(Arg.IsAny<IDictionary<string, string>>())).DoNothing();

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testTargetGroupArn"));

            // Assert that the SetRequestHeaders, SetRequestMethod, SetUri, SetRequestParameters, and AcceptDistributedTraceHeaders methods were called with the correct arguments
            Mock.Assert(() => _transaction.SetRequestHeaders(inputObject.MultiValueHeaders, Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<Func<IDictionary<string, IList<string>>, string, string>>()));
            Mock.Assert(() => _transaction.SetRequestMethod(inputObject.HttpMethod));
            Mock.Assert(() => _transaction.SetUri(inputObject.Path));
            Mock.Assert(() => _transaction.SetRequestParameters(inputObject.QueryStringParameters));

            Assert.That(_parsedMultiValueHeaders[NewRelicDistributedTraceKey], Is.EqualTo(new List<string>() { NewRelicDistributedTracePayload, NewRelicDistributedTracePayload2 }));
            Assert.That(_transportType, Is.EqualTo(TransportType.Unknown));
        });
    }

    // CloudWatchScheduledEvent
    [Test]
    public void AddEventTypeAttributes_CloudWatchScheduledEvent_AddsCorrectAttributes()
    {
        // Arrange
        var testTime = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.CloudWatchScheduledEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent
        {
            Account = "testAccount",
            Id = "testId",
            Region = "testRegion",
            Resources = ["testResource"],
            Time = testTime
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testResource"));
            Assert.That(_attributes["aws.lambda.eventSource.account"], Is.EqualTo("testAccount"));
            Assert.That(_attributes["aws.lambda.eventSource.id"], Is.EqualTo("testId"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.resource"], Is.EqualTo("testResource"));
            Assert.That(_attributes["aws.lambda.eventSource.time"], Is.EqualTo(testTime.ToString()));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_CloudWatchScheduledEvent_HandlesNullOrEmptyRecords(bool isEmpty)
    {
        // Arrange
        var testTime = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.CloudWatchScheduledEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent
        {
            Account = "testAccount",
            Id = "testId",
            Region = "testRegion",
            Time = testTime
        };

        if (isEmpty)
        {
            inputObject.Resources = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.account"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.id"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.region"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.resource"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.time"), Is.False);
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
                new() { EventSourceARN = "testArn", AwsRegion = "testRegion" }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_KinesisStreamingEvent_HandlesNoRecords(bool isEmpty)
    {
        // Arrange
        var eventType = AwsLambdaEventType.KinesisStreamingEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.KinesisEvents.KinesisEvent();
        if (isEmpty)
        {
            inputObject.Records = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.region"), Is.False);
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
                new()
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testDeliveryStreamArn"));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
        });
    }

    public void AddEventTypeAttributes_KinesisFirehoseEvent_HandlesNullRecords()
    {
        // Arrange
        var eventType = AwsLambdaEventType.KinesisFirehoseEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent
        {
            DeliveryStreamArn = "testDeliveryStreamArn",
            Region = "testRegion",
            Records = null
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.region"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);
        });
    }

    // S3Event
    [Test]
    public void AddEventTypeAttributes_S3Event_AddsCorrectAttributes()
    {
        // Arrange
        var eventTime = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.S3Event;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.S3Events.S3Event
        {
            Records = [
                new() {
                    AwsRegion = "testAwsRegion",
                    EventName = "testEventName",
                    EventTime = eventTime,
                    ResponseElements = new (){
                        XAmzId2 = "testXAmzId2",
                    },
                    S3 = new () {
                        Bucket = new () {
                            Name = "testName",
                            Arn = "testArn"
                        },
                        Object = new () {
                            Key = "testKey",
                            Sequencer = "testSequencer",
                            Size = 123
                        },
                    }
                }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
            Assert.That(_attributes["aws.lambda.eventSource.region"], Is.EqualTo("testAwsRegion"));
            Assert.That(_attributes["aws.lambda.eventSource.eventName"], Is.EqualTo("testEventName"));
            Assert.That(_attributes["aws.lambda.eventSource.eventTime"], Is.EqualTo(eventTime.ToString()));
            Assert.That(_attributes["aws.lambda.eventSource.xAmzId2"], Is.EqualTo("testXAmzId2"));
            Assert.That(_attributes["aws.lambda.eventSource.bucketName"], Is.EqualTo("testName"));
            Assert.That(_attributes["aws.lambda.eventSource.objectKey"], Is.EqualTo("testKey"));
            Assert.That(_attributes["aws.lambda.eventSource.objectSequencer"], Is.EqualTo("testSequencer"));
            Assert.That(_attributes["aws.lambda.eventSource.objectSize"], Is.EqualTo(123));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_S3Event_HandlesNoRecords(bool isEmpty)
    {
        // Arrange
        var eventTime = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.S3Event;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.S3Events.S3Event();

        if (isEmpty)
        {
            inputObject.Records = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.region"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.eventName"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.eventTime"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.xAmzId2"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.bucketName"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.objectKey"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.objectSequencer"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.objectSize"), Is.False);
        });
    }

    // SimpleEmailEvent
    [Test]
    public void AddEventTypeAttributes_SimpleEmailEvent_AddsCorrectAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SimpleEmailEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent<NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.MockReceiptAction>
        {
            Records = [
                new() {
                    Ses = new () {
                        Mail = new() {
                            CommonHeaders = new ()
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
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.date"], Is.EqualTo("testDate"));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));
            Assert.That(_attributes["aws.lambda.eventSource.returnPath"], Is.EqualTo("testReturnPath"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_SimpleEmailEvent_HandlesNoRecords(bool isEmpty)
    {
        // Arrange
        var eventType = AwsLambdaEventType.SimpleEmailEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent<NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents.MockReceiptAction>();

        if (isEmpty)
        {
            inputObject.Records = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.date"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.messageId"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.returnPath"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);
        });
    }

    // SNSEvent
    [Test]
    public void AddEventTypeAttributes_SNSEvent_AddsCorrectAttributes()
    {
        // Arrange
        var testTimestamp = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.SNSEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SNSEvents.SNSEvent
        {
            Records =
            [
                new() {
                    EventSubscriptionArn = "testEventSubscriptionArn",
                    Sns = new() {
                        MessageId = "testMessageId",
                        TopicArn = "testTopicArn",
                        Timestamp = testTimestamp,
                        Type = "testType",
                        MessageAttributes = new () { {NewRelicDistributedTraceKey, new  () { Value = NewRelicDistributedTracePayload} } }
                    }
                }
            ]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSubscriptionArn"));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
            Assert.That(_attributes["aws.lambda.eventSource.timestamp"], Is.EqualTo(testTimestamp.ToString()));
            Assert.That(_attributes["aws.lambda.eventSource.topicArn"], Is.EqualTo("testTopicArn"));
            Assert.That(_attributes["aws.lambda.eventSource.type"], Is.EqualTo("testType"));

            Mock.Assert(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), TransportType.Other));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_SNSEvent_HandlesNoRecords(bool isEmpty)
    {
        // Arrange
        var testTimestamp = DateTime.UtcNow;
        var eventType = AwsLambdaEventType.SNSEvent;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.SNSEvents.SNSEvent();
        if (isEmpty)
        {
            inputObject.Records = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.messageId"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.timestamp"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.topicArn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.type"), Is.False);

            Mock.Assert(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), TransportType.Other), Occurs.Never());
        });
    }

    // SQSEvent
    [Test]
    public void AddEventTypeAttributes_SQSEvent_AddsCorrectAttributes_TracingHeaders_FromMessageAttributes()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SQSEvent;
        var inputObject = new SQSEvent
        {
            Records = [
                new()
                {
                    EventSourceArn = "testEventSourceArn",
                    MessageAttributes = new() {
                        { NewRelicDistributedTraceKey, new() { StringValue = NewRelicDistributedTracePayload } },
                        { W3CTraceParentKey, new() { StringValue = W3CTraceParentPayload } },
                        { W3CTraceStateKey, new() { StringValue = W3CTraceStatePayload } }
                    },
                    MessageId = "testMessageId"
                }]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSourceArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));

            Mock.Assert(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), TransportType.Queue));
            Assert.That(_parsedHeaders[NewRelicDistributedTraceKey], Is.EqualTo(NewRelicDistributedTracePayload));
            Assert.That(_parsedHeaders[W3CTraceParentKey], Is.EqualTo(W3CTraceParentPayload));
            Assert.That(_parsedHeaders[W3CTraceStateKey], Is.EqualTo(W3CTraceStatePayload));
        });
    }

    [Test]
    public void AddEventTypeAttributes_SQSEvent_AddsCorrectAttributes_TracingHeaders_FromBody()
    {
        // Arrange
        var eventType = AwsLambdaEventType.SQSEvent;
        var inputObject = new SQSEvent
        {
            Records = [
                new()
                {
                    EventSourceArn = "testEventSourceArn",
                    Body = SnsBodyJson,
                    MessageId = "testMessageId"
                }]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSourceArn"));
            Assert.That(_attributes["aws.lambda.eventSource.length"], Is.EqualTo(1));
            Assert.That(_attributes["aws.lambda.eventSource.messageId"], Is.EqualTo("testMessageId"));

            Mock.Assert(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), TransportType.Queue));
            Assert.That(_parsedHeaders[NewRelicDistributedTraceKey], Is.EqualTo(NewRelicDistributedTracePayload));
            Assert.That(_parsedHeaders[W3CTraceParentKey], Is.EqualTo(W3CTraceParentPayload));
            Assert.That(_parsedHeaders[W3CTraceStateKey], Is.EqualTo(W3CTraceStatePayload));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_SQSEvent_HandlesNoRecords(bool isEmpty)
    {
        // Arrange
        var eventType = AwsLambdaEventType.SQSEvent;
        var inputObject = new SQSEvent();
        if (isEmpty) { }
        {
            inputObject.Records = [];
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.length"), Is.False);

            Mock.Assert(() => _transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IDictionary<string, string>>(), Arg.IsAny<Func<IDictionary<string, string>, string, IEnumerable<string>>>(), TransportType.Queue), Occurs.Never());
        });
    }

    // DynamoStream events
    [Test]
    public void AddEventTypeAttributes_DynamoStream_AddsCorrectAttributes()
    {
        //Arrange
        var eventType = AwsLambdaEventType.DynamoStream;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.DynamoDBEvents.DynamoDBEvent
        {
            Records = [
                new() {
                    EventSourceArn = "testEventSourceArn"
                }]
        };

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes["aws.lambda.eventSource.arn"], Is.EqualTo("testEventSourceArn"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddEventTypeAttributes_DynamoStream_HandlesNoRecords(bool isEmpty)
    {
        //Arrange
        var eventType = AwsLambdaEventType.DynamoStream;
        var inputObject = new NewRelic.Mock.Amazon.Lambda.DynamoDBEvents.DynamoDBEvent();

        if (isEmpty)
        {
            inputObject.Records = [];
        }

        // Act
        LambdaEventHelpers.AddEventTypeAttributes(_agent, _transaction, eventType, inputObject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_attributes.ContainsKey("aws.lambda.eventSource.arn"), Is.False);
        });
    }
}

public enum TracingTestCase
{
    Newrelic,
    W3C,
    Both
}
