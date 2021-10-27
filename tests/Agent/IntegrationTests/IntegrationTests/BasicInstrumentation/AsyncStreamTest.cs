// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetCoreTest]
    public class AsyncStreamTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCore3FeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCore3FeaturesFixture _fixture;

        public AsyncStreamTests(RemoteServiceFixtures.AspNetCore3FeaturesFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.AsyncStreamController", "GetNumbers");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.AsyncStreamController", "DoSomethingAsync");
                },
                exerciseApplication: () =>
                {
                    _fixture.AsyncStream();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);
            Assertions.MetricsExist(_expectedMetrics, metrics);
        }

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncStreamController/Get", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.AsyncStreamController/GetNumbers", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.AsyncStreamController/DoSomethingAsync", callCount = 10 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncStreamController/Get", metricScope = "WebTransaction/MVC/AsyncStream/Get", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.AsyncStreamController/GetNumbers", metricScope = "WebTransaction/MVC/AsyncStream/Get", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.AsyncStreamController/DoSomethingAsync", metricScope = "WebTransaction/MVC/AsyncStream/Get", callCount = 10 }
        };
    }
}
