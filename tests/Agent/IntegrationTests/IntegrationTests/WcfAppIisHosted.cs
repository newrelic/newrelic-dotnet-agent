/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class WcfAppIisHosted : IClassFixture<RemoteServiceFixtures.WcfAppIisHosted>
    {
        private readonly RemoteServiceFixtures.WcfAppIisHosted _fixture;

        public WcfAppIisHosted(RemoteServiceFixtures.WcfAppIisHosted fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var newRelicConfig = _fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfig);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "attributes" }, "include", "service.request.*");

                    var webConfig = Path.Combine(_fixture.DestinationApplicationDirectoryPath, "web.config");
                    CommonUtils.ModifyOrCreateXmlAttribute(webConfig, "", new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" }, "aspNetCompatibilityEnabled", "false");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetString();
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Apdex" },
                new Assertions.ExpectedMetric { metricName = @"ApdexAll" },
                new Assertions.ExpectedMetric { metricName = @"Apdex/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.GetData" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.GetData", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.GetData", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.GetData", callCount = 1 },
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
            };

            var expectedTraceAttributes = new Dictionary<string, string>
            {
                { "service.request.value", "42" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assertions.TransactionTraceHasAttributes(expectedTraceAttributes, TransactionTraceAttributeType.Agent, transactionSample),

                () => Assert.Equal(1, transactionEvents.Count),

                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );
        }
    }
}
