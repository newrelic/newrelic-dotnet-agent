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
        public void IncrementLogLinesCount_LevelIsNormalized()
        {
            _agentHealthReporter.IncrementLogLinesCount("info");
            _agentHealthReporter.IncrementLogLinesCount("Info");
            _agentHealthReporter.IncrementLogLinesCount("InFO");
            _agentHealthReporter.CollectLoggingMetrics();

            var infoLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/INFO");
            var allLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines");

            NrAssert.Multiple(
                () => Assert.AreEqual(2, _publishedMetrics.Count),
                () => Assert.AreEqual($"Logging/lines/INFO", infoLevelLines.MetricName.Name),
                () => Assert.AreEqual($"Logging/lines", allLines.MetricName.Name)
                );
        }

        [Test]
        public void IncrementLogLinesCount_CheckLevelsAndCounts()
        {
            _agentHealthReporter.IncrementLogLinesCount("info");
            _agentHealthReporter.IncrementLogLinesCount("debug");
            _agentHealthReporter.IncrementLogLinesCount("finest");
            _agentHealthReporter.CollectLoggingMetrics();

            var infoLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/INFO");
            var debugLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/DEBUG");
            var finestLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/FINEST");
            var allLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines");

            NrAssert.Multiple(
                () => Assert.AreEqual(4, _publishedMetrics.Count),
                () => Assert.AreEqual($"Logging/lines/INFO", infoLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, infoLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines/DEBUG", debugLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, debugLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines/FINEST", finestLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, finestLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines", allLines.MetricName.Name),
                () => Assert.AreEqual(3, allLines.Data.Value0)
                );
        }
    }
}
