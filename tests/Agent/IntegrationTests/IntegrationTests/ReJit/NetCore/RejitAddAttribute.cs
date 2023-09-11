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
    /// Tests adding an attribute (metricName) to a node (tracerFactory).
    /// Out of necessity, this attribute was removed prior to agent startup since it was easier to create then remove using existing methods.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddAttributeTest.xml
    /// </summary>
    [NetCoreTest]
    public abstract class RejitAddAttributeBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture: AspNetCoreReJitMvcApplicationFixture
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        protected RejitAddAttributeBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            var addAttributeFilePath = Path.Combine(_fixture.RemoteApplication.DestinationExtensionsDirectoryPath, "Integration.Testing.AddAttributeTest.xml");

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(addAttributeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddAttribute", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddBeforeMetricName", 7);
                    XmlUtils.DeleteXmlAttribute(addAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName");
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddAttribute();
                    XmlUtils.ModifyOrCreateXmlAttribute(addAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName", "MyCustomAddAfterMetricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestAddAttribute();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddAfterMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Rejit/GetAddAttribute", callCount = 1 },

                // Unscoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddAfterMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddAttribute", callCount = 2 },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddAfterMetricName", metricScope = "WebTransaction/Custom/MyCustomAddAfterMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddAttribute", metricScope = "WebTransaction/MVC/Rejit/GetAddAttribute", callCount = 1 }
            };

            var notExpcetedMetrics = new List<Assertions.ExpectedMetric>
            {
                //transactions
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddBeforeMetricName", callCount = 1 },

                // Unscoped
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddBeforeMetricName", callCount = 1 },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddBeforeMetricName", metricScope = "WebTransaction/Custom/MyCustomAddBeforeMetricName", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(notExpcetedMetrics, metrics)
            );
        }
    }

    public class RejitAddAttribute : RejitAddAttributeBase<AspNetCoreReJitMvcApplicationFixture>
    {
        public RejitAddAttribute(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RejitAddAttributeWithTieredCompilation : RejitAddAttributeBase<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitAddAttributeWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
