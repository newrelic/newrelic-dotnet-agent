// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    [NetFrameworkTest]
    public class RecordDatastoreSegment_Full_TestsFWLatest : RecordDatastoreSegmentTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RecordDatastoreSegment_Full_TestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class RecordDatastoreSegment_RequiredOnly_TestsFWLatest : RecordDatastoreSegmentTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RecordDatastoreSegment_RequiredOnly_TestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class RecordDatastoreSegment_Full_TestsCoreLatest : RecordDatastoreSegmentTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RecordDatastoreSegment_Full_TestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class RecordDatastoreSegment_RequiredOnly_TestsCoreLatest : RecordDatastoreSegmentTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RecordDatastoreSegment_RequiredOnly_TestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    public abstract class RecordDatastoreSegmentTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly TFixture _fixture;

        private bool _allOptions;

        public RecordDatastoreSegmentTests(TFixture fixture, ITestOutputHelper output, bool allOptions) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _allOptions = allOptions;

            if(_allOptions)
            {
                _fixture.AddCommand("ApiCalls TestFullRecordDatastoreSegment");
            }
            else
            {
                _fixture.AddCommand("ApiCalls TestRequiredRecordDatastoreSegment");
            }

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetLogLevel("finest");
                    configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
                    configModifier.ConfigureFasterMetricsHarvestCycle(25);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(30);
                }
            );

            _fixture.AddActions
            (
                exerciseApplication: () =>
                {
                    var threadProfileMatch = _fixture.AgentLog.WaitForLogLine(AgentLogFile.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Supportability/ApiInvocation/StartDatastoreSegment" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/statement/Other/MyModel/MyOperation" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/operation/Other/MyOperation" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/all" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/allOther" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/Other/all" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/Other/allOther" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = _allOptions ? "Datastore/instance/Other/MyHost/MyPath" : "Datastore/instance/Other/unknown/unknown" },
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Datastore/statement/Other/MyModel/MyOperation", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.Libraries.ApiCalls/RecordDatastoreSegment" },
            };

            // this will not exist if command text is missing.
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace()
                {
                    Sql = "MyCommandText",
                    DatastoreMetricName = "Datastore/statement/Other/MyModel/MyOperation",
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.Libraries.ApiCalls/RecordDatastoreSegment",
                    HasExplainPlan = false
                }
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics().ToList();

            var actualSqlTraces = _fixture.AgentLog.GetSqlTraces().ToList(); //0

            Assertions.MetricsExist(expectedMetrics, actualMetrics);

            if (_allOptions)
            {
                Assertions.SqlTraceExists(expectedSqlTraces, actualSqlTraces);
            }
            else // RequiredOnly
            {
                Assert.True(actualSqlTraces.Count == 0);
            }
            
        }
    }
}
