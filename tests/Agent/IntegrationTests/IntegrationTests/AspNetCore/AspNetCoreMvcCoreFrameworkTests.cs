// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvcCoreFrameworkTests : NewRelicIntegrationTest<AspNetCoreMvcCoreFrameworkFixture>
    {
        private readonly AspNetCoreMvcCoreFrameworkFixture _fixture;

        public AspNetCoreMvcCoreFrameworkTests(AspNetCoreMvcCoreFrameworkFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetLogLevel("FINEST");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.Get();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Values/Get", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Values/Get", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ValuesController/Get", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ValuesController/Get", metricScope = @"WebTransaction/MVC/Values/Get", CallCountAllHarvests = 2 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var exceptionHandlerFeatureErrorLogLines = _fixture.AgentLog.TryGetLogLines("Inspecting errors from the IExceptionHandlerFeature is disabled");

            Assert.NotNull(metrics);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Single(exceptionHandlerFeatureErrorLogLines)
            );
        }
    }
}
