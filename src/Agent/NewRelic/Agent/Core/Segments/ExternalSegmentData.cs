// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Metrics;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Configuration;
using NewRelic.Parsing;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments
{
    public class ExternalSegmentData : AbstractSegmentData, IExternalSegmentData
    {
        private const string TransactionGuidSegmentParameterKey = "transaction_guid";

        private int? _httpStatusCode;

        public override SpanCategory SpanCategory => SpanCategory.Http;

        public Uri Uri { get; }
        public string Method { get; }
        public string Type { get; }

        public ExternalSegmentData(Uri uri, string method, CrossApplicationResponseData crossApplicationResponseData = null)
        {
            Uri = uri;
            Method = method;
            CrossApplicationResponseData = crossApplicationResponseData;
        }

        public CrossApplicationResponseData CrossApplicationResponseData { get; set; }

        public void SetHttpStatusCode(int httpStatusCode)
        {
            _httpStatusCode = httpStatusCode;
        }

        internal override IEnumerable<KeyValuePair<string, object>> Finish()
        {
            var parameters = new Dictionary<string, object>();

            // The CAT response data will not be null if the agent received a response that contained CAT headers (e.g. if the request went to an app that is monitored by a supported New Relic agent)
            if (CrossApplicationResponseData != null)
                parameters[TransactionGuidSegmentParameterKey] = CrossApplicationResponseData.TransactionGuid;

            var cleanUri = StringsHelper.CleanUri(Uri);
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


        public override void SetSpanTypeSpecificAttributes(SpanAttributeValueCollection attribVals)
        {
            AttribDefs.SpanCategory.TrySetValue(attribVals, SpanCategory.Http);
            AttribDefs.HttpUrl.TrySetValue(attribVals, Uri);
            AttribDefs.HttpMethod.TrySetValue(attribVals, Method);
            AttribDefs.Component.TrySetValue(attribVals, _segmentState.TypeName);
            AttribDefs.SpanKind.TrySetDefault(attribVals);
            AttribDefs.HttpStatusCode.TrySetValue(attribVals, _httpStatusCode);   //Attrib handles null
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
