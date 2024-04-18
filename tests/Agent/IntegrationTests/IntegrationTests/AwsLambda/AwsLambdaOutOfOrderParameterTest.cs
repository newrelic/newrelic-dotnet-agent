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
    public abstract class AwsLambdaOutOfOrderParameterTest<T> : NewRelicIntegrationTest<T> where T : LambdaOutOfOrderParameterFixtureBase
    {
        private readonly LambdaOutOfOrderParameterFixtureBase _fixture;

        protected AwsLambdaOutOfOrderParameterTest(T fixture, ITestOutputHelper output)
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
                () => Assert.Equal("OtherTransaction/Lambda/OutOfOrderParameters", serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"])
                );
        }
    }

    public class AwsLambdaOutOfOrderParameterTestNet6 : AwsLambdaOutOfOrderParameterTest<LambdaOutOfOrderParameterFixtureNet6>
    {
        public AwsLambdaOutOfOrderParameterTestNet6(LambdaOutOfOrderParameterFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaOutOfOrderParameterTestNet8 : AwsLambdaOutOfOrderParameterTest<LambdaOutOfOrderParameterFixtureNet8>
    {
        public AwsLambdaOutOfOrderParameterTestNet8(LambdaOutOfOrderParameterFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
