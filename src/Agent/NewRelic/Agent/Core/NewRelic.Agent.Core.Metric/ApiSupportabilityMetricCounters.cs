// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Metric
{
    public enum ApiMethod
    {
        CreateDistributedTracePayload = 0,
        AcceptDistributedTracePayload = 1,
        CurrentTransaction = 2,
        AddCustomParameter = 3,     //To Be replaced by AddCustomAttribute
        DisableBrowserMonitoring = 4,
        GetBrowserTimingHeader = 5,
        IgnoreApdex = 6,
        IgnoreTransaction = 7,
        IncrementCounter = 8,
        NoticeError = 9,
        RecordCustomEvent = 10,
        RecordMetric = 11,
        RecordResponseTimeMetric = 12,
        SetApplicationName = 13,
        SetTransactionName = 14,
        SetUserParameters = 15,
        GetBrowserTimingFooter = 16,
        StartAgent = 17,
        SetTransactionUri = 18,
        TraceMetadata = 19,
        GetLinkingMetadata = 20,
        TransactionAddCustomAttribute = 21,
        TransactionGetCurrentSpan = 22,
        SpanAddCustomAttribute = 23,
        InsertDistributedTraceHeaders = 24,
        AcceptDistributedTraceHeaders = 25,
        SpanSetName = 26
    }

    public interface IApiSupportabilityMetricCounters : IOutOfBandMetricSource
    {
        void Record(ApiMethod method);
    }

    public class ApiSupportabilityMetricCounters : IApiSupportabilityMetricCounters
    {
        private static readonly string[] MetricNames;

        private static readonly InterlockedCounter[] Counters;

        private readonly IMetricBuilder _metricBuilder;

        private PublishMetricDelegate _publishMetricDelegate;

        static ApiSupportabilityMetricCounters()
        {
            var values = Enum.GetValues(typeof(ApiMethod));
            MetricNames = new string[values.Length];
            Counters = new InterlockedCounter[values.Length];
            foreach (var value in values)
            {
                MetricNames[(int)value] = Enum.GetName(typeof(ApiMethod), value);
                Counters[(int)value] = new InterlockedCounter();
            }

        }
        public ApiSupportabilityMetricCounters(IMetricBuilder metricBuilder)
        {
            _metricBuilder = metricBuilder;
        }

        public void Record(ApiMethod method)
        {
            Counters[(int)method].Increment();
        }

        public void CollectMetrics()
        {
            for (var x = 0; x < Counters.Length; x++)
            {
                if (TryGetCount(Counters[x], out var count))
                {
                    TrySend(_metricBuilder.TryBuildAgentApiMetric(MetricNames[x], count));
                }
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

        private void TrySend(MetricWireModel metric)
        {
            if (metric == null)
                return;

            if (_publishMetricDelegate == null)
            {
                Log.WarnFormat("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricName.Name);
                return;
            }

            try
            {
                _publishMetricDelegate(metric);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
        {
            if (_publishMetricDelegate != null)
                Log.Warn("Existing PublishMetricDelegate registration being overwritten.");

            _publishMetricDelegate = publishMetricDelegate;
        }

    }
}
