// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CompositeTests
{
    [TestFixture]
    public class ApiSupportabilityMetricTests
    {
        private const string SupportabilityMetricPrefix = "Supportability/ApiInvocation/";

        private static CompositeTestAgent _compositeTestAgent;
        private IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private IConfigurationService _configSvc;
        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
            _apiSupportabilityMetricCounters = _compositeTestAgent.Container.Resolve<IApiSupportabilityMetricCounters>();
            _configSvc = _compositeTestAgent.Container.Resolve<IConfigurationService>();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void CurrentTransactionTest()
        {
            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var agentBridgeApi = new AgentBridgeApi(agentWrapperApi, _apiSupportabilityMetricCounters, _configSvc);

            var currentTransaction = agentBridgeApi.CurrentTransaction;

            HarvestAndValidateMetric("CurrentTransaction");
        }

        [Test]
        public void DisableBrowserMonitoring()
        {
            AgentApi.DisableBrowserMonitoring();
            HarvestAndValidateMetric("DisableBrowserMonitoring");
        }

        [Test]
        public void GetBrowserTimingHeader()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.GetBrowserTimingHeader(), "GetBrowserTimingHeader");
        }

        [Test]
        public void IgnoreApdex()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.IgnoreApdex(), "IgnoreApdex");
        }

        [Test]
        public void IgnoreTransaction()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.IgnoreTransaction(), "IgnoreTransaction");
        }

        [Test]
        public void IncrementCounter()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.IncrementCounter("fred"), "IncrementCounter");
        }

        [Test]
        public void NoticeError()
        {
            var exception = new IndexOutOfRangeException();
            CallAgentApiMethodRequiringTransaction(() => AgentApi.NoticeError(exception), "NoticeError");
        }

        [Test]
        public void RecordCustomEvent()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.RecordCustomEvent("customEvent", null), "RecordCustomEvent");
        }

        [Test]
        public void RecordMetric()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.RecordMetric("metricName", 1f), "RecordMetric");
        }

        [Test]
        public void RecordResponseTimeMetric()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.RecordResponseTimeMetric("responseTimeMetric", 1234L), "RecordResponseTimeMetric");
        }

        [Test]
        public void SetApplicationName()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.SetApplicationName("applicationName"), "SetApplicationName");
        }

        [Test]
        public void SetErrorGroupCallback()
        {
            AgentApi.SetErrorGroupCallback(null);

            HarvestAndValidateMetric("SetErrorGroupCallback");
        }

        [Test]
        public void SetTransactionName()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.SetTransactionName("custom", "transactionName"), "SetTransactionName");
        }

        [Test]
        public void SetUserParameters()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.SetUserParameters("userName", "accountName", "productName"), "SetUserParameters");
        }

        [Test]
        public void StartAgent()
        {
            CallAgentApiMethodRequiringTransaction(() => AgentApi.StartAgent(), "StartAgent");
        }

        private void CallAgentApiMethodRequiringTransaction(Action apiMethod, string expectedMetricName)
        {
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            apiMethod();
            transaction.End();

            HarvestAndValidateMetric(expectedMetricName);
        }

        private void HarvestAndValidateMetric(string expectedMetricName)
        {
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedMetrics = new List<ExpectedMetric>
            {
                new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + expectedMetricName, CallCount = 1}
            };

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
        }

        private void CallTransactionApiBridgeMethod(Action<TransactionBridgeApi> apiMethod, string expectedMetricName)
        {
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters, _configSvc);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            apiMethod(transactionBridgeApi);

            segment.End();
            transaction.End();


            HarvestAndValidateMetric(expectedMetricName);
        }

        [Test]
        public void TraceMetadataTest()
        {
            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var agentBridgeApi = new AgentBridgeApi(agentWrapperApi, _apiSupportabilityMetricCounters, _configSvc);

            var traceMetadata = agentBridgeApi.TraceMetadata;

            HarvestAndValidateMetric("TraceMetadata");
        }

        [Test]
        public void GetLinkingMetadataTest()
        {
            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var agentBridgeApi = new AgentBridgeApi(agentWrapperApi, _apiSupportabilityMetricCounters, _configSvc);

            var getLinkingMetadata = agentBridgeApi.GetLinkingMetadata();

            HarvestAndValidateMetric("GetLinkingMetadata");
        }
    }
}
