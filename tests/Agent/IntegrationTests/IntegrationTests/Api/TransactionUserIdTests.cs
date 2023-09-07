// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    public abstract class TransactionUserIdTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly TFixture Fixture;

        public TransactionUserIdTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand("ApiCalls TestSetTransactionUserId CustomUserId");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(Fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetLogLevel("finest");
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void TestTransactionSetUserId()
        {
            var expectedTransactionEventAgentAttributes = new Dictionary<string, string>
            {
                { "enduser.id", "CustomUserId" }
            };
            var transactionEvents = Fixture.AgentLog.GetTransactionEvents();
            var transactionEvent = transactionEvents.FirstOrDefault(e => e.AgentAttributes.ContainsKey("enduser.id"));

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Supportability/ApiInvocation/SetUserId"}
            };

            var actualMetrics = Fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                    () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent)
                );
        }
    }

    [NetFrameworkTest]
    public class TransactionUserIdTestsFW : TransactionUserIdTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public TransactionUserIdTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class TransactionUserIdTestsCore : TransactionUserIdTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public TransactionUserIdTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
