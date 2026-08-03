// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.Redis;

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

                configModifier.ForceTransactionTraces().DisableEventListenerSamplers()
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
            new() { metricName = @"Datastore/all", CallCountAllHarvests = 66 },
            new() { metricName = @"Datastore/allOther", CallCountAllHarvests = 66 },
            new() { metricName = @"Datastore/Redis/all", CallCountAllHarvests = 66 },
            new() { metricName = @"Datastore/Redis/allOther", CallCountAllHarvests = 66 },
            new() { metricName = $@"Datastore/instance/Redis/{CommonUtils.NormalizeHostname(StackExchangeRedisConfiguration.StackExchangeRedisServer)}/{StackExchangeRedisConfiguration.StackExchangeRedisPort}", CallCountAllHarvests = 66},
            new() { metricName = @"Datastore/operation/Redis/SET", CallCountAllHarvests = 4 },
            new() { metricName = @"Datastore/operation/Redis/GET", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/APPEND", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/GETRANGE", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SETRANGE", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/STRLEN", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/DECR", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/INCR", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/HMSET", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/HINCRBY", CallCountAllHarvests = 4 }, // increment and decrement
            new() { metricName = @"Datastore/operation/Redis/HEXISTS", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/HLEN", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/HVALS", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/HLEN", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/EXISTS", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/RANDOMKEY", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/RENAME", CallCountAllHarvests = 2 },
            // Delete can resolve to DEL or UNLINK depending on Redis version
            new() { metricName = @"Datastore/operation/Redis/(DEL|UNLINK)", IsRegexName = true, CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/PING", CallCountAllHarvests = 4 }, //ping and identifyendpoint
            new() { metricName = @"Datastore/operation/Redis/SADD", CallCountAllHarvests = 4 },
            new() { metricName = @"Datastore/operation/Redis/SUNION", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SISMEMBER", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SCARD", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SMEMBERS", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SMOVE", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SRANDMEMBER", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SPOP", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/SREM", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/PUBLISH", CallCountAllHarvests = 2 },
            new() { metricName = @"Datastore/operation/Redis/EXEC", CallCountAllHarvests = 2}
        };

        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // The datastore operation happened inside a console app so there should be no allWeb metrics
            new() {metricName = @"Datastore/allWeb", CallCountAllHarvests = 1},
            new() {metricName = @"Datastore/Redis/allWeb", CallCountAllHarvests = 1}
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

public class StackExchangeRedisTestsFW462 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public StackExchangeRedisTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class StackExchangeRedisTestsFW471 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW471>
{
    public StackExchangeRedisTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class StackExchangeRedisTestsFW48 : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public StackExchangeRedisTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class StackExchangeRedisTestsFWLatest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public StackExchangeRedisTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class StackExchangeRedisTestsCoreOldest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public StackExchangeRedisTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class StackExchangeRedisTestsCoreLatest : StackExchangeRedisTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public StackExchangeRedisTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}