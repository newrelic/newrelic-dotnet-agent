// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RestSharp
{

    public abstract class RestSharpInstrumentationDistributedTracingBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public RestSharpInstrumentationDistributedTracingBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"RestSharpService StartService {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port}");
            _fixture.AddCommand($"RestSharpExerciser SyncClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} GET false");
            _fixture.AddCommand($"RestSharpExerciser SyncClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} PUT false");
            _fixture.AddCommand($"RestSharpExerciser SyncClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} POST false");
            _fixture.AddCommand($"RestSharpExerciser SyncClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} DELETE false");
            _fixture.AddCommand($"RestSharpExerciser RestSharpClientTaskCancelled {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port}");
            _fixture.AddCommand("RestSharpService StopService");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.SetLogLevel("finest");
                    configModifier.ForceTransactionTraces();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var callerTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp.RestSharpExerciser/SyncClient";
            var cancelledTrasnsactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp.RestSharpExerciser/RestSharpClientTaskCancelled";

            var serverName = _fixture.DestinationServerName;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "External/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/GET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/PUT", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/POST", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/DELETE", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/GET", metricScope = cancelledTrasnsactionName, callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/GET", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/PUT", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/POST", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/DELETE", metricScope = callerTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == callerTransactionName || sample.Path == @"WebTransaction/WebAPI/RestAPI/Get" || sample.Path == cancelledTrasnsactionName)
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            var externalSpanEvents = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("http.url"))
                .ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(transactionEventWithExternal),
                () => Assert.All(externalSpanEvents, AssertSpanEventsContainHttpStatusCodeForCompletedRequests)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);

            void AssertSpanEventsContainHttpStatusCodeForCompletedRequests(SpanEvent spanEvent)
            {
                var url = (string)spanEvent.AgentAttributes["http.url"];
                if (url.Contains("/RestAPI/Get/4"))
                {
                    // When an id of 4 is supplied, the client is supposed to timeout before the server has a chance to respond, so no status code
                    Assert.DoesNotContain("http.statusCode", spanEvent.AgentAttributes.Keys);
                }
                else
                {
                    Assert.Contains("http.statusCode", spanEvent.AgentAttributes.Keys);
                }
            }
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationDistributedTracingFWLatest : RestSharpInstrumentationDistributedTracingBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RestSharpInstrumentationDistributedTracingFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationDistributedTracingFW48 : RestSharpInstrumentationDistributedTracingBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RestSharpInstrumentationDistributedTracingFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationDistributedTracingFW471 : RestSharpInstrumentationDistributedTracingBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RestSharpInstrumentationDistributedTracingFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationDistributedTracingFW462 : RestSharpInstrumentationDistributedTracingBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RestSharpInstrumentationDistributedTracingFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
