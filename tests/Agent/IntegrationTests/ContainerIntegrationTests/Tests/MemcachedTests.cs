// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Xunit;
using Xunit.Abstractions;
using Assert = Xunit.Assert;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests
{
    public abstract class LinuxMemcachedTest<T> : NewRelicIntegrationTest<T> where T : MemcachedTestFixtureBase
    {
        private readonly T _fixture;

        protected LinuxMemcachedTest(T fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions(setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("debug");
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.Delay(15); // wait long enough to ensure memcached
                    _fixture.ExerciseApplication();

                    _fixture.Delay(11); // wait long enough to ensure a metric harvest occurs after we exercise the app
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                    // shut down the container and wait for the agent log to see it
                    _fixture.ShutdownRemoteApplication();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var datastoreAll = "Datastore/all";
            var datastoreAllOther = "Datastore/allWeb";
            var datastoreMemcachedAll = "Datastore/Memcached/all";
            var datastoreMemcachedAllOther = "Datastore/Memcached/allWeb";

            var datastoreOperationMemcachedGet = "Datastore/operation/Memcached/Get";
            var datastoreOperationMemcachedAdd = "Datastore/operation/Memcached/Add";
            var datastoreOperationMemcachedIncrement = "Datastore/operation/Memcached/Increment";
            var datastoreOperationMemcachedDecrement = "Datastore/operation/Memcached/Decrement";
            var datastoreOperationMemcachedTouch = "Datastore/operation/Memcached/Touch";
            var datastoreOperationMemcachedRemove = "Datastore/operation/Memcached/Remove";

            var datastoreStatementMemcachedGet = "Datastore/statement/Memcached/cache/Get";
            var datastoreStatementMemcachedAdd = "Datastore/statement/Memcached/cache/Add";
            var datastoreStatementMemcachedIncrement = "Datastore/statement/Memcached/cache/Increment";
            var datastoreStatementMemcachedDecrement = "Datastore/statement/Memcached/cache/Decrement";
            var datastoreStatementMemcachedTouch = "Datastore/statement/Memcached/cache/Touch";
            var datastoreStatementMemcachedRemove = "Datastore/statement/Memcached/cache/Remove";

            var transactionName = "WebTransaction/MVC/Memcached/TestAllMethods";

            var metrics = _fixture.AgentLog.GetMetrics();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = datastoreOperationMemcachedGet, callCount = 5 },
                new() { metricName = datastoreOperationMemcachedIncrement, callCount = 1 },
                new() { metricName = datastoreOperationMemcachedDecrement, callCount = 1 },
                new() { metricName = datastoreOperationMemcachedRemove, callCount = 2 },

                new() { metricName = datastoreStatementMemcachedGet, callCount = 5 },
                new() { metricName = datastoreStatementMemcachedIncrement, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedDecrement, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedRemove, callCount = 2 },
                
                new() { metricName = datastoreStatementMemcachedGet, callCount = 5, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedIncrement, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedDecrement, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedRemove, callCount = 2, metricScope = transactionName },
            };

            if (_fixture.DotnetVer == "8.0") // EnyimMemcachedCore 2.x
            {
                expectedMetrics.Add(new() { metricName = datastoreAll, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreAllOther, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAll, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAllOther, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedAdd, callCount = 11 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedAdd, callCount = 11 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedAdd, callCount = 11, metricScope = transactionName });
            }
            else if (_fixture.DotnetVer == "9.0") // EnyimMemcachedCore 3.x
            {
                expectedMetrics.Add(new() { metricName = datastoreAll, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreAllOther, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAll, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAllOther, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedAdd, callCount = 12 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedTouch, callCount = 1 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedAdd, callCount = 12 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedAdd, callCount = 12, metricScope = transactionName });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouch, callCount = 1 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouch, callCount = 1, metricScope = transactionName });
            }
            else
            {
                Assert.Fail("Unexpected .NET version in Memcache.");
            }

            // The address can change from system to system
            // Datastore/instance/Memcached/<address>/11211
            var instanceMetric = metrics.FirstOrDefault(m =>
                m.MetricSpec.Name.StartsWith("Datastore/instance/Memcached/")
                && m.MetricSpec.Name.EndsWith("/11211"));

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(instanceMetric)
            );
        }
    }

    [Trait("Category", "x64")]
    public class MemcachedDotNet8Test : LinuxMemcachedTest<MemcachedDotNet8TestFixture>
    {
        public MemcachedDotNet8Test(MemcachedDotNet8TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [Trait("Category", "x64")]
    public class MemcachedDotNet9Test : LinuxMemcachedTest<MemcachedDotNet9TestFixture>
    {
        public MemcachedDotNet9Test(MemcachedDotNet9TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
