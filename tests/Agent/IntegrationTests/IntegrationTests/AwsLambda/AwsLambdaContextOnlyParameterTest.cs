// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.General
{
    public abstract class AwsLambdaContextOnlyParameterTest<T> : NewRelicIntegrationTest<T> where T : LambdaContextOnlyParameterFixtureBase
    {
        private readonly LambdaContextOnlyParameterFixtureBase _fixture;

        protected AwsLambdaContextOnlyParameterTest(T fixture, ITestOutputHelper output)
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
                () => Assert.Equal("OtherTransaction/Lambda/LambdaContextOnly", serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"])
                );
        }
    }

    public class AwsLambdaContextOnlyParameterTestCoreOldest : AwsLambdaContextOnlyParameterTest<LambdaContextOnlyParameterFixtureCoreOldest>
    {
        public AwsLambdaContextOnlyParameterTestCoreOldest(LambdaContextOnlyParameterFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaContextOnlyParameterTestCoreLatest : AwsLambdaContextOnlyParameterTest<LambdaContextOnlyParameterFixtureCoreLatest>
    {
        public AwsLambdaContextOnlyParameterTestCoreLatest(LambdaContextOnlyParameterFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
