/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.IntegrationTests.Shared.Amazon;
using PlatformTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using System.Linq;

namespace PlatformTests
{
    public class AwsLambdaAwsSdkTests : IClassFixture<AwsLambdaAwsSdkTestFixture>
    {
        private AwsLambdaAwsSdkTestFixture _fixture;
        private DateTime _startTime;

        public AwsLambdaAwsSdkTests(AwsLambdaAwsSdkTestFixture fixture, ITestOutputHelper output)
        {
            _startTime = DateTime.UtcNow.AddMinutes(-2);  // Subract 2 minutes to account for time differences
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Exercise = () =>
            {
                _fixture.ExerciseFunction();
            };

            _fixture.Initialize();
        }

        [Fact]
        public async void Test()
        {
            var service = new CloudWatchLogsService();
            var logs = await service.GetCloudWatchEventMessagesForLogGroup(_fixture.LogGroupName, _startTime);
            var spanEventsRawData = CloudWatchUtilities.GetSpanEventDataFromLog(logs);
            var spanEvents = JsonConvert.DeserializeObject<List<SpanEvent>>(spanEventsRawData);

            var sQSSendMessageSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "SQS" && span.AgentAttributes["aws.operation"].ToString() == "Produce").FirstOrDefault();
            var sQSReceiveMessageSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "SQS" && span.AgentAttributes["aws.operation"].ToString() == "Consume").ToList().FirstOrDefault();

            var dynamoDescribeTableDBSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "DynamoDB" && span.AgentAttributes["aws.operation"].ToString() == "describe_table").FirstOrDefault();
            var dynamoGetItemTableDBSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "DynamoDB" && span.AgentAttributes["aws.operation"].ToString() == "get_item").FirstOrDefault();

            var sNSPublishSpanTopic = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "SNS" && span.AgentAttributes["aws.operation"].ToString() == "Produce" && !span.IntrinsicAttributes["name"].ToString().Contains("PhoneNumber")).FirstOrDefault();
            var sNSPublishSpanPhoneNumber = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "SNS" && span.AgentAttributes["aws.operation"].ToString() == "Produce" && span.IntrinsicAttributes["name"].ToString().Contains("PhoneNumber")).FirstOrDefault();

            var httpOutErrorSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "HttpOut" && span.AgentAttributes.ContainsKey("error")).FirstOrDefault();
            var httpOutSpan = spanEvents.Where(span => span.AgentAttributes.ContainsKey("component") && span.AgentAttributes["component"].ToString() == "HttpOut" && !span.AgentAttributes.ContainsKey("error")).FirstOrDefault();

            Assert.Equal("client", (string)sQSSendMessageSpan.AgentAttributes["span.kind"]);
            Assert.Equal("MessageBroker/SQS/Queue/Produce/Named/DotnetTestSQS", (string)sQSSendMessageSpan.IntrinsicAttributes["name"]);
            Assert.Equal("client", (string)sQSReceiveMessageSpan.AgentAttributes["span.kind"]);
            Assert.Equal("MessageBroker/SQS/Queue/Consume/Named/DotnetTestSQS", (string)sQSReceiveMessageSpan.IntrinsicAttributes["name"]);

            Assert.Equal("client", (string)dynamoDescribeTableDBSpan.AgentAttributes["span.kind"]);
            Assert.Equal("Datastore/statement/DynamoDB/DotNetTest/describe_table", (string)dynamoDescribeTableDBSpan.IntrinsicAttributes["name"]);
            Assert.Equal("client", (string)dynamoGetItemTableDBSpan.AgentAttributes["span.kind"]);
            Assert.Equal("Datastore/statement/DynamoDB/DotNetTest/get_item", (string)dynamoGetItemTableDBSpan.IntrinsicAttributes["name"]);

            Assert.Equal("client", (string)sNSPublishSpanTopic.AgentAttributes["span.kind"]);
            Assert.Equal("MessageBroker/SNS/Topic/Produce/Named/DotNetTestSNSTopic", (string)sNSPublishSpanTopic.IntrinsicAttributes["name"]);

            Assert.Equal("client", (string)sNSPublishSpanPhoneNumber.AgentAttributes["span.kind"]);
            Assert.Equal("MessageBroker/SNS/Topic/Produce/Named/PhoneNumber", (string)sNSPublishSpanPhoneNumber.IntrinsicAttributes["name"]);

            Assert.Equal("client", (string)httpOutSpan.AgentAttributes["span.kind"]);
            Assert.Equal("External/www.newrelic.com/GET", (string)httpOutSpan.IntrinsicAttributes["name"]);

            Assert.Equal("client", (string)httpOutErrorSpan.AgentAttributes["span.kind"]);
            Assert.Equal("External/www.b-a-d.url/GET", (string)httpOutErrorSpan.IntrinsicAttributes["name"]);
            Assert.Equal(true, httpOutErrorSpan.AgentAttributes["error"]);

            await service.DeleteCloudWatchLogStreamsForLogStreams(_fixture.LogGroupName);
        }
    }
}

