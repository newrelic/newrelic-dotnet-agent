// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.ReJit.NetCore
{
    /// <summary>
    /// Tests deleting a single attribute (metricName) on a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.DeleteAttributeTest.xml
    /// </summary>
    [NetCoreTest]
    public abstract class RejitDeleteAttributeBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture: AspNetCoreReJitMvcApplicationFixture
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        protected RejitDeleteAttributeBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            var deleteAttributeFilePath = Path.Combine(_fixture.RemoteApplication.DestinationExtensionsDirectoryPath, "Integration.Testing.DeleteAttributeTest.xml");

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(deleteAttributeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperDeleteAttribute", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomDeleteMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestDeleteAttribute();
                    XmlUtils.DeleteXmlAttribute(deleteAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestDeleteAttribute();
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                //transactions
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomDeleteMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Rejit/GetDeleteAttribute", callCount = 1 },

                // Unscoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteAttribute", callCount = 2 },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", metricScope = "WebTransaction/Custom/MyCustomDeleteMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteAttribute", metricScope = "WebTransaction/MVC/Rejit/GetDeleteAttribute", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }

    public class RejitDeleteAttribute : RejitDeleteAttributeBase<AspNetCoreReJitMvcApplicationFixture>
    {
        public RejitDeleteAttribute(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RejitDeleteAttributeWithTieredCompilation : RejitDeleteAttributeBase<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitDeleteAttributeWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
