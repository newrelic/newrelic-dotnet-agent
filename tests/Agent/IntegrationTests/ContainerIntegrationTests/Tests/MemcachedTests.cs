// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

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
                    configModifier.LogToConsole();
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

            var datastoreStatementMemcachedGetValueOrCreateAsyncGet = "Datastore/statement/Memcached/GetValueOrCreateAsync/Get";
            var datastoreStatementMemcachedGetValueOrCreateAsyncAdd = "Datastore/statement/Memcached/GetValueOrCreateAsync/Add";
            var datastoreStatementMemcachedGetAdd = "Datastore/statement/Memcached/Get/Add";
            var datastoreStatementMemcachedGetGet = "Datastore/statement/Memcached/Get/Get";
            var datastoreStatementMemcachedGetGenAdd = "Datastore/statement/Memcached/GetGen/Add";
            var datastoreStatementMemcachedGetGenGet = "Datastore/statement/Memcached/GetGen/Get";
            var datastoreStatementMemcachedGetAsyncAdd = "Datastore/statement/Memcached/GetAsync/Add";
            var datastoreStatementMemcachedGetAsyncGet = "Datastore/statement/Memcached/GetAsync/Get";
            var datastoreStatementMemcachedGetAsyncGenAdd = "Datastore/statement/Memcached/GetAsyncGen/Add";
            var datastoreStatementMemcachedGetAsyncGenGet = "Datastore/statement/Memcached/GetAsyncGen/Get";
            var datastoreStatementMemcachedIncrementAdd = "Datastore/statement/Memcached/Increment/Add";
            var datastoreStatementMemcachedIncrementIncrement = "Datastore/statement/Memcached/Increment/Increment";
            var datastoreStatementMemcachedDecrementAdd = "Datastore/statement/Memcached/Decrement/Add";
            var datastoreStatementMemcachedDecrementDecrement = "Datastore/statement/Memcached/Decrement/Decrement";
            var datastoreStatementMemcachedTouchAsyncAdd = "Datastore/statement/Memcached/TouchAsync/Add";
            var datastoreStatementMemcachedTouchAsyncTouch = "Datastore/statement/Memcached/TouchAsync/Touch";
            var datastoreStatementMemcachedRemoveAdd = "Datastore/statement/Memcached/Remove/Add";
            var datastoreStatementMemcachedRemoveRemove = "Datastore/statement/Memcached/Remove/Remove";
            var datastoreStatementMemcachedRemoveAsyncAdd = "Datastore/statement/Memcached/RemoveAsync/Add";
            var datastoreStatementMemcachedRemoveAsyncRemove = "Datastore/statement/Memcached/RemoveAsync/Remove";

            var transactionName = "WebTransaction/MVC/Memcached/TestAllMethods";

            var metrics = _fixture.AgentLog.GetMetrics();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = datastoreOperationMemcachedGet, callCount = 5 },
                new() { metricName = datastoreOperationMemcachedIncrement, callCount = 1 },
                new() { metricName = datastoreOperationMemcachedDecrement, callCount = 1 },
                new() { metricName = datastoreOperationMemcachedRemove, callCount = 2 },

                new() { metricName = datastoreStatementMemcachedGetValueOrCreateAsyncGet, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetValueOrCreateAsyncAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetGet, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetGenAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetGenGet, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetAsyncAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetAsyncGet, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetAsyncGenAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedGetAsyncGenGet, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedIncrementAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedIncrementIncrement, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedDecrementAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedDecrementDecrement, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedRemoveAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedRemoveRemove, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedRemoveAsyncAdd, callCount = 1 },
                new() { metricName = datastoreStatementMemcachedRemoveAsyncRemove, callCount = 1 },

                new() { metricName = datastoreStatementMemcachedGetValueOrCreateAsyncGet, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetValueOrCreateAsyncAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetGet, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetGenAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetGenGet, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetAsyncAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetAsyncGet, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetAsyncGenAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedGetAsyncGenGet, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedIncrementAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedIncrementIncrement, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedDecrementAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedDecrementDecrement, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedRemoveAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedRemoveRemove, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedRemoveAsyncAdd, callCount = 1, metricScope = transactionName },
                new() { metricName = datastoreStatementMemcachedRemoveAsyncRemove, callCount = 1, metricScope = transactionName },
            };

            if (_fixture.DotnetVer == "6.0")
            {
                expectedMetrics.Add(new() { metricName = datastoreAll, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreAllOther, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAll, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAllOther, callCount = 20 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedAdd, callCount = 11 });
            }
            else if (_fixture.DotnetVer == "8.0")
            {
                expectedMetrics.Add(new() { metricName = datastoreAll, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreAllOther, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAll, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreMemcachedAllOther, callCount = 22 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedAdd, callCount = 12 });
                expectedMetrics.Add(new() { metricName = datastoreOperationMemcachedTouch, callCount = 1 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouchAsyncAdd, callCount = 1 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouchAsyncTouch, callCount = 1 });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouchAsyncAdd, callCount = 1, metricScope = transactionName });
                expectedMetrics.Add(new() { metricName = datastoreStatementMemcachedTouchAsyncTouch, metricScope = transactionName });
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

    public class MemcachedDotNet6Test : LinuxMemcachedTest<MemcachedDotNet6TestFixture>
    {
        public MemcachedDotNet6Test(MemcachedDotNet6TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    public class MemcachedDotNet8Test : LinuxMemcachedTest<MemcachedDotNet8TestFixture>
    {
        public MemcachedDotNet8Test(MemcachedDotNet8TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
