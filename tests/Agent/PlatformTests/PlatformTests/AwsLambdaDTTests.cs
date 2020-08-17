// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Shared.Amazon;
using PlatformTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PlatformTests
{
    public class AwsLambdaDTTests : IClassFixture<AwsLambdaDTTestFixture>
    {
        private AwsLambdaDTTestFixture _fixture;
        private CloudWatchLogsService _cloudWatchLogsService = new CloudWatchLogsService();

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
            "aws.requestId",
            "request.headers.x-forwarded-port",
            "response.status"
        };

        public AwsLambdaDTTests(AwsLambdaDTTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Exercise = () =>
            {
                _fixture.ExerciseChainTest();
            };

            _fixture.Initialize();
        }

        [Fact]
        public async void ChainTest()
        {
            var calleeApplicationSpanEvents = await GetSpanEvents(_fixture.CalleeApplication.LogGroupName, _fixture.CallerApplication.StartTime.AddMinutes(-2));
            var callerApplicationSpanEvents = await GetSpanEvents(_fixture.CallerApplication.LogGroupName, _fixture.CallerApplication.StartTime.AddMinutes(-2));

            var calleeApplicationTransactionEvents = await GetTransactionEvents(_fixture.CalleeApplication.LogGroupName, _fixture.CallerApplication.StartTime.AddMinutes(-2));
            var callerApplicationTransactionEvents = await GetTransactionEvents(_fixture.CallerApplication.LogGroupName, _fixture.CallerApplication.StartTime.AddMinutes(-2));

            var calleeSpanEvent = calleeApplicationSpanEvents.First();
            var calleeTransactionEvent = calleeApplicationTransactionEvents.First();

            var callerSpanEvent = callerApplicationSpanEvents.First();
            var callerTransactionEvent = callerApplicationTransactionEvents.First();

            Assert.NotNull(calleeSpanEvent.IntrinsicAttributes);
            Assert.NotNull(calleeSpanEvent.UserAttributes);
            Assert.NotNull(calleeSpanEvent.AgentAttributes);

            // Span Event A Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, calleeSpanEvent.IntrinsicAttributes);
            Assert.Equal(callerSpanEvent.IntrinsicAttributes["transactionId"], calleeSpanEvent.IntrinsicAttributes["traceId"]);
            Assert.True(calleeSpanEvent.IntrinsicAttributes.ContainsKey("parentId"));

            // Span Event A Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, calleeSpanEvent.AgentAttributes);
            Assert.Equal("443", calleeSpanEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", calleeSpanEvent.AgentAttributes["response.status"]);

            // Transaction Event A Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, calleeTransactionEvent.IntrinsicAttributes);

            // Transaction Event A Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, calleeTransactionEvent.AgentAttributes);
            Assert.Equal("443", calleeTransactionEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", calleeTransactionEvent.AgentAttributes["response.status"]);

            Assert.NotNull(callerSpanEvent.IntrinsicAttributes);
            Assert.NotNull(callerSpanEvent.UserAttributes);
            Assert.NotNull(callerSpanEvent.AgentAttributes);

            // Span Event B Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, callerSpanEvent.IntrinsicAttributes);
            Assert.Equal(callerSpanEvent.IntrinsicAttributes["traceId"], callerSpanEvent.IntrinsicAttributes["transactionId"]);
            Assert.False(callerSpanEvent.IntrinsicAttributes.ContainsKey("parentId")); //There should be no parent as this is the first span.

            // Span Event B Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, callerSpanEvent.AgentAttributes);
            Assert.Equal("443", callerSpanEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", callerSpanEvent.AgentAttributes["response.status"]);

            // Transaction Event B Intrinsic Attributes
            ValidateExpectedAttributesNotNull(_expectedIntrinsicAttributeNames, callerTransactionEvent.IntrinsicAttributes);

            // Transaction Event B Agent Attributes
            ValidateExpectedAttributesNotNull(_expectedAgentAttributeNames, callerTransactionEvent.AgentAttributes);
            Assert.Equal("443", callerTransactionEvent.AgentAttributes["request.headers.x-forwarded-port"]);
            Assert.Equal("200", callerTransactionEvent.AgentAttributes["response.status"]);

            await _cloudWatchLogsService.DeleteCloudWatchLogStreamsForLogStreams(_fixture.CalleeApplication.LogGroupName);
            await _cloudWatchLogsService.DeleteCloudWatchLogStreamsForLogStreams(_fixture.CallerApplication.LogGroupName);
        }


        private async Task<List<SpanEvent>> GetSpanEvents(string logGroupName, DateTime startTime)
        {
            var applicationLogs = await _cloudWatchLogsService.GetCloudWatchEventMessagesForLogGroup(logGroupName, startTime);
            var applicationSpanEventsRawData = CloudWatchUtilities.GetSpanEventDataFromLog(applicationLogs);
            return JsonConvert.DeserializeObject<List<SpanEvent>>(applicationSpanEventsRawData);
        }

        private async Task<List<TransactionEvent>> GetTransactionEvents(string logGroupName, DateTime startTime)
        {
            var applicationLogs = await _cloudWatchLogsService.GetCloudWatchEventMessagesForLogGroup(logGroupName, startTime);
            var transactionEventsRawData = CloudWatchUtilities.GetTransactionEventDataFromLog(applicationLogs);
            return JsonConvert.DeserializeObject<List<TransactionEvent>>(transactionEventsRawData);
        }

        private void ValidateExpectedAttributesNotNull(List<string> expectedAttributeNames, IDictionary<string, object> attributes)
        {
            foreach (var name in expectedAttributeNames)
            {
                Assert.NotNull(attributes[name]);
            }
        }
    }
}

