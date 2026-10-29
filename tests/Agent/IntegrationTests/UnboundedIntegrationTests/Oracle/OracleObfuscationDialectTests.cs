// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.Oracle;

public abstract class OracleObfuscationDialectTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;

    public OracleObfuscationDialectTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand("OracleExerciser DialectQuery");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(15);
                configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                // Without this the fast query never crosses the SQL trace threshold, so no sql_trace_data is sent
                configModifier.ForceSqlTraces();
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/DialectQuery";

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Oracle/user_tables/select", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Oracle/user_tables/select", CallCountAllHarvests = 1, metricScope = expectedTransactionName },
        };

        var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
        {
            new Assertions.ExpectedSqlTrace
            {
                TransactionName = expectedTransactionName,
                Sql = "? SELECT \"DEGREE\" FROM user_tables WHERE ROWNUM <= ? AND TABLE_NAME <> ?",
                DatastoreMetricName = "Datastore/statement/Oracle/user_tables/select",
            }
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
        );
    }
}

[Trait("Platform", "WindowsOnly")]
public class OracleObfuscationDialectTestsFW462 : OracleObfuscationDialectTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public OracleObfuscationDialectTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class OracleObfuscationDialectTestsFWLatest : OracleObfuscationDialectTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public OracleObfuscationDialectTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class OracleObfuscationDialectTestsCoreOldest : OracleObfuscationDialectTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public OracleObfuscationDialectTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class OracleObfuscationDialectTestsCoreLatest : OracleObfuscationDialectTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public OracleObfuscationDialectTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
