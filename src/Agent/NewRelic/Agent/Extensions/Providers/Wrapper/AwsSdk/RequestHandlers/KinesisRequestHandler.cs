// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers
{
    internal static class KinesisRequestHandler
    {
        public const string VendorName = "Kinesis";
        public static readonly List<string> MessageBrokerRequestTypes = new List<string> { "GetRecordsRequest", "PutRecordsRequest", "PutRecordRequest" };
        private static readonly ConcurrentHashSet<string> _unsupportedKinesisRequestTypes = [];


        // These lists are incomplete, don't want to throw them away yet though
        //public static readonly List<string> RequestTypesWithARN =
        //    new List<string> { "AddTagsToStreamRequest", "DecreaseStreamRetensionPeriodRequest", "DeleteResourcePolicyRequest",
        //                       "DeleteStreamRequest", "DeregisterStreamConsumerRequest", "DescribeStreamConsumerRequest",
        //                       "DescribeStreamRequest", "DescribeStreamSummaryRequest", "DisableEnhancedMonitoringRequest",
        //                       "EnableEnhancedMonitoringRequest", "GetResourcePolicyRequest", "GetShardIteratorRequest"};
        //public static readonly List<string> RequestTypesWithStreamName =
        //    new List<string> { "CreateStreamRequest", "DecreaseStreamRetensionPeriodRequest", "DeleteStreamRequest", "DescribeStreamRequest",
        //                       "DescribeStreamSummaryRequest", "DisableEnhancedMonitoringRequest", "EnableEnhancedMonitoringRequest",
        //                       "GetShardIteratorRequest"};

        private static readonly ConcurrentDictionary<Type, Func<object, object>> _getRequestResponseFromGeneric = new();
        //private static readonly ConcurrentHashSet<string> _unsupportedSQSRequestTypes = [];

        public static AfterWrappedMethodDelegate HandleKinesisRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, dynamic executionContext)
        {
            var requestType = request.GetType().Name as string;

            // Only some Kinesis requests are treated as message queue operations (put records, get records).  The rest should be treated as ordinary external spans.

            // Not all request types have a stream name or a stream ARN
            ISegment segment;

            if (MessageBrokerRequestTypes.Contains(requestType))
            {
                var action = requestType.StartsWith("Put") ? MessageBrokerAction.Produce : MessageBrokerAction.Consume;
                var streamARN = request.StreamARN as string;
                // arn:aws:kinesis:us-west-2:342444490463:stream/AlexKinesisTesting
                var arnParts = streamARN.Split(':');

                var region = arnParts[3];
                var accountId = arnParts[4];
                var streamName = arnParts[arnParts.Length - 1];

                segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, action, VendorName, destinationName: streamName, messagingSystemName: "aws_kinesis_data_streams", cloudAccountId: accountId, cloudRegion: region);
                segment.GetExperimentalApi().MakeLeaf();
            }
            else
            {
                var operationName = "Kinesis/" + requestType.Replace("Request", "");
                var arn = string.Empty;
                // TODO: add stream name to end if it exists, e.g. "Kinesis/CreateStream/myStreamName"
                if (request.GetType().GetProperty("StreamName") != null)
                {
                    operationName += "/" + request.StreamName as string;
                }
                if (request.GetType().GetProperty("StreamARN") != null)
                {
                    arn = request.StreamARN as string;
                }
                if (request.GetType().GetProperty("ResourceARN") != null)
                {
                    arn = request.ResourceARN as string;
                }

                if (arn != string.Empty)
                {
                    // URI is not available in any of the request types
                    // Options: use ARN for URI (only works if the request type has an ARN)
                    //          assume that the URI fits the pattern of "kinesis.$REGION.amazonaws.com" (need a region, seems fake)

                    segment = transaction.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, new Uri(arn), operationName, isLeaf: true);
                }
                else
                {
                    if (!_unsupportedKinesisRequestTypes.Contains(requestType))  // log once per unsupported request type
                    {
                        agent.Logger.Debug($"AwsSdkPipelineWrapper: Kinesis Request type {requestType} is not supported. Returning NoOp delegate.");
                        _unsupportedKinesisRequestTypes.Add(requestType);
                    }

                    return Delegates.NoOp;

                }


            }

            // DT stuff TODO figure this out

            //ar dtHeaders = agent.GetConfiguredDTHeaders();

            //switch (action)
            //{
            //    case MessageBrokerAction.Produce when requestType == "SendMessageRequest":
            //        {
            //            if (request.MessageAttributes == null)
            //            {
            //                agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest.MessageAttributes is null, unable to insert distributed trace headers.");
            //            }
            //            else
            //            {
            //                SqsHelper.InsertDistributedTraceHeaders(transaction, request, dtHeaders.Count);
            //            }

            //            break;
            //        }
            //    case MessageBrokerAction.Produce:
            //        {
            //            if (requestType == "SendMessageBatchRequest")
            //            {
            //                // loop through each message in the batch and insert distributed trace headers
            //                foreach (var message in request.Entries)
            //                {
            //                    if (message.MessageAttributes == null)
            //                    {
            //                        agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest.Entries.MessageAttributes is null, unable to insert distributed trace headers.");
            //                    }
            //                    else
            //                    {
            //                        SqsHelper.InsertDistributedTraceHeaders(transaction, message, dtHeaders.Count);
            //                    }
            //                }
            //            }

            //            break;
            //        }

            //    // modify the request to ask for DT headers in the response message attributes.
            //    case MessageBrokerAction.Consume:
            //        {
            //            // create a new list or clone the existing one so we don't modify the original list
            //            request.MessageAttributeNames = request.MessageAttributeNames == null ? new List<string>() : new List<string>(request.MessageAttributeNames);

            //            foreach (var header in dtHeaders)
            //                request.MessageAttributeNames.Add(header);

            //            break;
            //        }
            //}

            return isAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, ProcessResponse, TaskContinuationOptions.ExecuteSynchronously)
                :
                Delegates.GetDelegateFor(
                    onComplete: segment.End,
                    onSuccess: () =>
                    {
                        //if (action != MessageBrokerAction.Consume)
                        //    return;

                        //var ec = executionContext;
                        //var response = ec.ResponseContext.Response; // response is a ReceiveMessageResponse

                        //AcceptTracingHeadersIfSafe(transaction, response);
                    }
                );

            void ProcessResponse(Task responseTask)
            {
                //if (!ValidTaskResponse(responseTask) || segment == null || action != MessageBrokerAction.Consume)
                //    return;

                //// taskResult is a ReceiveMessageResponse
                //var taskResultGetter = _getRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
                //dynamic response = taskResultGetter(responseTask);

                //AcceptTracingHeadersIfSafe(transaction, response);

            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }

        private static void AcceptTracingHeadersIfSafe(ITransaction transaction, dynamic response)
        {
            if (response.Messages != null && response.Messages.Count > 0 && response.Messages[0].MessageAttributes != null)
            {
                // accept distributed trace headers from the first message in the response
                SqsHelper.AcceptDistributedTraceHeaders(transaction, response.Messages[0].MessageAttributes);
            }
        }

    }
}
