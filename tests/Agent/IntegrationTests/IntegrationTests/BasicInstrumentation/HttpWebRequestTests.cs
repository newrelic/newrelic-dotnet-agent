// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class HttpWebRequestTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        private readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

        public HttpWebRequestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.AddCommand("HttpWebRequestLibrary GetAll http://newrelic.com");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var errorEvents = _fixture.AgentLog.GetErrorEvents();
            var errorTraces = _fixture.AgentLog.GetErrorTraces();

            Assert.Empty(errorEvents);
            Assert.Empty(errorTraces);

            var metric = _fixture.AgentLog.GetMetricByName("External/newrelic.com/Stream/GET");

            Assert.Equal(3u, metric?.Values?.CallCount ?? 0);
        }
    }
}
