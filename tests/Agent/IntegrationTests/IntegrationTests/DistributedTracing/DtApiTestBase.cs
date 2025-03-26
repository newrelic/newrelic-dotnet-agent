// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    public abstract class DtApiTestBase : NewRelicIntegrationTest<RemoteServiceFixtures.DistributedTracingApiFixture>
    {
        public enum TracingTestOption
        {
            Legacy,
            W3cAndNewrelicHeaders,
            None
        }

        protected readonly DistributedTracingApiFixture _fixture;

        protected readonly TracingTestOption _tracingTestOption;

        public DtApiTestBase(DistributedTracingApiFixture fixture, ITestOutputHelper output, TracingTestOption tracingTestOption) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.CommandLineArguments = tracingTestOption == TracingTestOption.W3cAndNewrelicHeaders ? "w3c" : null;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public abstract void Metrics();

    }
}
