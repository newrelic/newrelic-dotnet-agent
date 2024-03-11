// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Metrics
{
    public enum ApiMethod
    {
        CurrentTransaction = 0,
        DisableBrowserMonitoring = 1,
        GetBrowserTimingHeader = 2,
        IgnoreApdex = 3,
        IgnoreTransaction = 4,
        IncrementCounter = 5,
        NoticeError = 6,
        RecordCustomEvent = 7,
        RecordMetric = 8,
        RecordResponseTimeMetric = 9,
        SetApplicationName = 10,
        SetTransactionName = 11,
        SetUserParameters = 12,
        GetBrowserTimingFooter = 13,
        StartAgent = 14,
        SetTransactionUri = 15,
        TraceMetadata = 16,
        GetLinkingMetadata = 17,
        TransactionAddCustomAttribute = 18,
        TransactionGetCurrentSpan = 19,
        SpanAddCustomAttribute = 20,
        InsertDistributedTraceHeaders = 21,
        AcceptDistributedTraceHeaders = 22,
        SpanSetName = 23,
        SetErrorGroupCallback = 24,
        SetUserId = 25,
        StartDatastoreSegment = 26,
        SetLlmTokenCountingCallback = 27,
        RecordLlmFeedbackEvent = 28,
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

        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
        {
            if (_publishMetricDelegate != null)
                Log.Warn("Existing PublishMetricDelegate registration being overwritten.");

            _publishMetricDelegate = publishMetricDelegate;
        }

    }
}
