// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface IMetricAggregator
    {
        void Collect(IAllMetricStatsCollection metric);
    }

    public class MetricAggregator : AbstractAggregator<IAllMetricStatsCollection>, IMetricAggregator
    {
        private MetricStatsCollectionQueue _metricStatsCollectionQueue;
        private readonly IMetricBuilder _metricBuilder;
        private readonly IMetricNameService _metricNameService;
        private readonly IEnumerable<IOutOfBandMetricSource> _outOfBandMetricSources;

        public MetricAggregator(IDataTransportService dataTransportService, IMetricBuilder metricBuilder, IMetricNameService metricNameService, IEnumerable<IOutOfBandMetricSource> outOfBandMetricSources, IProcessStatic processStatic, IScheduler scheduler)
            : base(dataTransportService, scheduler, processStatic)
        {
            _metricBuilder = metricBuilder;
            _metricNameService = metricNameService;
            _outOfBandMetricSources = outOfBandMetricSources;

            foreach (var source in outOfBandMetricSources)
            {
                if (source != null)
                {
                    source.RegisterPublishMetricHandler(Collect);
                }
            }

            _metricStatsCollectionQueue = CreateMetricStatsCollectionQueue();
        }

        protected override TimeSpan HarvestCycle => _configuration.MetricsHarvestCycle;

        public MetricStatsCollectionQueue StatsCollectionQueue => _metricStatsCollectionQueue;

        protected override bool IsEnabled => true;

        #region interface and abstract override required methods

        public override void Collect(IAllMetricStatsCollection metric)
        {
            bool done = false;
            while (!done)
            {
                done = _metricStatsCollectionQueue.MergeMetrics(metric);
            }
        }
        protected override void ManualHarvest(string transactionId) => InternalHarvest(transactionId);

        protected override void Harvest() => InternalHarvest();

        protected void InternalHarvest(string transactionId = null)
        {
            Log.Finest("Metric harvest starting.");

            foreach (var source in _outOfBandMetricSources)
            {
                source.CollectMetrics();
            }

            var oldMetrics = GetStatsCollectionForHarvest();

            oldMetrics.MergeUnscopedStats(MetricNames.SupportabilityMetricHarvestTransmit, MetricDataWireModel.BuildCountData());
            var metricsToSend = oldMetrics.ConvertToJsonForSending(_metricNameService);

            var responseStatus = DataTransportService.Send(metricsToSend, transactionId);
            HandleResponse(responseStatus, metricsToSend);

            Log.Debug("Metric harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            ReplaceStatsCollectionQueue();
        }

        #endregion

        private void HandleResponse(DataTransportResponseStatus responseStatus, IEnumerable<MetricWireModel> unsuccessfulSendMetrics)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainMetricData(unsuccessfulSendMetrics);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }

        private void RetainMetricData(IEnumerable<MetricWireModel> unsuccessfulSendMetrics)
        {
            foreach (var metric in unsuccessfulSendMetrics)
            {
                Collect(metric);
            }
        }

        /// <summary>
        /// Replaces the current MetricStatsCollectionQueue with a new one and combines all the MetricStatsCollections in the 
        /// old queue into a single MetricStatsCollection that can serve as the source of aggregated metrics to
        /// send to the collector.
        /// </summary>
        /// <returns></returns>
        private MetricStatsCollection GetStatsCollectionForHarvest()
        {
            MetricStatsCollectionQueue oldMetricStatsCollectionQueue = ReplaceStatsCollectionQueue();
            return oldMetricStatsCollectionQueue.GetStatsCollectionForHarvest();
        }

        private MetricStatsCollectionQueue ReplaceStatsCollectionQueue()
        {
            MetricStatsCollectionQueue oldMetricStatsCollectionQueue = _metricStatsCollectionQueue;
            _metricStatsCollectionQueue = CreateMetricStatsCollectionQueue();
            return oldMetricStatsCollectionQueue;
        }

        private MetricStatsCollectionQueue CreateMetricStatsCollectionQueue()
        {
            return new MetricStatsCollectionQueue();
        }
    }
}
