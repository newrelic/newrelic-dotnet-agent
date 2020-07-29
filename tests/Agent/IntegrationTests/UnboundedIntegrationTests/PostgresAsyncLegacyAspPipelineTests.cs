using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
    public class PostgresAsyncLegacyAspPipelineTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public PostgresAsyncLegacyAspPipelineTests(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
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

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml";
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
                // The Npgsql driver executes an unrelated SELECT query while connecting, otherwise these
                // would all be zero since the application-level metrics (which come from async calls) are suppressed by the existence of the legacy asp pipeline
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{PostgresConfiguration.PostgresServer}/{PostgresConfiguration.PostgresPort}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // These should be suppressed by the existence of the legacy asp pipeline
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/PostgresAsync"},
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
