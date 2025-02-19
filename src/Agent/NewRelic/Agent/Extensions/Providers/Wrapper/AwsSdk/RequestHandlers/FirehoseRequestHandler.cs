// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers
{
    internal static class FirehoseRequestHandler
    {
        public const string VendorName = "Firehose";
        public static readonly List<string> MessageBrokerRequestTypes = new List<string> { "GetRecordsRequest", "PutRecordsRequest", "PutRecordRequest" };
        private static readonly ConcurrentDictionary<string, string> _operationNameCache = new();


        public static AfterWrappedMethodDelegate HandleFirehoseRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, ArnBuilder builder)
        {
            var requestType = request.GetType().Name as string;

            // @TODO get clarification from spec reviewers as to whether we're doing this for Firehose or not
            // Only some Firehose requests are treated as message queue operations (put records, get records).  The rest should be treated as ordinary spans.

            // Not all request types have a stream name or a stream ARN

            var streamName = GetStreamNameFromRequest(request);
            string arn = GetArnFromRequest(request);
            if (arn == null && streamName != null)
            {
                //TODO fix this
                //arn:aws:firehose:us-west-2:342444490463:deliverystream/AlexTestFirehoseStream
                arn = builder.Build("firehose", $"deliverystream/{streamName}");
            }

            var operation = _operationNameCache.GetOrAdd(requestType, requestType.Replace("Request", string.Empty).ToSnakeCase());

            ISegment segment;

            if (MessageBrokerRequestTypes.Contains(requestType))
            {
                var action = requestType.StartsWith("Put") ? MessageBrokerAction.Produce : MessageBrokerAction.Consume;

                segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, action, VendorName, destinationName: streamName, messagingSystemName: "aws_kinesis_delivery_streams", cloudAccountId: builder.AccountId, cloudRegion: builder.Region);
                segment.GetExperimentalApi().MakeLeaf();
            }
            else
            {
                var operationName = requestType.Replace("Request", "");

                if (streamName != null)
                {
                    operationName += "/" + streamName;
                }

                segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, VendorName, operationName, isLeaf: false);

            }

            if (arn != null)
            {
                segment.AddCloudSdkAttribute("cloud.resource_id", arn);
            }
            segment.AddCloudSdkAttribute("aws.operation", operation);
            segment.AddCloudSdkAttribute("aws.region", builder.Region);
            segment.AddCloudSdkAttribute("cloud.platform", "aws_kinesis_delivery_streams");

            return isAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, ProcessResponse, TaskContinuationOptions.ExecuteSynchronously)
                :
                Delegates.GetDelegateFor(
                    onComplete: segment.End,
                    onSuccess: () =>
                    {
                        return;
                    }
                );

            void ProcessResponse(Task responseTask)
            {
                return;
            }
        }

        private static string GetStreamNameFromRequest(dynamic request)
        {
            try
            {
                var streamName = request.DeliveryStreamName as string;
                if (streamName != null)
                {
                    return streamName;
                }
                // if StreamName is null/unavailable, StreamARN may exist
                var streamARN = GetArnFromRequest(request) as string;
                if (streamARN != null)
                {
                    // arn:aws:kinesis:us-west-2:342444490463:stream/AlexKinesisTesting
                    var arnParts = streamARN.Split(':');
                    // TODO: cache name based on arn for performance?
                    return arnParts[arnParts.Length - 1].Split('/')[1];
                }
            }
            catch
            {
            }
            return null;
        }

        private static string GetArnFromRequest(dynamic request)
        {
            try
            {
                var streamARN = request.DeliveryStreamARN as string;
                if (streamARN != null)
                {
                    return streamARN;
                }
            }
            catch
            {
            }
            return null;
        }

    }
}
