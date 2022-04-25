// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class GloballyForceNewTransactionEnabledTestsFW462 : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureFW462>
    {
        public GloballyForceNewTransactionEnabledTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(true, fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class GloballyForceNewTransactionEnabledTestsCore31 : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureCore31>
    {
        public GloballyForceNewTransactionEnabledTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(true, fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class GloballyForceNewTransactionDisabledTestsFW462 : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureFW462>
    {
        public GloballyForceNewTransactionDisabledTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(false, fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class GloballyForceNewTransactionDisabledTestsCore31 : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureCore31>
    {
        public GloballyForceNewTransactionDisabledTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(false, fixture, output)
        {
        }
    }

    public abstract class GloballyForceNewTransactionTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string LibraryClassName = "MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation";

        protected readonly TFixture Fixture;

        private readonly bool ForceNewTransaction;

        public GloballyForceNewTransactionTests(bool forceNewTransaction, TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            ForceNewTransaction = forceNewTransaction;

            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.EnvironmentVariables.Add("NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD", ForceNewTransaction ? "true" : "false");

            Fixture.AddCommand($"AttributeInstrumentation MakeOtherTransactionWithThreadedCallToInstrumentedMethod");
            Fixture.AddCommand("RootCommands DelaySeconds 5");

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
            var expectedMetrics = new List<Assertions.ExpectedMetric>();

            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/all", callCount = ForceNewTransaction ? 2 : 1 });
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 });
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 });

            if (ForceNewTransaction)
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });
            }
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });

            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}
