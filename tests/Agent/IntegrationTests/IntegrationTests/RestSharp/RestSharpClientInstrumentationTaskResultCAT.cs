// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RestSharp
{
    public abstract class RestSharpInstrumentationTaskResultCATBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public RestSharpInstrumentationTaskResultCATBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"RestSharpService StartService {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port}");
            _fixture.AddCommand($"RestSharpExerciser TaskResultClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} GET true true");
            _fixture.AddCommand($"RestSharpExerciser TaskResultClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} PUT false false");
            _fixture.AddCommand($"RestSharpExerciser TaskResultClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} POST false false");
            _fixture.AddCommand($"RestSharpExerciser TaskResultClient {_fixture.DestinationServerName} {_fixture.RemoteApplication.Port} DELETE true true");
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
            var crossProcessId = _fixture.AgentLog.GetCrossProcessId();
            var callerTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp.RestSharpExerciser/TaskResultClient";

            var serverName = _fixture.DestinationServerName;
            ;
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "External/all", callCount = 4 },
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

    [NetFrameworkTest]
    public class RestSharpInstrumentationTaskResultCATFWLatest : RestSharpInstrumentationTaskResultCATBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RestSharpInstrumentationTaskResultCATFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationTaskResultCATFW48 : RestSharpInstrumentationTaskResultCATBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RestSharpInstrumentationTaskResultCATFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationTaskResultCATFW471 : RestSharpInstrumentationTaskResultCATBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RestSharpInstrumentationTaskResultCATFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RestSharpInstrumentationTaskResultCATFW462 : RestSharpInstrumentationTaskResultCATBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RestSharpInstrumentationTaskResultCATFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
