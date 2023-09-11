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
    /// Tests changing the value for a single attribute (metricName) on a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.ChangeAttributeTest.xml
    /// </summary>
    [NetCoreTest]
    public abstract class RejitChangeAttributeValueBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AspNetCoreReJitMvcApplicationFixture
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        protected RejitChangeAttributeValueBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            var changeAttributeFilePath = Path.Combine(_fixture.RemoteApplication.DestinationExtensionsDirectoryPath, "Integration.Testing.ChangeAttributeTest.xml");

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(changeAttributeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperChangeAttributeValue", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomChangeMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestChangeAttributeValue();
                    XmlUtils.ModifyOrCreateXmlAttribute(changeAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName", "MyCustomRenamedMetricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestChangeAttributeValue();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomChangeMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomRenamedMetricName", callCount = 1 },

                // Unscoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomChangeMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenamedMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetChangeAttributeValue", callCount = 2 },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomChangeMetricName", metricScope = "WebTransaction/Custom/MyCustomChangeMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenamedMetricName", metricScope = "WebTransaction/Custom/MyCustomRenamedMetricName", callCount = 1 }
            };

            var notExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                //transactions
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Rejit/GetChangeAttributeValue" },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetChangeAttributeValue", metricScope = "WebTransaction/MVC/Rejit/GetChangeAttributeValue" }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(notExpectedMetrics, metrics)
            );
        }
    }

    public class RejitChangeAttributeValue : RejitChangeAttributeValueBase<AspNetCoreReJitMvcApplicationFixture>
    {
        public RejitChangeAttributeValue(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RejitChangeAttributeValueWithTieredCompilation : RejitChangeAttributeValueBase<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitChangeAttributeValueWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
