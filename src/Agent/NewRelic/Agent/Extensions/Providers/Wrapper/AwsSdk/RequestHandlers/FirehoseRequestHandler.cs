// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers
{
    internal static class FirehoseRequestHandler
    {
        public const string VendorName = "Firehose";
        private static ConcurrentDictionary<string, string> _operationNameCache = new();

        public static AfterWrappedMethodDelegate HandleFirehoseRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, ArnBuilder builder)
        {
            var requestType = request.GetType().Name as string;

            // Unlike Kinesis Data Streams, all Firehose requests are instrumented with ordinary method segments

            // Not all request types have a stream name or a stream ARN

            var streamName = KinesisHelper.GetDeliveryStreamNameFromRequest(request);
            string arn = KinesisHelper.GetDeliveryStreamArnFromRequest(request);
            if (arn == null && streamName != null)
            {
                //arn:aws:firehose:us-west-2:111111111111:deliverystream/FirehoseStreamName
                arn = builder.Build("firehose", $"deliverystream/{streamName}");
            }

            var operation = _operationNameCache.GetOrAdd(requestType, requestType.Replace("Request", string.Empty).ToSnakeCase());

            ISegment segment;

            var operationName = requestType.Replace("Request", "");

            if (streamName != null)
            {
                operationName += "/" + streamName;
            }

            segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, VendorName, operationName, isLeaf: false);


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


    }
}
