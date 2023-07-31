// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
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
        private ConfigurationAutoResponder _configurationAutoResponder;

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _agentHealthReporter = new AgentHealthReporter(metricBuilder, Mock.Create<IScheduler>());
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [TearDown]
        public void TearDown()
        {
            _configurationAutoResponder.Dispose();
        }

        private static IConfiguration GetDefaultConfiguration()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.LogEventCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogDecoratorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogMetricsCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.InfiniteTracingCompression).Returns(true);
            return configuration;
        }

        [Test]
        public void ReportPreHarvest_SendsExpectedMetrics()
        {
            _agentHealthReporter.ReportAgentVersion("1.0");
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
        public void ReportsInfiniteTracingOneTimeMetricsOnlyOnce()
        {
            var expectedOneTimeMetrics = new Dictionary<string, long>
            {
                { "Supportability/InfiniteTracing/Compression/enabled", 1 }
            };

            _agentHealthReporter.CollectMetrics();
            var firstCollectionMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricName.Name, x.Data.Value0));
            CollectionAssert.IsSubsetOf(expectedOneTimeMetrics, firstCollectionMetricNamesAndValues);

            _agentHealthReporter.CollectMetrics();
            var secondCollectionMetricNames = _publishedMetrics.Select(x => x.MetricName);
            CollectionAssert.IsNotSubsetOf(expectedOneTimeMetrics.Keys, secondCollectionMetricNames);
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
            var unspecifiedDestinationAndAreaMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/UnspecifiedDestination/Output/Bytes").ToArray();
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
            var connectMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/connect/Output/Bytes").ToArray();
            Assert.AreEqual(1, connectMetric.Length);
            Assert.AreEqual(1, connectMetric[0].Data.Value0, 1); // call count
            Assert.AreEqual(100, connectMetric[0].Data.Value1, 100); // bytes sent
            Assert.AreEqual(200, connectMetric[0].Data.Value2, 200); // bytes received

            // Verify that subarea metric exists for Collector 'doSomething1'
            var doSomething1Metric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/doSomething1/Output/Bytes").ToArray();
            Assert.AreEqual(1, doSomething1Metric.Length);
            Assert.AreEqual(2, doSomething1Metric[0].Data.Value0); // call count
            Assert.AreEqual(500, doSomething1Metric[0].Data.Value1); // bytes sent
            Assert.AreEqual(700, doSomething1Metric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector 'doSomething2'
            var doSomething2Metric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/doSomething2/Output/Bytes").ToArray();
            Assert.AreEqual(1, doSomething2Metric.Length);
            Assert.AreEqual(1, doSomething2Metric[0].Data.Value0); // call count
            Assert.AreEqual(400, doSomething2Metric[0].Data.Value1); // bytes sent
            Assert.AreEqual(500, doSomething2Metric[0].Data.Value2); // bytes received

            // Verify that subarea metric exists for Collector data with unspecified api area
            var collectorUnspecifiedMetric = _publishedMetrics.Where(x => x.MetricName.Name == "Supportability/DotNET/Collector/UnspecifiedDestinationArea/Output/Bytes").ToArray();
            Assert.AreEqual(1, collectorUnspecifiedMetric.Length);
            Assert.AreEqual(1, collectorUnspecifiedMetric[0].Data.Value0); // call count
            Assert.AreEqual(100, collectorUnspecifiedMetric[0].Data.Value1); // bytes sent
            Assert.AreEqual(100, collectorUnspecifiedMetric[0].Data.Value2); // bytes received
        }

        [Test]
        public void IncrementLogLinesCount_CheckLevelsAndCounts()
        {
            _agentHealthReporter.IncrementLogLinesCount("INFO");
            _agentHealthReporter.IncrementLogLinesCount("DEBUG");
            _agentHealthReporter.IncrementLogLinesCount("FINEST");
            _agentHealthReporter.IncrementLogLinesCount("MISSING_LEVEL");
            _agentHealthReporter.IncrementLogDeniedCount("INFO");
            _agentHealthReporter.IncrementLogDeniedCount("DEBUG");
            _agentHealthReporter.IncrementLogDeniedCount("FINEST");
            _agentHealthReporter.IncrementLogDeniedCount("MISSING_LEVEL");

            _agentHealthReporter.CollectLoggingMetrics();

            var infoLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/INFO");
            var debugLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/DEBUG");
            var finestLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/FINEST");
            var missingLevelLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines/MISSING_LEVEL");
            var allLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/lines");

            var infoLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/denied/INFO");
            var debugLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/denied/DEBUG");
            var finestLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/denied/FINEST");
            var missingLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/denied/MISSING_LEVEL");
            var allDeniedLines = _publishedMetrics.First(metric => metric.MetricName.Name == "Logging/denied");

            NrAssert.Multiple(
                () => Assert.AreEqual(10, _publishedMetrics.Count),
                () => Assert.AreEqual($"Logging/lines/INFO", infoLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, infoLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines/DEBUG", debugLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, debugLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines/FINEST", finestLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, finestLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines/MISSING_LEVEL", missingLevelLines.MetricName.Name),
                () => Assert.AreEqual(1, missingLevelLines.Data.Value0),
                () => Assert.AreEqual($"Logging/lines", allLines.MetricName.Name),
                () => Assert.AreEqual(4, allLines.Data.Value0),
                () => Assert.AreEqual($"Logging/denied/INFO", infoLevelDeniedLines.MetricName.Name),
                () => Assert.AreEqual(1, infoLevelDeniedLines.Data.Value0),
                () => Assert.AreEqual($"Logging/denied/DEBUG", debugLevelDeniedLines.MetricName.Name),
                () => Assert.AreEqual(1, debugLevelDeniedLines.Data.Value0),
                () => Assert.AreEqual($"Logging/denied/FINEST", finestLevelDeniedLines.MetricName.Name),
                () => Assert.AreEqual(1, finestLevelDeniedLines.Data.Value0),
                () => Assert.AreEqual($"Logging/denied/MISSING_LEVEL", missingLevelDeniedLines.MetricName.Name),
                () => Assert.AreEqual(1, missingLevelDeniedLines.Data.Value0),
                () => Assert.AreEqual($"Logging/denied", allDeniedLines.MetricName.Name),
                () => Assert.AreEqual(4, allDeniedLines.Data.Value0)
                );
        }

        [Test]
        public void ReportLoggingSupportabilityMetrics()
        {
            _agentHealthReporter.ReportLoggingEventCollected();
            _agentHealthReporter.ReportLoggingEventsSent(2);
            _agentHealthReporter.ReportLoggingEventsDropped(3);
            _agentHealthReporter.ReportLogForwardingFramework("log4net");

            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("Framework1");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("Framework2");

            _agentHealthReporter.CollectMetrics();


            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/Logging/Forwarding/Seen", 1 },
                { "Supportability/Logging/Forwarding/Sent", 2 },
                { "Supportability/Logging/Forwarding/Dropped", 3 },
                { "Supportability/Logging/Metrics/DotNET/enabled", 1 },
                { "Supportability/Logging/Forwarding/DotNET/enabled", 1 },
                { "Supportability/Logging/LocalDecorating/DotNET/enabled", 1 },
                { "Supportability/Logging/DotNET/log4net/enabled", 1 },
                { "Supportability/Logging/Forwarding/DotNET/Framework1/enabled", 1},
                { "Supportability/Logging/Forwarding/DotNET/Framework2/enabled", 1}
            };
            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricName.Name, x.Data.Value0));

            CollectionAssert.IsSubsetOf(expectedMetricNamesAndValues, actualMetricNamesAndValues);
        }

        [Test]
        public void LoggingFrameworkOnlyReportedOnce()
        {
            _agentHealthReporter.ReportLogForwardingFramework("log4net");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("log4net");
            _agentHealthReporter.CollectMetrics();

            Assert.True(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/DotNET/log4net/enabled"));
            Assert.True(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/Forwarding/DotNET/log4net/enabled"));

            // Clear out captured metrics, and recollect
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.ReportLogForwardingFramework("log4net");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("log4net");
            _agentHealthReporter.ReportLogForwardingFramework("serilog");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("serilog");
            _agentHealthReporter.CollectMetrics();

            Assert.True(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/DotNET/serilog/enabled"));
            Assert.False(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/DotNET/log4net/enabled"));
            Assert.True(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/Forwarding/DotNET/serilog/enabled"));
            Assert.False(_publishedMetrics.Any(x => x.MetricName.Name == "Supportability/Logging/Forwarding/DotNET/log4net/enabled"));
        }

        [Test]
        public void LoggingConfigurationSupportabilityMetricsOnlyReportedOnce()
        {
            _agentHealthReporter.CollectMetrics();

            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/Logging/Metrics/DotNET/enabled", 1 },
                { "Supportability/Logging/Forwarding/DotNET/enabled", 1 },
                { "Supportability/Logging/LocalDecorating/DotNET/enabled", 1 },
            };

            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricName.Name, x.Data.Value0));

            CollectionAssert.IsSubsetOf(expectedMetricNamesAndValues, actualMetricNamesAndValues);

            // Clear out captured metrics, and recollect
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.CollectMetrics();

            actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricName.Name, x.Data.Value0));
            CollectionAssert.IsNotSubsetOf(expectedMetricNamesAndValues, actualMetricNamesAndValues);
        }
    }
}
