// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.General
{
    public abstract class AwsLambdaSmokeTestBase<T> : NewRelicIntegrationTest<T> where T : LambdaSnsEventTriggerFixtureBase
    {
        private readonly LambdaSnsEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _awsLambdaApmModeEnabled;

        protected AwsLambdaSmokeTestBase(string expectedTransactionName, T fixture, ITestOutputHelper output, bool awsLambdaApmModeEnabled)
            : base(fixture)
        {
            _expectedTransactionName = expectedTransactionName;
            _awsLambdaApmModeEnabled = awsLambdaApmModeEnabled;

            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AdditionalSetupConfiguration = () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ForceTransactionTraces();
            };

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
                () => Assert.NotNull(serverlessPayload.Telemetry.TransactionTracePayload),
                () => Assert.Null(serverlessPayload.Telemetry.SqlTracePayload),
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

    public class AwsLambdaSmokeTestCoreOldest : AwsLambdaSmokeTestBase<LambdaSnsEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaSmokeTestCoreOldest(LambdaSnsEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output, false)
        {
        }
    }

    public class AwsLambdaAsyncSmokeTestCoreOldest : AwsLambdaSmokeTestBase<AsyncLambdaSnsEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncSmokeTestCoreOldest(AsyncLambdaSnsEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output,false)
        {
        }
    }

    public class AwsLambdaHandlerOnlySmokeTestCoreOldest : AwsLambdaSmokeTestBase<LambdaHandlerOnlySnsTriggerFixtureCoreOldest>
    {
        public AwsLambdaHandlerOnlySmokeTestCoreOldest(LambdaHandlerOnlySnsTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output, false)
        {
        }
    }

    public class AwsLambdaHandlerOnlyAsyncSmokeTestCoreOldest : AwsLambdaSmokeTestBase<AsyncLambdaHandlerOnlySnsTriggerFixtureCoreOldest>
    {
        public AwsLambdaHandlerOnlyAsyncSmokeTestCoreOldest(AsyncLambdaHandlerOnlySnsTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output, false)
        {
        }
    }

    public class AwsLambdaSmokeTestCoreLatest : AwsLambdaSmokeTestBase<LambdaSnsEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaSmokeTestCoreLatest(LambdaSnsEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output,false)
        {
        }
    }

    public class AwsLambdaAsyncSmokeTestCoreLatest : AwsLambdaSmokeTestBase<AsyncLambdaSnsEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncSmokeTestCoreLatest(AsyncLambdaSnsEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output, false)
        {
        }
    }

    public class AwsLambdaHandlerOnlySmokeTestCoreLatest : AwsLambdaSmokeTestBase<LambdaHandlerOnlySnsTriggerFixtureCoreLatest>
    {
        public AwsLambdaHandlerOnlySmokeTestCoreLatest(LambdaHandlerOnlySnsTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandler", fixture, output, false)
        {
        }
    }

    public class AwsLambdaHandlerOnlyAsyncSmokeTestCoreLatest : AwsLambdaSmokeTestBase<AsyncLambdaHandlerOnlySnsTriggerFixtureCoreLatest>
    {
        public AwsLambdaHandlerOnlyAsyncSmokeTestCoreLatest(AsyncLambdaHandlerOnlySnsTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SnsHandlerAsync", fixture, output,false)
        {
        }
    }
    public class AwsLambdaServerlessModeSmokeTestCoreLatest : AwsLambdaSmokeTestBase<LambdaSnsEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaServerlessModeSmokeTestCoreLatest(LambdaSnsEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/SNS SnsHandler", fixture, output, true) // Enable AWS Lambda APM mode
        {
        }
    }
}
