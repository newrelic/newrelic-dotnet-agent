// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class AttributeInstrumentationTestsFW462 : AttributeInstrumentationTests<ConsoleDynamicMethodFixtureFW462>
    {
        public AttributeInstrumentationTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class AttributeInstrumentationTestsCoreOldest : AttributeInstrumentationTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AttributeInstrumentationTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public abstract class AttributeInstrumentationTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string LibraryClassName = "MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation";

        private readonly string[] TestCommands = new string[]
            {
                // Web transactions (i.e. [Transaction(Web = true)]
                "MakeWebTransaction",
                "MakeWebTransactionWithCustomUri",
                // Other transactions (i.e. [Transaction] or [Transaction(Web = false)])
                "MakeOtherTransaction",
                "MakeOtherTransactionAsync",
                "MakeOtherTransactionThenCallAsyncMethod",
                "MakeOtherTransactionWithCallToNetStandardMethod"
            };


        protected readonly TFixture Fixture;

        public AttributeInstrumentationTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            foreach (var testCommand in TestCommands)
            {
                Fixture.AddCommand($"AttributeInstrumentation {testCommand}");
            }

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = $"WebTransaction", callCount = 2},
                new Assertions.ExpectedMetric {metricName = $"OtherTransaction/all", callCount = 4},

                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWork", callCount = 3},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWorkAsync", callCount = 2},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeMoreWorkAsync", callCount = 2},

                new Assertions.ExpectedMetric {metricName = $"WebTransaction/Custom/{LibraryClassName}/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeWebTransaction", metricScope = $"WebTransaction/Custom/{LibraryClassName}/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWork", metricScope = $"WebTransaction/Custom/{LibraryClassName}/MakeWebTransaction", callCount = 1},

                new Assertions.ExpectedMetric {metricName = $"WebTransaction/Uri/fizz/buzz", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeWebTransactionWithCustomUri", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeWebTransactionWithCustomUri", metricScope = "WebTransaction/Uri/fizz/buzz", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWork", metricScope = "WebTransaction/Uri/fizz/buzz", callCount = 1},

                new Assertions.ExpectedMetric {metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransaction", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWork", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransaction", callCount = 1},

                new Assertions.ExpectedMetric {metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWorkAsync", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeMoreWorkAsync", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},

                new Assertions.ExpectedMetric {metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionAsync", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeWorkAsync", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/DoSomeMoreWorkAsync", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionAsync", callCount = 1},

                new Assertions.ExpectedMetric {metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionWithCallToNetStandardMethod", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = $"DotNet/NetStandardTestLibrary.MyClass/MyMethodToBeInstrumented", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
            };

            var expectedTransactionEventAgentAttributes = new Dictionary<string, string>
            {
                { "request.uri", "/fizz/buzz" }
            };


            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var transactionEvent = Fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes["name"].ToString() == "WebTransaction/Uri/fizz/buzz")
                .FirstOrDefault();


            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent)
            );
        }
    }
}
