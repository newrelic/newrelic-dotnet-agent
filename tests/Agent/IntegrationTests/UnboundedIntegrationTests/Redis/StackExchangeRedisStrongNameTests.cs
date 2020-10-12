// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Assert = Xunit.Assert;

namespace NewRelic.Agent.UnboundedIntegrationTests.Redis
{
    [NetFrameworkTest]
    public class StackExchangeRedisStrongNameTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public StackExchangeRedisStrongNameTests(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                        instrumentationFilePath, "NewRelic.Agent.Core.Tracer.Factories.Sql.DataReaderTracerFactory", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetStackExchangeRedisStrongName();
                    _fixture.GetStackExchangeRedisAsyncStrongName();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/all", callCount = 62 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/allWeb", callCount = 62 },
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
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/DEL", callCount = 2 },
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
				// The datastore operation happened inside a web transaction so there should be no allOther metrics
				new Assertions.ExpectedMetric {metricName = @"Datastore/allOther", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Datastore/Redis/allOther", callCount = 1}
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/RedisController/StackExchangeRedisStrongName");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RedisController/StackExchangeRedisStrongName");

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent)
            );
        }
    }
}
