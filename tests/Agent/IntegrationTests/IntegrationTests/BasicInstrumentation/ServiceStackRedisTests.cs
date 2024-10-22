// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class ServiceStackRedisTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public ServiceStackRedisTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.GetRedis();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new Assertions.ExpectedMetric {metricName = "Datastore/all", CallCountAllHarvests = 3},
                new Assertions.ExpectedMetric {metricName = "Datastore/Redis/all", CallCountAllHarvests = 3},

                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.SaveAsync, callCount = 1},
                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.SaveAsync, metricScope = "WebTransaction/MVC/RedisController/Get", callCount = 1},

                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.Shutdown, callCount = 1},
                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.Shutdown, metricScope = "WebTransaction/MVC/RedisController/Get", callCount = 1},

                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.RewriteAppendOnlyFileAsync, callCount = 1},
                new Assertions.ExpectedMetric {metricName = "Datastore/operation/Redis/" + ServiceStackRedisCommands.RewriteAppendOnlyFileAsync, metricScope = "WebTransaction/MVC/RedisController/Get", callCount = 1},
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, actualMetrics)
            );
        }
    }
}
