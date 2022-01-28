// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentHealth
{
    [TestFixture]
    public class AgentHealthReporterTests
    {
        private AgentHealthReporter _agentHealthReporter;
        private List<MetricWireModel> _publishedMetrics;

        [SetUp]
        public void SetUp()
        {
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _agentHealthReporter = new AgentHealthReporter(metricBuilder, Mock.Create<IScheduler>(), Mock.Create<IDnsStatic>());
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [Test]
        public void ReportPreHarvest_SendsExpectedMetrics()
        {
            _agentHealthReporter.ReportAgentVersion("1.0", "foo");
            Assert.AreEqual(1, _publishedMetrics.Count);
            var metric1 = _publishedMetrics.ElementAt(0);
            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/AgentVersion/1.0", metric1.MetricName.Name),
                () => Assert.AreEqual(null, metric1.MetricName.Scope),
                () => Assert.AreEqual(1, metric1.Data.Value0),
                () => Assert.AreEqual(0, metric1.Data.Value1),
                () => Assert.AreEqual(0, metric1.Data.Value2),
                () => Assert.AreEqual(0, metric1.Data.Value3),
                () => Assert.AreEqual(0, metric1.Data.Value4),
                () => Assert.AreEqual(0, metric1.Data.Value5)
                );
        }

        [Test]
        public void ReportWrapperShutdown_SendsExpectedMetrics()
        {
            _agentHealthReporter.ReportWrapperShutdown(Mock.Create<IWrapper>(), new Method(typeof(string), "FooMethod", "FooParam"));
            Assert.AreEqual(3, _publishedMetrics.Count);
            var metric0 = _publishedMetrics.ElementAt(0);
            var metric1 = _publishedMetrics.ElementAt(1);
            var metric2 = _publishedMetrics.ElementAt(2);
            Assert.AreEqual("Supportability/WrapperShutdown/all", metric0.MetricName.Name);
            Assert.AreEqual("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/all", metric1.MetricName.Name);
            Assert.AreEqual("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/String.FooMethod", metric2.MetricName.Name);
        }

        [Test]
        public void GenerateExpectedCollectorErrorSupportabilityMetrics()
        {
            _agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), HttpStatusCode.InternalServerError);
            Assert.AreEqual(2, _publishedMetrics.Count);
            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Agent/Collector/HTTPError/500", _publishedMetrics[0].MetricName.Name),
                () => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0),
                () => Assert.AreEqual("Supportability/Agent/Collector/test_method_endpoint/Duration", _publishedMetrics[1].MetricName.Name),
                () => Assert.AreEqual(1, _publishedMetrics[1].Data.Value0),
                () => Assert.AreEqual(1.5, _publishedMetrics[1].Data.Value1)
            );
        }

        [Test]
        public void ShouldNotGenerateHttpErrorCollectorErrorSupportabilityMetric()
        {
            _agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), statusCode: null);
            Assert.AreEqual(1, _publishedMetrics.Count);
            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Agent/Collector/test_method_endpoint/Duration", _publishedMetrics[0].MetricName.Name),
                () => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0),
                () => Assert.AreEqual(1.5, _publishedMetrics[0].Data.Value1)
            );
        }

        [Test]
        public void ReportSupportabilityCountMetric_DefaultCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            _agentHealthReporter.ReportSupportabilityCountMetric(MetricName);
            Assert.AreEqual(1, _publishedMetrics.Count);
            NrAssert.Multiple(
                () => Assert.AreEqual($"Supportability/{MetricName}", _publishedMetrics[0].MetricName.Name),
                () => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0)
            );
        }

        [Test]
        public void ReportSupportabilityCountMetric_SuppliedCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            _agentHealthReporter.ReportSupportabilityCountMetric(MetricName, 2);
            Assert.AreEqual(1, _publishedMetrics.Count);
            NrAssert.Multiple(
                () => Assert.AreEqual($"Supportability/{MetricName}", _publishedMetrics[0].MetricName.Name),
                () => Assert.AreEqual(2, _publishedMetrics[0].Data.Value0)
            );
        }

        [Test]
        public void CollectMetrics_ReportsAgentVersion()
        {
            var agentVersion = AgentInstallConfiguration.AgentVersion;
            _agentHealthReporter.CollectMetrics();
            NrAssert.Multiple(
                () => Assert.AreEqual($"Supportability/AgentVersion/{agentVersion}", _publishedMetrics[0].MetricName.Name),
                () => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0)
            );
        }

        [Test]
        public void ReportsInfiniteTracingSupportabilityMetrics()
        {
            _agentHealthReporter.ReportInfiniteTracingSpanResponseError();
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(StatusCode.Unimplemented));
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(StatusCode.OutOfRange));
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout();
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout();
            _agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(32);
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(1);
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(13);
            _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(1);
            _agentHealthReporter.CollectMetrics();

            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/InfiniteTracing/Span/Response/Error", 1 },
                { "Supportability/InfiniteTracing/Span/gRPC/UNIMPLEMENTED", 1 },
                { "Supportability/InfiniteTracing/Span/gRPC/OUT_OF_RANGE", 1 },
                { "Supportability/InfiniteTracing/Span/gRPC/Timeout", 2 },
                { "Supportability/InfiniteTracing/Span/Dropped", 32 },
                { "Supportability/InfiniteTracing/Span/Seen", 1 },
                { "Supportability/InfiniteTracing/Span/Sent", 13 },
                { "Supportability/InfiniteTracing/Span/Received", 1 }
            };
            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricName.Name, x.Data.Value0));

            CollectionAssert.IsSubsetOf(expectedMetricNamesAndValues, actualMetricNamesAndValues);
        }

        [Test]
        public void Verify_DataUsageSupportabilityMetrics()
        {
            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", "connect", 100, 200);
            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", "doSomething1", 200, 300);
            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", "doSomething1", 300, 400);
            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", "doSomething2", 400, 500);
            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", String.Empty, 100, 100);
            _agentHealthReporter.ReportSupportabilityDataUsage(String.Empty, String.Empty, 100, 100);
            _agentHealthReporter.CollectMetrics();

            // Verify that top level Unspecified destination metric exists with expected rolled up values
            var perDestinationUnspecifiedMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/UnspecifiedDestination/Output/Bytes").ToArray();
            Assert.AreEqual(1, perDestinationUnspecifiedMetric.Length);
            Assert.AreEqual(1, perDestinationUnspecifiedMetric[0].Data.Value0); // call count
            Assert.AreEqual(100, perDestinationUnspecifiedMetric[0].Data.Value1); // bytes sent
            Assert.AreEqual(100, perDestinationUnspecifiedMetric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector data with unspecified api area
            var unspecifiedDestinationAndAreaMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/UnspecifiedDestination/Output/Bytes/UnspecifiedDestinationArea").ToArray();
            Assert.AreEqual(1, unspecifiedDestinationAndAreaMetric.Length);
            Assert.AreEqual(1, unspecifiedDestinationAndAreaMetric[0].Data.Value0); // call count
            Assert.AreEqual(100, unspecifiedDestinationAndAreaMetric[0].Data.Value1); // bytes sent
            Assert.AreEqual(100, unspecifiedDestinationAndAreaMetric[0].Data.Value2); // bytes received

            // Verify that top level Collector destination metric exists with expected rolled up values
            var perDestinationCollectorMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/Output/Bytes").ToArray();
            Assert.AreEqual(1, perDestinationCollectorMetric.Length);
            Assert.AreEqual(5, perDestinationCollectorMetric[0].Data.Value0); // call count
            Assert.AreEqual(1100, perDestinationCollectorMetric[0].Data.Value1); // bytes sent
            Assert.AreEqual(1500, perDestinationCollectorMetric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector 'connect'
            var connectMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/Output/Bytes/connect").ToArray();
            Assert.AreEqual(1, connectMetric.Length);
            Assert.AreEqual(1, connectMetric[0].Data.Value0, 1); // call count
            Assert.AreEqual(100, connectMetric[0].Data.Value1, 100); // bytes sent
            Assert.AreEqual(200, connectMetric[0].Data.Value2, 200); // bytes received

            // Verify that subarea metric exists for Collector 'doSomething1'
            var doSomething1Metric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/Output/Bytes/doSomething1").ToArray();
            Assert.AreEqual(1, doSomething1Metric.Length);
            Assert.AreEqual(2, doSomething1Metric[0].Data.Value0); // call count
            Assert.AreEqual(500, doSomething1Metric[0].Data.Value1); // bytes sent
            Assert.AreEqual(700, doSomething1Metric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector 'doSomething2'
            var doSomething2Metric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/Output/Bytes/doSomething2").ToArray();
            Assert.AreEqual(1, doSomething2Metric.Length);
            Assert.AreEqual(1, doSomething2Metric[0].Data.Value0); // call count
            Assert.AreEqual(400, doSomething2Metric[0].Data.Value1); // bytes sent
            Assert.AreEqual(500, doSomething2Metric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector data with unspecified api area
            var collectorUnspecifiedMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/Output/Bytes/UnspecifiedDestinationArea").ToArray();
            Assert.AreEqual(1, collectorUnspecifiedMetric.Length);
            Assert.AreEqual(1, collectorUnspecifiedMetric[0].Data.Value0); // call count
            Assert.AreEqual(100, collectorUnspecifiedMetric[0].Data.Value1); // bytes sent
            Assert.AreEqual(100, collectorUnspecifiedMetric[0].Data.Value2); // bytes received
        }
    }
}
