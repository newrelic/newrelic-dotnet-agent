// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Owin;

public abstract class OwinRequestQueueTimeTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.OwinWebApiFixture
{
    private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

    protected OwinRequestQueueTimeTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                // Past timestamp in ms: queue time should be positive and recorded.
                var pastMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - 2000;
                _fixture.GetWithCustomHeaders(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-Request-Start", $"t={pastMs}")
                });

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void QueueTimeMetricAndAttributePresent()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = "WebFrontend/QueueTime", callCount = 1 }
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics)
        );

        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
        NrAssert.Multiple(
            () => Assert.True(
                transactionEvents.Any(e => e.IntrinsicAttributes.ContainsKey("queueDuration")),
                "Expected at least one transaction event to contain the queueDuration intrinsic attribute.")
        );
    }
}

public class Owin2RequestQueueTimeTests : OwinRequestQueueTimeTestsBase<RemoteServiceFixtures.OwinWebApiFixture>
{
    public Owin2RequestQueueTimeTests(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class Owin3RequestQueueTimeTests : OwinRequestQueueTimeTestsBase<RemoteServiceFixtures.Owin3WebApiFixture>
{
    public Owin3RequestQueueTimeTests(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class Owin4RequestQueueTimeTests : OwinRequestQueueTimeTestsBase<RemoteServiceFixtures.Owin4WebApiFixture>
{
    public Owin4RequestQueueTimeTests(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
