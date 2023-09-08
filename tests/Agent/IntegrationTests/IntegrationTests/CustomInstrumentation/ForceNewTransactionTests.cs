// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

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
    public class GloballyForceNewTransactionEnabledTestsCoreOldest : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public GloballyForceNewTransactionEnabledTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
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
    public class GloballyForceNewTransactionDisabledTestsCoreOldest : GloballyForceNewTransactionTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public GloballyForceNewTransactionDisabledTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(false, fixture, output)
        {
        }
    }

    public abstract class GloballyForceNewTransactionTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string LibraryClassName = "MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation";

        protected readonly TFixture _fixture;

        private readonly bool ForceNewTransaction;

        public GloballyForceNewTransactionTests(bool forceNewTransaction, TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            ForceNewTransaction = forceNewTransaction;

            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.EnvironmentVariables.Add("NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD", ForceNewTransaction ? "true" : "false");

            _fixture.AddCommand($"AttributeInstrumentation MakeOtherTransactionWithThreadedCallToInstrumentedMethod");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(10));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"OtherTransaction/all", callCount = ForceNewTransaction ? 2 : 1 },
                new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 }
            };

            if (ForceNewTransaction)
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });
            }
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}
