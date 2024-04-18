// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda
{
    [NetCoreTest]
    public abstract class AwsLambdaStreamParameterTest<T> : NewRelicIntegrationTest<T> where T : LambdaStreamParameterFixtureBase
    {
        private readonly LambdaStreamParameterFixtureBase _fixture;

        protected AwsLambdaStreamParameterTest(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueTrigger();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1));
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayload = _fixture.AgentLog.GetServerlessPayloads().Single();
            var customEventPayload = serverlessPayload.Telemetry.CustomEventsPayload;

            Assert.Multiple(
                () => Assert.Equal("$LATEST", serverlessPayload.Metadata.FunctionVersion),
                () => Assert.Equal("OtherTransaction/Lambda/StreamParameter", serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"])
                );
        }
    }

    public class AwsLambdaStreamParameterTestNet6 : AwsLambdaStreamParameterTest<LambdaStreamParameterFixtureNet6>
    {
        public AwsLambdaStreamParameterTestNet6(LambdaStreamParameterFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaStreamParameterTestNet8 : AwsLambdaStreamParameterTest<LambdaStreamParameterFixtureNet8>
    {
        public AwsLambdaStreamParameterTestNet8(LambdaStreamParameterFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
