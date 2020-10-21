// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    [NetFrameworkTest]
    public class MsSqlAsyncLegacyAspPipelineTests : NewRelicIntegrationTest<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.MsSqlBasicMvcFixture _fixture;

        public MsSqlAsyncLegacyAspPipelineTests(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var webConfigPath = Path.Combine(fixture.DestinationApplicationDirectoryPath, "web.config");
                    new WebConfigModifier(webConfigPath).ForceLegacyAspPipeline();

                    var newRelicConfigPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfigPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMsSqlAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = 5},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 2, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 3, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlAsync"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );

            var httpClientSuppressedRegex =
                @".* The method (.+) in class (.+) from assembly (.+) will not be instrumented. (.*)";
            Assert.NotNull(_fixture.AgentLog.TryGetLogLine(httpClientSuppressedRegex));
        }
    }
}
