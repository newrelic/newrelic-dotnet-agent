// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk
{
    public class AwsSdkMultiServiceTest : NewRelicIntegrationTest<AwsSdkContainerMultiServiceTestFixture>
    {
        private readonly AwsSdkContainerMultiServiceTestFixture _fixture;

        private readonly string _tableName = $"TableName_{Guid.NewGuid()}";
        private readonly string _queueName = $"QueueName_{Guid.NewGuid()}";
        private readonly string _bookName = $"BookName_{Guid.NewGuid()}";

        public AwsSdkMultiServiceTest(AwsSdkContainerMultiServiceTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions(setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.ForceTransactionTraces();
                    configModifier.EnableDistributedTrace();
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(15);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                    configModifier.LogToConsole();

                },
                exerciseApplication: () =>
                {
                    _fixture.Delay(5);

                    _fixture.ExerciseMultiService(_tableName, _queueName, _bookName);

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex,
                        TimeSpan.FromMinutes(2));

                    // shut down the container and wait for the agent log to see it
                    _fixture.ShutdownRemoteApplication();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
        }
    }
}
