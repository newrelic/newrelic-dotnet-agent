/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    public class ExternalSegmentData : AbstractSegmentData
    {
        private const string TransactionGuidSegmentParameterKey = "transaction_guid";
        public Uri Uri { get; }
        public string Method { get; }

        public ExternalSegmentData(Uri uri, string method, CrossApplicationResponseData crossApplicationResponseData = null)
        {
            Uri = uri;
            Method = method;
            CrossApplicationResponseData = crossApplicationResponseData;
        }

        public CrossApplicationResponseData CrossApplicationResponseData { get; set; }


        internal override IEnumerable<KeyValuePair<string, object>> Finish()
        {
            var parameters = new Dictionary<string, object>();

            // The CAT response data will not be null if the agent received a response that contained CAT headers (e.g. if the request went to an app that is monitored by a supported New Relic agent)
            if (CrossApplicationResponseData != null)
                parameters[TransactionGuidSegmentParameterKey] = CrossApplicationResponseData.TransactionGuid;

            var cleanUri = Strings.CleanUri(Uri);
            parameters["uri"] = cleanUri;

            return parameters;
        }

        public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
        {
            var duration = segment.Duration.Value;
            var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);

            MetricBuilder.TryBuildExternalRollupMetrics(Uri.Host, duration, txStats);

            // The CAT response data will be null if the agent did not receive a response that contained CAT headers (e.g. if the request went to an app that isn't monitored by a supported New Relic agent).
            // According to the agent spec, in the event when CAT response data is present, the agent generates ExternalTransaction/{host}/{cross_process_id}/{transaction_name} scoped metric to replace External/{host}/{method} scoped metric.
            if (CrossApplicationResponseData == null)
            {
                // Generate scoped and unscoped external metrics as CAT not present.
                MetricBuilder.TryBuildExternalSegmentMetric(Uri.Host, Method, duration, exclusiveDuration, txStats, false);
            }
            else
            {
                // Only generate unscoped metric for response with CAT headers because segments should only produce a single scoped metric and the CAT metric is more interesting than the external segment metric.
                MetricBuilder.TryBuildExternalSegmentMetric(Uri.Host, Method, duration, exclusiveDuration, txStats, true);

                var externalCrossProcessId = CrossApplicationResponseData.CrossProcessId;
                var externalTransactionName = CrossApplicationResponseData.TransactionName;

                MetricBuilder.TryBuildExternalAppMetric(Uri.Host, externalCrossProcessId, exclusiveDuration, txStats);
                MetricBuilder.TryBuildExternalTransactionMetric(Uri.Host, externalCrossProcessId, externalTransactionName, duration, exclusiveDuration, txStats);

            }
        }

        public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, IEnumerable<KeyValuePair<string, object>> newParameters)
        {
            return new TypedSegment<ExternalSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
        }

        public override string GetTransactionTraceName()
        {
            // APM expects metric names to be used for external segment trace names
            var name = CrossApplicationResponseData == null
                ? MetricNames.GetExternalHost(Uri.Host, "Stream", Method)
                : MetricNames.GetExternalTransaction(Uri.Host, CrossApplicationResponseData.CrossProcessId, CrossApplicationResponseData.TransactionName);
            return name.ToString();
        }

        public override bool IsCombinableWith(AbstractSegmentData otherData)
        {
            var otherTypedSegment = otherData as ExternalSegmentData;
            if (otherTypedSegment == null)
                return false;

            if (Uri != otherTypedSegment.Uri)
                return false;

            if (Method != otherTypedSegment.Method)
                return false;

            return true;
        }
    }
}
