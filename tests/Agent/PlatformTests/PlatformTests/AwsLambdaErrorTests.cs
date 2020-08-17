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

namespace PlatformTests
{
    public class AwsLambdaErrorTests : IClassFixture<AwsLambdaErrorTestFixture>
    {
        private AwsLambdaErrorTestFixture _fixture;
        private DateTime _startTime;

        public AwsLambdaErrorTests(AwsLambdaErrorTestFixture fixture, ITestOutputHelper output)
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
            var errorEventRawData = CloudWatchUtilities.GetErrorEventDataFromLog(logs);
            var errorEvents = JsonConvert.DeserializeObject<List<ErrorEventEvents>>(errorEventRawData);
            var errorEvent = errorEvents.First();

            var errorTraceRawData = CloudWatchUtilities.GetErrorTraceDataFromLog(logs);
            var errorTraces = JsonConvert.DeserializeObject<List<ErrorTrace>>(errorTraceRawData);
            var errorTrace = errorTraces.First();

            Assert.Equal("my exception", errorEvent.IntrinsicAttributes["error.message"]);
            Assert.Equal("Exception", errorEvent.IntrinsicAttributes["error.class"]);
            Assert.Equal("Exception", errorEvent.UserAttributes["error.kind"]);

            Assert.Equal("Exception", errorTrace.ExceptionClassName);
            Assert.Equal("my exception", errorTrace.Message);
            Assert.Equal("Other/Function/AwsLambdaErrorTestFunction", errorTrace.Path);

            Assert.NotNull(errorTrace.Attributes);
            Assert.True(errorTrace.Attributes.IntrinsicAttributes.Count > 0);
            Assert.True(errorTrace.Attributes.UserAttributes.Count > 0);
            Assert.NotNull(errorTrace.Attributes.StackTrace);

            await service.DeleteCloudWatchLogStreamsForLogStreams(_fixture.LogGroupName);
        }
    }
}

