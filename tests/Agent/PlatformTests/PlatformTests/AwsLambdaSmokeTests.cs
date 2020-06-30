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
    public class AwsLambdaSmokeTests : IClassFixture<AwsLambdaApplicationFixture>
    {
        private AwsLambdaApplicationFixture _fixture;
        private DateTime _startTime;

        private readonly List<string> _expectedIntrinsicAttributeNames = new List<string>()
        {
            "transactionId",
            "traceId",
            "priority",
            "sampled",
            "guid",
            "timestamp",
            "duration",
            "category",
            "nr.entryPoint"
        };

        private readonly List<string> _expectedAgentAttributeNames = new List<string>()
        {
            "aws.arn",
            "aws.lambda.coldStart",
            "aws.requestId",
            "request.headers.x-forwarded-port",
            "response.status"
        };

        public AwsLambdaSmokeTests(AwsLambdaApplicationFixture fixture, ITestOutputHelper output)
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

            var transactionEvents = JsonConvert.DeserializeObject<List<TransactionEvent>>(spanEventsRawData);

            var spanEvent = spanEvents.First();
            var transactionEvent = transactionEvents.First();

            Assert.NotNull(spanEvent.IntrinsicAttributes);
            Assert.NotNull(spanEvent.UserAttributes);
            Assert.NotNull(spanEvent.AgentAttributes);

            // Span Event Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, spanEvent.IntrinsicAttributes);
            Assert.Equal(spanEvent.IntrinsicAttributes["traceId"], spanEvent.IntrinsicAttributes["transactionId"]);
            Assert.False(spanEvent.IntrinsicAttributes.ContainsKey("parentId")); //There should be no parent as this is the first span.

            // Span Event Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, spanEvent.AgentAttributes);
            Assert.Equal("443", spanEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", spanEvent.AgentAttributes["response.status"]);

            // Transaction Event Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, transactionEvent.IntrinsicAttributes);

            // Transaction Event Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, transactionEvent.AgentAttributes);
            Assert.Equal("443", transactionEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", transactionEvent.AgentAttributes["response.status"]);

            await service.DeleteCloudWatchLogStreamsForLogStreams(_fixture.LogGroupName);
        }

        private void ValidateExpectedAttributesNotNull(List<string> expectedAttributeNames, IDictionary<string, object> attributes)
        {
            foreach (var name in expectedAttributeNames)
            {
                if(!attributes.ContainsKey(name))
                {
                    Assert.False(true, $"attribute {name} does not exist");
                }

                if (attributes[name] == null)
                {
                    Assert.False(true, $"attribute {name} has null value");
                }
            }
        }
    }
}

