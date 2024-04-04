// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda
{
    [NetCoreTest]
    public class AwsLambdaSmokeTest : NewRelicIntegrationTest<RemoteServiceFixtures.LambdaSnsEventTriggerFixture>
    {
        private readonly RemoteServiceFixtures.LambdaSnsEventTriggerFixture _fixture;

        public AwsLambdaSmokeTest(RemoteServiceFixtures.LambdaSnsEventTriggerFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueSnsEvent();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1));
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayload = _fixture.AgentLog.GetServerlessPayloads().Single();

            Assert.Multiple(
                () => Assert.Equal("NR_LAMBDA_MONITORING", serverlessPayload.ServerlessType),
                () => Assert.Equal("dotnet", serverlessPayload.Metadata.AgentLanguage),
                () => Assert.NotNull(serverlessPayload.Telemetry.MetricsPayload),
                () => Assert.NotNull(serverlessPayload.Telemetry.TransactionEventsPayload),
                () => Assert.NotNull(serverlessPayload.Telemetry.SpanEventsPayload),
                () => Assert.Null(serverlessPayload.Telemetry.SqlTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.TransctionTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.ErrorTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.ErrorEventsPayload)
                );
        }
    }
}
