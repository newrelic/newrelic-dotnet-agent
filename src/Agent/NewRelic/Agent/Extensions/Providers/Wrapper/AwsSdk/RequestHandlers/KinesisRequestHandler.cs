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
    internal static class KinesisRequestHandler
    {
        public const string VendorName = "Kinesis";
        public static readonly List<string> MessageBrokerRequestTypes = new List<string> { "GetRecordsRequest", "PutRecordsRequest", "PutRecordRequest" };
        private static ConcurrentDictionary<string, string> _operationNameCache = new();


        public static AfterWrappedMethodDelegate HandleKinesisRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, object request, bool isAsync, ArnBuilder builder)
        {
            var requestType = request.GetType().Name as string;

            // Only some Kinesis requests are treated as message queue operations (put records, get records).  The rest should be treated as ordinary spans.

            // Not all request types have a stream name or a stream ARN

            var streamName = KinesisHelper.GetStreamNameFromRequest(request);
            string arn = KinesisHelper.GetStreamArnFromRequest(request);
            if (arn == null && streamName != null)
            {
                arn = builder.Build("kinesis", $"stream/{streamName}");
            }

            var operation = _operationNameCache.GetOrAdd(requestType, requestType.Replace("Request", string.Empty).ToSnakeCase());

            ISegment segment;

            if (MessageBrokerRequestTypes.Contains(requestType))
            {
                var action = requestType.StartsWith("Put") ? MessageBrokerAction.Produce : MessageBrokerAction.Consume;

                segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, action, VendorName, destinationName: streamName ?? "Unknown", messagingSystemName: "aws_kinesis_data_streams", cloudAccountId: builder.AccountId, cloudRegion: builder.Region);
                segment.GetExperimentalApi().MakeLeaf();
            }
            else
            {
                var operationName = requestType.Replace("Request", "");

                if (streamName != null)
                {
                    operationName += "/" + streamName;
                }

                segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, "Kinesis", operationName, isLeaf: false);

            }

            if (arn != null)
            {
                segment.AddCloudSdkAttribute("cloud.resource_id", arn);
            }
            segment.AddCloudSdkAttribute("aws.operation", operation);
            segment.AddCloudSdkAttribute("aws.region", builder.Region);
            segment.AddCloudSdkAttribute("cloud.platform", "aws_kinesis_data_streams");

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

    }
}
