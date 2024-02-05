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
using NewRelic.Core.Logging;
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
        private bool _enableLogging;
        private List<IDictionary<string, string>> _ignoredInstrumentation;

        [SetUp]
        public void SetUp()
        {
            _enableLogging = true;
            _ignoredInstrumentation = new List<IDictionary<string, string>>();
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
            _agentHealthReporter.Dispose();
        }

        private IConfiguration GetDefaultConfiguration()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.LogEventCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogDecoratorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogMetricsCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.InfiniteTracingCompression).Returns(true);
            Mock.Arrange(() => configuration.LoggingEnabled).Returns(() => _enableLogging);
            Mock.Arrange(() => configuration.IgnoredInstrumentation).Returns(() => _ignoredInstrumentation);
            return configuration;
        }

        [Test]
        public void ReportPreHarvest_SendsExpectedMetrics()
        {
            _agentHealthReporter.ReportAgentVersion("1.0");
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            var metric1 = _publishedMetrics.ElementAt(0);
            NrAssert.Multiple(
                () => Assert.That(metric1.MetricNameModel.Name, Is.EqualTo("Supportability/AgentVersion/1.0")),
                () => Assert.That(metric1.MetricNameModel.Scope, Is.EqualTo(null)),
                () => Assert.That(metric1.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(metric1.DataModel.Value1, Is.EqualTo(0)),
                () => Assert.That(metric1.DataModel.Value2, Is.EqualTo(0)),
                () => Assert.That(metric1.DataModel.Value3, Is.EqualTo(0)),
                () => Assert.That(metric1.DataModel.Value4, Is.EqualTo(0)),
                () => Assert.That(metric1.DataModel.Value5, Is.EqualTo(0))
                );
        }

        [Test]
        public void ReportWrapperShutdown_SendsExpectedMetrics()
        {
            _agentHealthReporter.ReportWrapperShutdown(Mock.Create<IWrapper>(), new Method(typeof(string), "FooMethod", "FooParam"));
            Assert.That(_publishedMetrics, Has.Count.EqualTo(3));
            var metric0 = _publishedMetrics.ElementAt(0);
            var metric1 = _publishedMetrics.ElementAt(1);
            var metric2 = _publishedMetrics.ElementAt(2);
            Assert.Multiple(() =>
            {
                Assert.That(metric0.MetricNameModel.Name, Is.EqualTo("Supportability/WrapperShutdown/all"));
                Assert.That(metric1.MetricNameModel.Name, Is.EqualTo("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/all"));
                Assert.That(metric2.MetricNameModel.Name, Is.EqualTo("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/String.FooMethod"));
            });
        }

        [Test]
        public void GenerateExpectedCollectorErrorSupportabilityMetrics()
        {
            _agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), HttpStatusCode.InternalServerError);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(2));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo("Supportability/Agent/Collector/HTTPError/500")),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(_publishedMetrics[1].MetricNameModel.Name, Is.EqualTo("Supportability/Agent/Collector/test_method_endpoint/Duration")),
                () => Assert.That(_publishedMetrics[1].DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(_publishedMetrics[1].DataModel.Value1, Is.EqualTo(1.5))
            );
        }

        [Test]
        public void ShouldNotGenerateHttpErrorCollectorErrorSupportabilityMetric()
        {
            _agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), statusCode: null);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo("Supportability/Agent/Collector/test_method_endpoint/Duration")),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(_publishedMetrics[0].DataModel.Value1, Is.EqualTo(1.5))
            );
        }

        [Test]
        public void ReportSupportabilityCountMetric_DefaultCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            _agentHealthReporter.ReportSupportabilityCountMetric(MetricName);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo($"Supportability/{MetricName}")),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void ReportSupportabilityCountMetric_SuppliedCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            _agentHealthReporter.ReportSupportabilityCountMetric(MetricName, 2);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo($"Supportability/{MetricName}")),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void ReportCountMetric()
        {
            const string MetricName = "Some/Metric/Name";
            _agentHealthReporter.ReportCountMetric(MetricName, 2);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo(MetricName)),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void ReportByteMetric()
        {
            const string MetricName = "Some/Metric/Name";
            const long totalBytes = 1024 * 1024 * 1024;
            _agentHealthReporter.ReportByteMetric(MetricName, totalBytes);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo(MetricName)),
                () => Assert.That(_publishedMetrics[0].DataModel, Is.EqualTo(MetricDataWireModel.BuildByteData(totalBytes)))
            );
        }

        [Test]
        public void ReportByteMetric_WithExclusiveBytes()
        {
            const string MetricName = "Some/Metric/Name";
            const long totalBytes = 1024 * 1024 * 1024;
            const long exclusiveBytes = 1024 * 1024 * 64;
            _agentHealthReporter.ReportByteMetric(MetricName, totalBytes, exclusiveBytes);
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo(MetricName)),
                () => Assert.That(_publishedMetrics[0].DataModel, Is.EqualTo(MetricDataWireModel.BuildByteData(totalBytes, exclusiveBytes)))
            );
        }

        [Test]
        public void CollectMetrics_ReportsAgentVersion()
        {
            var agentVersion = AgentInstallConfiguration.AgentVersion;
            _agentHealthReporter.CollectMetrics();
            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics[0].MetricNameModel.Name, Is.EqualTo($"Supportability/AgentVersion/{agentVersion}")),
                () => Assert.That(_publishedMetrics[0].DataModel.Value0, Is.EqualTo(1))
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
            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));

            Assert.That(expectedMetricNamesAndValues, Is.SubsetOf(actualMetricNamesAndValues));
        }

        [Test]
        public void ReportsInfiniteTracingOneTimeMetricsOnlyOnce()
        {
            var expectedOneTimeMetrics = new Dictionary<string, long>
            {
                { "Supportability/InfiniteTracing/Compression/enabled", 1 }
            };

            _agentHealthReporter.CollectMetrics();
            var firstCollectionMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));
            Assert.That(expectedOneTimeMetrics, Is.SubsetOf(firstCollectionMetricNamesAndValues));

            _agentHealthReporter.CollectMetrics();
            var secondCollectionMetricNames = _publishedMetrics.Select(x => x.MetricNameModel);
            Assert.That(expectedOneTimeMetrics.Keys, Is.Not.SubsetOf(secondCollectionMetricNames));
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
            var perDestinationUnspecifiedMetric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/UnspecifiedDestination/Output/Bytes").ToArray();
            Assert.That(perDestinationUnspecifiedMetric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(perDestinationUnspecifiedMetric[0].DataModel.Value0, Is.EqualTo(1)); // call count
                Assert.That(perDestinationUnspecifiedMetric[0].DataModel.Value1, Is.EqualTo(100)); // bytes sent
                Assert.That(perDestinationUnspecifiedMetric[0].DataModel.Value2, Is.EqualTo(100)); // bytes received
            });

            // Verify that subarea metric exists for Collector data with unspecified api area
            var unspecifiedDestinationAndAreaMetric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/UnspecifiedDestination/Output/Bytes").ToArray();
            Assert.That(unspecifiedDestinationAndAreaMetric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(unspecifiedDestinationAndAreaMetric[0].DataModel.Value0, Is.EqualTo(1)); // call count
                Assert.That(unspecifiedDestinationAndAreaMetric[0].DataModel.Value1, Is.EqualTo(100)); // bytes sent
                Assert.That(unspecifiedDestinationAndAreaMetric[0].DataModel.Value2, Is.EqualTo(100)); // bytes received
            });

            // Verify that top level Collector destination metric exists with expected rolled up values
            var perDestinationCollectorMetric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/Collector/Output/Bytes").ToArray();
            Assert.That(perDestinationCollectorMetric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(perDestinationCollectorMetric[0].DataModel.Value0, Is.EqualTo(5)); // call count
                Assert.That(perDestinationCollectorMetric[0].DataModel.Value1, Is.EqualTo(1100)); // bytes sent
                Assert.That(perDestinationCollectorMetric[0].DataModel.Value2, Is.EqualTo(1500)); // bytes received
            });

            // Verify that subarea metric exists for Collector 'connect'
            var connectMetric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/Collector/connect/Output/Bytes").ToArray();
            Assert.That(connectMetric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(connectMetric[0].DataModel.Value0, Is.EqualTo(1).Within(1)); // call count
                Assert.That(connectMetric[0].DataModel.Value1, Is.EqualTo(100).Within(100)); // bytes sent
                Assert.That(connectMetric[0].DataModel.Value2, Is.EqualTo(200).Within(200)); // bytes received
            });

            // Verify that subarea metric exists for Collector 'doSomething1'
            var doSomething1Metric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/Collector/doSomething1/Output/Bytes").ToArray();
            Assert.That(doSomething1Metric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(doSomething1Metric[0].DataModel.Value0, Is.EqualTo(2)); // call count
                Assert.That(doSomething1Metric[0].DataModel.Value1, Is.EqualTo(500)); // bytes sent
                Assert.That(doSomething1Metric[0].DataModel.Value2, Is.EqualTo(700)); // bytes received
            });

            // Verify that subarea metric exists for Collector 'doSomething2'
            var doSomething2Metric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/Collector/doSomething2/Output/Bytes").ToArray();
            Assert.That(doSomething2Metric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(doSomething2Metric[0].DataModel.Value0, Is.EqualTo(1)); // call count
                Assert.That(doSomething2Metric[0].DataModel.Value1, Is.EqualTo(400)); // bytes sent
                Assert.That(doSomething2Metric[0].DataModel.Value2, Is.EqualTo(500)); // bytes received
            });

            // Verify that subarea metric exists for Collector data with unspecified api area
            var collectorUnspecifiedMetric = _publishedMetrics.Where(x => x.MetricNameModel.Name == "Supportability/DotNET/Collector/UnspecifiedDestinationArea/Output/Bytes").ToArray();
            Assert.That(collectorUnspecifiedMetric, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(collectorUnspecifiedMetric[0].DataModel.Value0, Is.EqualTo(1)); // call count
                Assert.That(collectorUnspecifiedMetric[0].DataModel.Value1, Is.EqualTo(100)); // bytes sent
                Assert.That(collectorUnspecifiedMetric[0].DataModel.Value2, Is.EqualTo(100)); // bytes received
            });
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

            var infoLevelLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/lines/INFO");
            var debugLevelLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/lines/DEBUG");
            var finestLevelLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/lines/FINEST");
            var missingLevelLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/lines/MISSING_LEVEL");
            var allLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/lines");

            var infoLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/denied/INFO");
            var debugLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/denied/DEBUG");
            var finestLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/denied/FINEST");
            var missingLevelDeniedLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/denied/MISSING_LEVEL");
            var allDeniedLines = _publishedMetrics.First(metric => metric.MetricNameModel.Name == "Logging/denied");

            NrAssert.Multiple(
                () => Assert.That(_publishedMetrics, Has.Count.EqualTo(10)),
                () => Assert.That(infoLevelLines.MetricNameModel.Name, Is.EqualTo($"Logging/lines/INFO")),
                () => Assert.That(infoLevelLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(debugLevelLines.MetricNameModel.Name, Is.EqualTo($"Logging/lines/DEBUG")),
                () => Assert.That(debugLevelLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(finestLevelLines.MetricNameModel.Name, Is.EqualTo($"Logging/lines/FINEST")),
                () => Assert.That(finestLevelLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(missingLevelLines.MetricNameModel.Name, Is.EqualTo($"Logging/lines/MISSING_LEVEL")),
                () => Assert.That(missingLevelLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(allLines.MetricNameModel.Name, Is.EqualTo($"Logging/lines")),
                () => Assert.That(allLines.DataModel.Value0, Is.EqualTo(4)),
                () => Assert.That(infoLevelDeniedLines.MetricNameModel.Name, Is.EqualTo($"Logging/denied/INFO")),
                () => Assert.That(infoLevelDeniedLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(debugLevelDeniedLines.MetricNameModel.Name, Is.EqualTo($"Logging/denied/DEBUG")),
                () => Assert.That(debugLevelDeniedLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(finestLevelDeniedLines.MetricNameModel.Name, Is.EqualTo($"Logging/denied/FINEST")),
                () => Assert.That(finestLevelDeniedLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(missingLevelDeniedLines.MetricNameModel.Name, Is.EqualTo($"Logging/denied/MISSING_LEVEL")),
                () => Assert.That(missingLevelDeniedLines.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(allDeniedLines.MetricNameModel.Name, Is.EqualTo($"Logging/denied")),
                () => Assert.That(allDeniedLines.DataModel.Value0, Is.EqualTo(4))
                );
        }

        [Test]
        public void ReportLoggingSupportabilityMetrics()
        {
            _agentHealthReporter.ReportLoggingEventCollected();
            _agentHealthReporter.ReportLoggingEventsSent(2);
            _agentHealthReporter.ReportLoggingEventsDropped(3);
            _agentHealthReporter.ReportLoggingEventsEmpty();
            _agentHealthReporter.ReportLogForwardingFramework("log4net");

            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("Framework1");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("Framework2");

            _agentHealthReporter.CollectMetrics();


            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/Logging/Forwarding/Seen", 1 },
                { "Supportability/Logging/Forwarding/Sent", 2 },
                { "Supportability/Logging/Forwarding/Dropped", 3 },
                { "Supportability/Logging/Forwarding/Empty", 1 },
                { "Supportability/Logging/Metrics/DotNET/enabled", 1 },
                { "Supportability/Logging/Forwarding/DotNET/enabled", 1 },
                { "Supportability/Logging/LocalDecorating/DotNET/enabled", 1 },
                { "Supportability/Logging/DotNET/log4net/enabled", 1 },
                { "Supportability/Logging/Forwarding/DotNET/Framework1/enabled", 1},
                { "Supportability/Logging/Forwarding/DotNET/Framework2/enabled", 1}
            };
            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));

            Assert.That(expectedMetricNamesAndValues, Is.SubsetOf(actualMetricNamesAndValues));
        }

        [Test]
        public void LoggingFrameworkOnlyReportedOnce()
        {
            _agentHealthReporter.ReportLogForwardingFramework("log4net");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("log4net");
            _agentHealthReporter.CollectMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/DotNET/log4net/enabled"), Is.True);
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/Forwarding/DotNET/log4net/enabled"), Is.True);
            });

            // Clear out captured metrics, and recollect
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.ReportLogForwardingFramework("log4net");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("log4net");
            _agentHealthReporter.ReportLogForwardingFramework("serilog");
            _agentHealthReporter.ReportLogForwardingEnabledWithFramework("serilog");
            _agentHealthReporter.CollectMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/DotNET/serilog/enabled"), Is.True);
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/DotNET/log4net/enabled"), Is.False);
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/Forwarding/DotNET/serilog/enabled"), Is.True);
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Logging/Forwarding/DotNET/log4net/enabled"), Is.False);
            });
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

            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));

            Assert.That(expectedMetricNamesAndValues, Is.SubsetOf(actualMetricNamesAndValues));

            // Clear out captured metrics, and recollect
            _publishedMetrics = new List<MetricWireModel>();
            _agentHealthReporter.CollectMetrics();

            actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));
            Assert.That(expectedMetricNamesAndValues, Is.Not.SubsetOf(actualMetricNamesAndValues));
        }

        [Test]
        public void LoggingDisabledSupportabilityMetricsPresent()
        {
            _enableLogging = false;
            Log.FileLoggingHasFailed = true;
            _agentHealthReporter.CollectMetrics();

            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/DotNET/AgentLogging/Disabled", 1 },
                { "Supportability/DotNET/AgentLogging/DisabledDueToError", 1 },
            };
            var actualMetricNamesAndValues = _publishedMetrics.Select(x => new KeyValuePair<string, long>(x.MetricNameModel.Name, x.DataModel.Value0));

            Assert.That(expectedMetricNamesAndValues, Is.SubsetOf(actualMetricNamesAndValues));

            Log.FileLoggingHasFailed = false;
        }

        [Test]
        public void LoggingDisabledSupportabilityMetricsMissing()
        {
            Log.FileLoggingHasFailed = false;
            _agentHealthReporter.CollectMetrics();

            var expectedMetricNamesAndValues = new Dictionary<string, long>
            {
                { "Supportability/DotNET/AgentLogging/Disabled", 1 },
                { "Supportability/DotNET/AgentLogging/DisabledDueToError", 1 },
            };
            Assert.Multiple(() =>
            {
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/DotNET/AgentLogging/Disabled"), Is.False);
                Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/DotNET/AgentLogging/DisabledDueToError"), Is.False);
            });
        }

        [Test]
        public void IgnoredInstrumentationSupportabiltyMetricPresent()
        {
            var expectedMetricName = new MetricNameWireModel("Supportability/Dotnet/IgnoredInstrumentation", null);
            var expectedMetricData = MetricDataWireModel.BuildGaugeValue(1);
            _ignoredInstrumentation.Add(new Dictionary<string, string> { { "assemblyName", "Assembly" } });

            _agentHealthReporter.CollectMetrics();

            var actualMetric = _publishedMetrics.Single(m => m.MetricNameModel.Equals(expectedMetricName));
            Assert.That(actualMetric.DataModel, Is.EqualTo(expectedMetricData),
                $"Got count {actualMetric.DataModel.Value0} and value {actualMetric.DataModel.Value1} instead of count {expectedMetricData.Value0} and value {expectedMetricData.Value1}.");
        }

        [Test]
        public void IgnoredInstrumentationSupportabiltyMetricMissing()
        {
            _agentHealthReporter.CollectMetrics();

            Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/Dotnet/IgnoredInstrumentation"), Is.False);
        }
    }
}
