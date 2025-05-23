// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RestSharp
{
    public abstract class RestSharpInstrumentationAsyncAwaitCATBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public RestSharpInstrumentationAsyncAwaitCATBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"RestSharpService StartService {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port}");
            _fixture.AddCommand($"RestSharpExerciser AsyncAwaitClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} GET true true");
            _fixture.AddCommand($"RestSharpExerciser AsyncAwaitClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} PUT false false");
            _fixture.AddCommand($"RestSharpExerciser AsyncAwaitClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} POST false false");
            _fixture.AddCommand($"RestSharpExerciser AsyncAwaitClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} DELETE true true");
            _fixture.AddCommand("RestSharpService StopService");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.EnableCat();
                    configModifier.ForceTransactionTraces();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverName = _fixture.DestinationServerName;

            var crossProcessId = _fixture.AgentLog.GetCrossProcessId();
            var callerTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp.RestSharpExerciser/AsyncAwaitClient";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "External/all", CallCountAllHarvests = 4 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/PUT", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/POST", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{serverName}/Stream/DELETE", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{serverName}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Get", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{serverName}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Put", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{serverName}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Post", metricScope = callerTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{serverName}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Delete", metricScope = callerTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == callerTransactionName || sample.Path == @"WebTransaction/WebAPI/RestAPI/Get")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(transactionEventWithExternal)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);
        }
    }

    public class RestSharpInstrumentationAsyncAwaitCATFWLatest : RestSharpInstrumentationAsyncAwaitCATBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RestSharpInstrumentationAsyncAwaitCATFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RestSharpInstrumentationAsyncAwaitCATFW48 : RestSharpInstrumentationAsyncAwaitCATBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RestSharpInstrumentationAsyncAwaitCATFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RestSharpInstrumentationAsyncAwaitCATFW471 : RestSharpInstrumentationAsyncAwaitCATBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RestSharpInstrumentationAsyncAwaitCATFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RestSharpInstrumentationAsyncAwaitCATFW462 : RestSharpInstrumentationAsyncAwaitCATBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RestSharpInstrumentationAsyncAwaitCATFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
