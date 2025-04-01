// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public class MsSqlFailedExplainPlanTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureCoreLatest>
    {
        private readonly ConsoleDynamicMethodFixtureCoreLatest _fixture;

        public MsSqlFailedExplainPlanTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture)
        {
            var exerciserName = "MicrosoftDataSqlClientExerciser";

            _fixture = fixture;
            _fixture.TestLogger = outputHelper;

            var procedureName = Utilities.GenerateProcedureName();

            _fixture.AddCommand($"{exerciserName} MsSqlCreateStoredProcWithTempTable {procedureName}");
            _fixture.AddCommand($"{exerciserName} MsSqlStoredProcWithTempTable {procedureName}");
            _fixture.AddCommand($"{exerciserName} MsSqlStoredProcWithTempTable {procedureName}");
            _fixture.AddCommand($"{exerciserName} MsSqlDropStoredProcWithTempTable {procedureName}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(15);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");       //This has to stay at finest to ensure parameter check security

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ExplainPlainFailureLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();

        }

        [Fact]
        public void Test()
        {
            // verify that the log contains one explain plan failure log line - second call to the stored proc doesn't try to generate an explain plan
            Assert.Single(_fixture.AgentLog.TryGetLogLines(AgentLogBase.ExplainPlainFailureLogLineRegex));
        }
    }
}
