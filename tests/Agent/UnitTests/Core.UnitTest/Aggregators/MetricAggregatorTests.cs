// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class MetricAggregatorTests
    {
        private IDataTransportService _dataTransportService;
        private IMetricBuilder _metricBuilder;
        private IOutOfBandMetricSource[] _outOfBandMetricSources;
        private IAgentHealthReporter _agentHealthReporter;
        private IMetricNameService _metricNameService;
        private MetricAggregator _metricAggregator;
        private IDnsStatic _dnsStatic;
        private IProcessStatic _processStatic;
        private Action _harvestAction;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private TimeSpan? _harvestCycle;

        [SetUp]
        public void SetUp()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _outOfBandMetricSources = new IOutOfBandMetricSource[] { (IOutOfBandMetricSource)_agentHealthReporter };
            _dnsStatic = Mock.Create<IDnsStatic>();
            _processStatic = Mock.Create<IProcessStatic>();
            _metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);

            var scheduler = Mock.Create<IScheduler>();

            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _metricAggregator = new MetricAggregator(_dataTransportService, _metricBuilder, _metricNameService, _outOfBandMetricSources, _processStatic, scheduler);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _metricAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void Constructor_RegistersWithOutOfBandMetricSources()
        {
            foreach (var source in _outOfBandMetricSources)
            {
                Mock.Assert(() => source.RegisterPublishMetricHandler(Arg.IsAny<PublishMetricDelegate>()));
            }
        }

        [Test]
        public void Harvest_CallsCollectMetricsOnOutOfBandMetricSources()
        {
            _harvestAction();

            foreach (var source in _outOfBandMetricSources)
            {
                Mock.Assert(() => source.CollectMetrics());
            }
        }

        [Test]
        public void Harvest_SendsApmRequiredMetricEvenIfNoOtherMetricsExist()
        {
            var sentMetrics = Enumerable.Empty<MetricWireModel>();
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .DoInstead<IEnumerable<MetricWireModel>>(metrics => sentMetrics = metrics);

            _harvestAction();

            Assert.NotNull(sentMetrics);
            Assert.AreEqual(1, sentMetrics.Count());
            var sentMetric = sentMetrics.ElementAt(0);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.SupportabilityMetricHarvestTransmit, sentMetric.MetricName.Name),
                () => Assert.AreEqual(null, sentMetric.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric.Data.Value0),
                () => Assert.AreEqual(0, sentMetric.Data.Value1),
                () => Assert.AreEqual(0, sentMetric.Data.Value2),
                () => Assert.AreEqual(0, sentMetric.Data.Value3),
                () => Assert.AreEqual(0, sentMetric.Data.Value4),
                () => Assert.AreEqual(0, sentMetric.Data.Value5)
                );
        }

        public class TestMetricWireModel : IAllMetricStatsCollection
        {
            private TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "Test", false));

            public void CreateMetric(IMetricBuilder metricBuilder)
            {
                MetricBuilder.TryBuildSimpleSegmentMetric("test_metric", TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(1), txStats);
            }

            public void AddMetricsToCollection(MetricStatsCollection collection)
            {
                Thread.Sleep(5);
                txStats.AddMetricsToCollection(collection);
            }
        }

        [Test]
        public void StatsEngineQueue_Is_Busy()
        {
            var dataTransportService = Mock.Create<IDataTransportService>();
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            var outOfBandMetricSource = Mock.Create<IOutOfBandMetricSource>();
            var agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            var dnsStatic = Mock.Create<IDnsStatic>();
            var processStatic = Mock.Create<IProcessStatic>();

            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestAction = action);
            _metricAggregator = new MetricAggregator(dataTransportService, metricBuilder, _metricNameService, new[] { outOfBandMetricSource }, processStatic, scheduler);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            var sentMetrics = Enumerable.Empty<MetricWireModel>();
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .DoInstead<IEnumerable<MetricWireModel>>(metrics => sentMetrics = metrics);

            var maxThreads = 25;
            List<Thread> threads = new List<Thread>();

            for (int i = 0; i < maxThreads; i++)
            {
                var test = new TestMetricWireModel();
                test.CreateMetric(metricBuilder);

                Thread thread = new Thread(() =>
                {
                    _metricAggregator.Collect(test);
                });
                thread.IsBackground = true;
                threads.Add(thread);
                thread.Start();
            }

            for (int i = 0; i < maxThreads; i++)
            {
                threads[i].Join();
            }

            _harvestAction();

            //Check the number of metrics being sent up.
            Assert.IsTrue(sentMetrics.Count() == 3, "Count was " + sentMetrics.Count());
            // there should be one supportability and two DotNet (one scoped and one unscoped)
            string[] names = new string[] { "Supportability/MetricHarvest/transmit", "DotNet/test_metric" };
            foreach (MetricWireModel current in sentMetrics)
            {
                Assert.IsTrue(names.Contains(current.MetricName.Name), "Name is not present: " + current.MetricName.Name);
            }
        }

        [Test]
        public void Harvest_SendsReportedMetrics()
        {
            var sentMetrics = Enumerable.Empty<MetricWireModel>();
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .DoInstead<IEnumerable<MetricWireModel>>(metrics => sentMetrics = metrics);

            _metricAggregator.Collect(MetricWireModel.BuildMetric(_metricNameService, "DotNet/metric1", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))));
            _metricAggregator.Collect(MetricWireModel.BuildMetric(_metricNameService, "DotNet/metric2", "scope2", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5))));

            _harvestAction();

            Assert.NotNull(sentMetrics);
            Assert.AreEqual(3, sentMetrics.Count());
            MetricWireModel sentMetric1 = null;
            MetricWireModel sentMetric2 = null;
            MetricWireModel sentMetric3 = null;

            foreach (MetricWireModel metric in sentMetrics)
            {
                if ("DotNet/metric1".Equals(metric.MetricName.Name))
                {
                    sentMetric1 = metric;
                }
                else if ("DotNet/metric2".Equals(metric.MetricName.Name))
                {
                    sentMetric2 = metric;
                }
                else if ("Supportability/MetricHarvest/transmit".Equals(metric.MetricName.Name))
                {
                    sentMetric3 = metric;
                }
                else
                {
                    Assert.Fail("Unexpected metric name " + metric.MetricName.Name);
                }
            }

            NrAssert.Multiple(
                () => Assert.AreEqual("DotNet/metric1", sentMetric1.MetricName.Name),
                () => Assert.AreEqual(null, sentMetric1.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric1.Data.Value0),
                () => Assert.AreEqual(3, sentMetric1.Data.Value1),
                () => Assert.AreEqual(1, sentMetric1.Data.Value2),
                () => Assert.AreEqual(3, sentMetric1.Data.Value3),
                () => Assert.AreEqual(3, sentMetric1.Data.Value4),
                () => Assert.AreEqual(9, sentMetric1.Data.Value5),

                () => Assert.AreEqual("DotNet/metric2", sentMetric2.MetricName.Name),
                () => Assert.AreEqual("scope2", sentMetric2.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric2.Data.Value0),
                () => Assert.AreEqual(7, sentMetric2.Data.Value1),
                () => Assert.AreEqual(5, sentMetric2.Data.Value2),
                () => Assert.AreEqual(7, sentMetric2.Data.Value3),
                () => Assert.AreEqual(7, sentMetric2.Data.Value4),
                () => Assert.AreEqual(49, sentMetric2.Data.Value5),

                () => Assert.AreEqual(MetricNames.SupportabilityMetricHarvestTransmit, sentMetric3.MetricName.Name),
                () => Assert.AreEqual(null, sentMetric3.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric3.Data.Value0),
                () => Assert.AreEqual(0, sentMetric3.Data.Value1),
                () => Assert.AreEqual(0, sentMetric3.Data.Value2),
                () => Assert.AreEqual(0, sentMetric3.Data.Value3),
                () => Assert.AreEqual(0, sentMetric3.Data.Value4),
                () => Assert.AreEqual(0, sentMetric3.Data.Value5)
            );
        }

        [Test]
        public void Harvest_AggregatesMetricsBeforeSending()
        {
            var sentMetrics = Enumerable.Empty<MetricWireModel>();
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .DoInstead<IEnumerable<MetricWireModel>>(metrics => sentMetrics = metrics);

            _metricAggregator.Collect(MetricWireModel.BuildMetric(_metricNameService, "DotNet/metric1", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))));
            _metricAggregator.Collect(MetricWireModel.BuildMetric(_metricNameService, "DotNet/metric1", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5))));
            _metricAggregator.Collect(MetricWireModel.BuildMetric(_metricNameService, "DotNet/metric2", "scope2", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5))));

            _harvestAction();

            Assert.NotNull(sentMetrics);
            Assert.AreEqual(3, sentMetrics.Count());
            MetricWireModel sentMetric1 = null;
            MetricWireModel sentMetric2 = null;
            MetricWireModel sentMetric3 = null;

            foreach (MetricWireModel metric in sentMetrics)
            {
                if ("DotNet/metric1".Equals(metric.MetricName.Name))
                {
                    sentMetric1 = metric;
                }
                else if ("DotNet/metric2".Equals(metric.MetricName.Name))
                {
                    sentMetric2 = metric;
                }
                else if ("Supportability/MetricHarvest/transmit".Equals(metric.MetricName.Name))
                {
                    sentMetric3 = metric;
                }
                else
                {
                    Assert.Fail("Unexpected metric name " + metric.MetricName.Name);
                }
            }

            NrAssert.Multiple(
                () => Assert.AreEqual("DotNet/metric1", sentMetric1.MetricName.Name),
                () => Assert.AreEqual(null, sentMetric1.MetricName.Scope),
                () => Assert.AreEqual(2, sentMetric1.Data.Value0),
                () => Assert.AreEqual(10, sentMetric1.Data.Value1),
                () => Assert.AreEqual(6, sentMetric1.Data.Value2),
                () => Assert.AreEqual(3, sentMetric1.Data.Value3),
                () => Assert.AreEqual(7, sentMetric1.Data.Value4),
                () => Assert.AreEqual(58, sentMetric1.Data.Value5),

                () => Assert.AreEqual("DotNet/metric2", sentMetric2.MetricName.Name),
                () => Assert.AreEqual("scope2", sentMetric2.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric2.Data.Value0),
                () => Assert.AreEqual(7, sentMetric2.Data.Value1),
                () => Assert.AreEqual(5, sentMetric2.Data.Value2),
                () => Assert.AreEqual(7, sentMetric2.Data.Value3),
                () => Assert.AreEqual(7, sentMetric2.Data.Value4),
                () => Assert.AreEqual(49, sentMetric2.Data.Value5),

                () => Assert.AreEqual(MetricNames.SupportabilityMetricHarvestTransmit, sentMetric3.MetricName.Name),
                () => Assert.AreEqual(null, sentMetric3.MetricName.Scope),
                () => Assert.AreEqual(1, sentMetric3.Data.Value0),
                () => Assert.AreEqual(0, sentMetric3.Data.Value1),
                () => Assert.AreEqual(0, sentMetric3.Data.Value2),
                () => Assert.AreEqual(0, sentMetric3.Data.Value3),
                () => Assert.AreEqual(0, sentMetric3.Data.Value4),
                () => Assert.AreEqual(0, sentMetric3.Data.Value5)
            );
        }

        [Test]
        public void PreCleanShutdownEvent_TriggersHarvest()
        {
            EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());

            Mock.Assert(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()), Occurs.Once());
        }

        [Test]
        public void Metrics_are_retained_after_harvest_if_response_equals_retain()
        {
            // Arrange
            IEnumerable<MetricWireModel> unsentMetrics = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .Returns<IEnumerable<MetricWireModel>>((metrics) =>
                {
                    unsentMetrics = metrics;
                    return DataTransportResponseStatus.Retain;
                });

            _metricAggregator.Collect(BuildMetric(_metricNameService, "DotNet/metric1", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))));
            _harvestAction();
            _metricAggregator.Collect(BuildMetric(_metricNameService, "DotNet/metric2", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))));
            _harvestAction();

            // Assert
            Assert.AreEqual(3, unsentMetrics.Count()); // include "DotNet/metric1", "DotNet/metric2" and "Supportability/MetricHarvest/transmit" metrics
            Assert.IsTrue(unsentMetrics.Any(_ => _.MetricName.Name == "DotNet/metric1" && _.Data.Value0 == 1));
            Assert.IsTrue(unsentMetrics.Any(_ => _.MetricName.Name == "DotNet/metric2" && _.Data.Value0 == 1));
            Assert.IsTrue(unsentMetrics.Any(_ => _.MetricName.Name == "Supportability/MetricHarvest/transmit" && _.Data.Value0 == 2));
        }
    }
}
