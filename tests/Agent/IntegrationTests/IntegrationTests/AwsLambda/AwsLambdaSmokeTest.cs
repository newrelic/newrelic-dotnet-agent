// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.General
{
    [NetCoreTest]
    public abstract class AwsLambdaSmokeTestBase<T> : NewRelicIntegrationTest<T> where T : LambdaSnsEventTriggerFixtureBase
    {
        private readonly LambdaSnsEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaSmokeTestBase(string expectedTransactionName, T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _expectedTransactionName = expectedTransactionName;

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
                () => Assert.Equal(2, serverlessPayload.Version),
                () => Assert.Equal("NR_LAMBDA_MONITORING", serverlessPayload.ServerlessType),
                () => Assert.Equal("dotnet", serverlessPayload.Metadata.AgentLanguage),
                () => AssertStringNotNullOrWhitespace(serverlessPayload.Metadata.AgentVersion, "Metadata Agent Version"),
                () => AssertStringNotNullOrWhitespace(serverlessPayload.Metadata.Arn, "Metadata Arn"),
                () => Assert.Equal("self executing assembly", serverlessPayload.Metadata.ExecutionEnvironment),
                () => Assert.Equal("1.0", serverlessPayload.Metadata.FunctionVersion),
                () => Assert.Equal(2, serverlessPayload.Metadata.MetadataVersion),
                () => Assert.Equal(17, serverlessPayload.Metadata.ProtocolVersion),
                () => Assert.NotNull(serverlessPayload.Telemetry.MetricsPayload),
                () => Assert.NotNull(serverlessPayload.Telemetry.TransactionEventsPayload),
                () => Assert.NotNull(serverlessPayload.Telemetry.SpanEventsPayload),
                () => Assert.Null(serverlessPayload.Telemetry.SqlTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.TransactionTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.ErrorTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.ErrorEventsPayload),
                () => Assert.Equal(_expectedTransactionName, serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"])
                );
        }

        private static void AssertStringNotNullOrWhitespace(string actual, string itemName)
        {
            Assert.False(string.IsNullOrWhiteSpace(actual), $"Expected {itemName} to contain a value but was '{actual}'.");
        }
    }

    public class AwsLambdaSmokeTest : AwsLambdaSmokeTestBase<LambdaSnsEventTriggerFixtureNet6>
    {
        public AwsLambdaSmokeTest(LambdaSnsEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output)
        {
        }
    }

    public class AwsLambdaAsyncSmokeTestNet6 : AwsLambdaSmokeTestBase<AsyncLambdaSnsEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncSmokeTestNet6(AsyncLambdaSnsEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output)
        {
        }
    }

    public class AwsLambdaHandlerOnlySmokeTestNet6 : AwsLambdaSmokeTestBase<LambdaHandlerOnlySnsTriggerFixtureNet6>
    {
        public AwsLambdaHandlerOnlySmokeTestNet6(LambdaHandlerOnlySnsTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output)
        {
        }
    }

    public class AwsLambdaHandlerOnlyAsyncSmokeTestNet6 : AwsLambdaSmokeTestBase<AsyncLambdaHandlerOnlySnsTriggerFixtureNet6>
    {
        public AwsLambdaHandlerOnlyAsyncSmokeTestNet6(AsyncLambdaHandlerOnlySnsTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output)
        {
        }
    }

    public class AwsLambdaSmokeTestNet8 : AwsLambdaSmokeTestBase<LambdaSnsEventTriggerFixtureNet8>
    {
        public AwsLambdaSmokeTestNet8(LambdaSnsEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output)
        {
        }
    }

    public class AwsLambdaAsyncSmokeTestNet8 : AwsLambdaSmokeTestBase<AsyncLambdaSnsEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncSmokeTestNet8(AsyncLambdaSnsEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output)
        {
        }
    }

    public class AwsLambdaHandlerOnlySmokeTestNet8 : AwsLambdaSmokeTestBase<LambdaHandlerOnlySnsTriggerFixtureNet8>
    {
        public AwsLambdaHandlerOnlySmokeTestNet8(LambdaHandlerOnlySnsTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output)
        {
        }
    }

    public class AwsLambdaHandlerOnlyAsyncSmokeTestNet8 : AwsLambdaSmokeTestBase<AsyncLambdaHandlerOnlySnsTriggerFixtureNet8>
    {
        public AwsLambdaHandlerOnlyAsyncSmokeTestNet8(AsyncLambdaHandlerOnlySnsTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output)
        {
        }
    }
}
