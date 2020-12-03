// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres
{
    [NetFrameworkTest]
    public class PostgresAsyncLegacyAspPipelineTests : NewRelicIntegrationTest<PostgresBasicMvcFixture>
    {
        private readonly PostgresBasicMvcFixture _fixture;

        public PostgresAsyncLegacyAspPipelineTests(PostgresBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var webConfigPath = Path.Combine(fixture.DestinationApplicationDirectoryPath, "web.config");
                    new WebConfigModifier(webConfigPath).ForceLegacyAspPipeline();

                    var newRelicConfigFilePath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfigFilePath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigFilePath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetPostgresAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// Most application-level metrics (which come from async calls) are suppressed by the existence of the legacy asp pipeline
				new Assertions.ExpectedMetric { metricName = @"DotNet/PostgresController/PostgresAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Npgsql.NpgsqlConnection/Open", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/PostgresController/PostgresAsync", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// These should be suppressed by the existence of the legacy asp pipeline
				new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/PostgresController/PostgresAsync"},
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );

            var httpClientSuppressedRegex =
                @".* The method (.+) in class (.+) from assembly (.+) will not be instrumented. (.*)";
            Assert.NotNull(_fixture.AgentLog.TryGetLogLine(httpClientSuppressedRegex));
        }
    }
}
