// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    /// <summary>
    /// This test verifies that our TerminatingSegmentWrapper behaves correctly when running on .NET Core. In particular, we really care
    /// about testing the behavior of removing the transaction data from AsyncLocal storage.
    /// </summary>
    [NetCoreTest]
    public class DetachWrapperTests : IClassFixture<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public DetachWrapperTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\TerminatingSegmentInstrumentation.xml";

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreMvcBasicRequestsApplication", "AspNetCoreMvcBasicRequestsApplication.Controllers.DetachWrapperController", "AsyncMethodWithExternalCall", "DetachWrapper");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetCallAsyncExternal();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(_unexpectedMetrics, metrics)
                );
        }

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.com/Stream/GET"}
        };

        private readonly List<Assertions.ExpectedMetric> _unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET"}
        };
    }
}
