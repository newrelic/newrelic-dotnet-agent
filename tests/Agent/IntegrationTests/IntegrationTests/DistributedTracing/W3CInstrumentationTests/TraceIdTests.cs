// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing.W3CInstrumentationTests
{
    [NetFrameworkTest]
    public class TraceIdTests : NewRelicIntegrationTest<AspNetCore3BasicWebApiApplicationFixture>
    {
        private readonly AspNetCore3BasicWebApiApplicationFixture _fixture;
        private string _traceId;

        public TraceIdTests(AspNetCore3BasicWebApiApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                    configModifier.SetLogLevel("all");
                },
                exerciseApplication: () =>
                {
                    _traceId = _fixture.GetTraceId();
                    _fixture.RemoteApplication.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    _fixture.RemoteApplication.AgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();

        }

        [Fact]
        public void Test()
        {
            var txEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(txEvent);

            Assert.Equal(_traceId, txEvent.IntrinsicAttributes["traceId"]);

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            foreach (var span in spanEvents)
            {
                Assert.Equal(_traceId, span.IntrinsicAttributes["traceId"]);
            }
        }
    }
}
