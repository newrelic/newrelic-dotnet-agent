// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    [NetFrameworkTest]
    public class AspNetCoreMvcCoreFrameworkTests : IClassFixture<AspNetCoreMvcCoreFrameworkFixture>
    {
        private readonly AspNetCoreMvcCoreFrameworkFixture _fixture;

        public AspNetCoreMvcCoreFrameworkTests(AspNetCoreMvcCoreFrameworkFixture fixture, ITestOutputHelper output)
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Values/Get", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Values/Get", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ValuesController/Get", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ValuesController/Get", metricScope = @"WebTransaction/MVC/Values/Get", callCount = 2 },
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
