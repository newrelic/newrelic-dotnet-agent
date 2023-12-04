// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Metrics
{
    public enum CATSupportabilityCondition
    {
        Request_Create_Success,
        Request_Create_Failure,
        Request_Create_Failure_XProcID,
        Request_Accept_Success,
        Request_Accept_Failure,
        Request_Accept_Failure_NotTrusted,
        Request_Accept_Failure_Decode,
        Request_Accept_Multiple,
        Response_Create_Success,
        Response_Create_Failure,
        Response_Create_Failure_XProcID,
        Response_Accept_Success,
        Response_Accept_Failure,
        Response_Accept_MultipleResponses
    }


    public interface ICATSupportabilityMetricCounters : IOutOfBandMetricSource
    {
        void Record(CATSupportabilityCondition condition);
    }

    public class CATSupportabilityMetricCounters : ICATSupportabilityMetricCounters
    {

        private readonly Dictionary<CATSupportabilityCondition, InterlockedCounter> _counters;
        private PublishMetricDelegate _publishMetricDelegate;
        private readonly IMetricBuilder _metricBuilder;


        public CATSupportabilityMetricCounters(IMetricBuilder metricBuilder)
        {
            _metricBuilder = metricBuilder;

            _counters = Enum.GetValues(typeof(CATSupportabilityCondition))
                .Cast<CATSupportabilityCondition>()
                .ToDictionary(x => x, x => new InterlockedCounter());
        }

        public void CollectMetrics()
        {

            foreach (var kvp in _counters)
            {
                if (TryGetCount(kvp.Value, out var count))
                {
                    TrySend(_metricBuilder.TryBuildCATSupportabilityCountMetric(kvp.Key, count));
                }
            }
        }

        private void TrySend(MetricWireModel metric)
        {
            if (metric == null)
                return;

            if (_publishMetricDelegate == null)
            {
                Log.Warn("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricNameModel.Name);
                return;
            }

            try
            {
                _publishMetricDelegate(metric);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TrySend() failed");
            }
        }

        private bool TryGetCount(InterlockedCounter counter, out int metricCount)
        {
            metricCount = 0;
            if (counter.Value > 0)
            {
                metricCount = counter.Exchange(0);
                return true;
            }

            return false;
        }

        public void Record(CATSupportabilityCondition condition)
        {
            _counters[condition].Increment();
        }

        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
        {
            if (_publishMetricDelegate != null)
                Log.Warn("Existing PublishMetricDelegate registration being overwritten.");

            _publishMetricDelegate = publishMetricDelegate;
        }
    }
}
