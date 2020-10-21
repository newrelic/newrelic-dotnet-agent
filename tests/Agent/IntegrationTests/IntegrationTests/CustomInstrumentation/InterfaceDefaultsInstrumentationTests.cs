// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetCoreTest]
    public class InterfaceDefaultsInstrumentationTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCore3FeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCore3FeaturesFixture _fixture;

        private const int ExpectedTransactionCount = 2;

        public InterfaceDefaultsInstrumentationTests(RemoteServiceFixtures.AspNetCore3FeaturesFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentation.xml";

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.ILoggerNoAttributes", "LogException");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesNoDefault", "LogMessage");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault", "LogException");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCore3Features", "AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault", "LogMessage");
                },
                exerciseApplication: () =>
                {
                    _fixture.InterfaceDefaultsGetWithAttributes();
                    _fixture.InterfaceDefaultsGetWithoutAttributes();
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
            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ILoggerWithAttributes/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesNoDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithAttributes", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ILoggerWithAttributes/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesNoDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ILoggerNoAttributes/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesNoDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithoutAttributes", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ILoggerNoAttributes/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesNoDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCore3Features.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 }
        };
    }
}
