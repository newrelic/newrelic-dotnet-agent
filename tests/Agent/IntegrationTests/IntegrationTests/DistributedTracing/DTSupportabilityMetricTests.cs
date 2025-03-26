// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    [NetFrameworkTest]
    public class DTSupportabilityMetricTests : NewRelicIntegrationTest<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
    {
        readonly RemoteServiceFixtures.DTBasicMVCApplicationFixture _fixture;

        public DTSupportabilityMetricTests(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
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
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/Null", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/ParseException", callCount = 1 },
                // The methods for GenerateAcceptSuccessMetric and GenerateCreateSuccessMetric result in AcceptPayload/Success metrics so we should look for two.
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Success", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }

    }
}
