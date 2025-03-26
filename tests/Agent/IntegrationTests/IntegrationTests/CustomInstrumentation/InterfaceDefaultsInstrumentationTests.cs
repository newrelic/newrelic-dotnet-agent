// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetCoreTest]
    public class InterfaceDefaultsInstrumentationTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreFeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreFeaturesFixture _fixture;

        public InterfaceDefaultsInstrumentationTests(RemoteServiceFixtures.AspNetCoreFeaturesFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.ILoggerNoAttributes", "LogException");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesNoDefault", "LogMessage");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault", "LogException");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault", "LogMessage");
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
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ILoggerWithAttributes/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesNoDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithAttributes", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ILoggerWithAttributes/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesNoDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerWithAttributesOverridesDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithAttributes", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ILoggerNoAttributes/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesNoDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogMessage", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/InterfaceDefaultsController/GetWithoutAttributes", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ILoggerNoAttributes/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesNoDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogException", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.ConsoleLoggerNoAttributesOverridesDefault/LogMessage", metricScope = "WebTransaction/MVC/InterfaceDefaults/GetWithoutAttributes", callCount = 1 }
        };
    }
}
