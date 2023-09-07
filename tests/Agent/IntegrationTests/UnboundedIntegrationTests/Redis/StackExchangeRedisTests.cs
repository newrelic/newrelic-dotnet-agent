// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Redis
{
    public abstract class StackExchangeRedisTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        public StackExchangeRedisTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"StackExchangeRedisExerciser DoSomeWork");
            _fixture.AddCommand($"StackExchangeRedisExerciser DoSomeWorkAsync");

            // Confirm both transaction transforms have completed before moving on to host application shutdown, and final sendDataOnExit harvest
            _fixture.AddActions(setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                },
                exerciseApplication: () => _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 2)
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var syncTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.StackExchangeRedis.StackExchangeRedisExerciser/DoSomeWork";
            var asyncTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.StackExchangeRedis.StackExchangeRedisExerciser/DoSomeWorkAsync";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/all", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/allOther", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Redis/{CommonUtils.NormalizeHostname(StackExchangeRedisConfiguration.StackExchangeRedisServer)}/{StackExchangeRedisConfiguration.StackExchangeRedisPort}", callCount = 62},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/GET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/GET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/APPEND", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/GETRANGE", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SETRANGE", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/STRLEN", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/DECR", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/INCR", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HMSET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HINCRBY", callCount = 4 }, // increment and decrement
				new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HEXISTS", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HLEN", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HVALS", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/HLEN", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/EXISTS", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/RANDOMKEY", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/RENAME", callCount = 2 },
                // Delete can resolve to DEL or UNLINK depending on Redis version
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/(DEL|UNLINK)", IsRegexName = true, callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/PING", callCount = 4 }, //ping and identifyendpoint
				new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SADD", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SUNION", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SISMEMBER", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SCARD", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SMEMBERS", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SMOVE", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SRANDMEMBER", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SPOP", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SREM", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/PUBLISH", callCount = 2 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// The datastore operation happened inside a console app so there should be no allWeb metrics
				new Assertions.ExpectedMetric {metricName = @"Datastore/allWeb", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Datastore/Redis/allWeb", callCount = 1}
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            // The previous version of this test cheated a bit by naming both transactions the same. Since either one might be the slowest,
            // let's allow for either one
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(syncTransactionName) ?? _fixture.AgentLog.TryGetTransactionSample(asyncTransactionName);
            var transactionEvent1 = _fixture.AgentLog.TryGetTransactionEvent(syncTransactionName);
            var transactionEvent2 = _fixture.AgentLog.TryGetTransactionEvent(asyncTransactionName);

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent1),
                () => Assert.NotNull(transactionEvent2)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent1),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent2)
            );
        }
    }

    [NetFrameworkTest]
    public class StackExchangeRedisTestsFW462 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public StackExchangeRedisTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class StackExchangeRedisTestsFW471 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public StackExchangeRedisTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class StackExchangeRedisTestsFW48 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public StackExchangeRedisTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class StackExchangeRedisTestsFWLatest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public StackExchangeRedisTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class StackExchangeRedisTestsCoreOldest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public StackExchangeRedisTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class StackExchangeRedisTestsCoreLatest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public StackExchangeRedisTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }
}
