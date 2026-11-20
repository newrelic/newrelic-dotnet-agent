// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing;

// Generic on the fixture so the same assertions run in agent mode and in OTel distro mode (hosted web core under the
// OTel native profiler + the New Relic distro plugin). The distro subclass lives in the OtelDistro namespace, which CI
// does not select (it needs NR_DEV_OTEL_HOME).
public abstract class DTSupportabilityMetricTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.DTBasicMVCApplicationFixture
{
    readonly TFixture _fixture;

    protected DTSupportabilityMetricTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ForceTransactionTraces();
                configModifier.SetOrDeleteSpanEventsEnabled(true);
            },
            exerciseApplication: () =>
            {
                _fixture.GenerateMajorVersionMetric();
                _fixture.GenerateIgnoredNullMetric();
                _fixture.GenerateParsePayloadMetric();
                _fixture.GenerateAcceptSuccessMetric();
                _fixture.GenerateUntrustedAccountMetric();
                _fixture.GenerateCreateSuccessMetric();
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/Null", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/ParseException", CallCountAllHarvests = 1 },
            // The methods for GenerateAcceptSuccessMetric and GenerateCreateSuccessMetric result in AcceptPayload/Success metrics so we should look for two.
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Success", CallCountAllHarvests = 2 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", CallCountAllHarvests = 1 },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics)
        );
    }

}

[Trait("Platform", "WindowsOnly")]
public class DTSupportabilityMetricTests : DTSupportabilityMetricTestsBase<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
{
    public DTSupportabilityMetricTests(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
