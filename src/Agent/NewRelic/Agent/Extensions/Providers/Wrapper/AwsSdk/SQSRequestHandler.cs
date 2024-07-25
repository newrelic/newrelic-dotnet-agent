// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    internal static class SQSRequestHandler
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _getRequestResponseFromGeneric = new();
        private static readonly HashSet<string> _unsupportedSQSRequestTypes = [];

        public static AfterWrappedMethodDelegate HandleSQSRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, dynamic executionContext)
        {
            var requestType = request.GetType().Name;

            MessageBrokerAction action;
            switch (requestType)
            {
                case "SendMessageRequest":
                case "SendMessageBatchRequest":
                    action = MessageBrokerAction.Produce;
                    break;
                case "ReceiveMessageRequest":
                    action = MessageBrokerAction.Consume;
                    break;
                case "PurgeQueueRequest":
                    action = MessageBrokerAction.Purge;
                    break;
                default:
                    if (_unsupportedSQSRequestTypes.Add(requestType)) // log once per unsupported request type
                        agent.Logger.Debug($"AwsSdkPipelineWrapper: SQS Request type {requestType} is not supported. Returning NoOp delegate.");

                    return Delegates.NoOp;
            }

            var dtHeaders = agent.GetConfiguredDTHeaders();

            string requestQueueUrl = request.QueueUrl;
            ISegment segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, action);
            switch (action)
            {
                case MessageBrokerAction.Produce when requestType == "SendMessageRequest":
                    {
                        if (request.MessageAttributes == null)
                        {
                            agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest.MessageAttributes is null, unable to insert distributed trace headers.");
                        }
                        else
                        {
                            SqsHelper.InsertDistributedTraceHeaders(transaction, request, dtHeaders.Count);
                        }

                        break;
                    }
                case MessageBrokerAction.Produce:
                    {
                        if (requestType == "SendMessageBatchRequest")
                        {
                            // loop through each message in the batch and insert distributed trace headers
                            foreach (var message in request.Entries)
                            {
                                if (message.MessageAttributes == null)
                                {
                                    agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest.Entries.MessageAttributes is null, unable to insert distributed trace headers.");
                                }
                                else
                                {
                                    SqsHelper.InsertDistributedTraceHeaders(transaction, message, dtHeaders.Count);
                                }
                            }
                        }

                        break;
                    }

                // modify the request to ask for DT headers in the response message attributes.
                case MessageBrokerAction.Consume:
                    {
                        // create a new list or clone the existing one so we don't modify the original list
                        request.MessageAttributeNames = request.MessageAttributeNames == null ? new List<string>() : new List<string>(request.MessageAttributeNames);

                        foreach (var header in dtHeaders)
                            request.MessageAttributeNames.Add(header);

                        break;
                    }
            }

            return isAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, ProcessResponse, TaskContinuationOptions.ExecuteSynchronously)
                :
                Delegates.GetDelegateFor(
                    onComplete: segment.End,
                    onSuccess: () =>
                    {
                        if (action != MessageBrokerAction.Consume)
                            return;

                        var ec = executionContext;
                        var response = ec.ResponseContext.Response; // response is a ReceiveMessageResponse

                        // accept distributed trace headers from the first message in the response
                        SqsHelper.AcceptDistributedTraceHeaders(transaction, response.Messages[0].MessageAttributes);
                    }
                );

            void ProcessResponse(Task responseTask)
            {
                if (!ValidTaskResponse(responseTask) || (segment == null) || action != MessageBrokerAction.Consume)
                    return;

                // taskResult is a ReceiveMessageResponse
                var taskResultGetter = _getRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
                dynamic receiveMessageResponse = taskResultGetter(responseTask);

                // accept distributed trace headers from the first message in the response
                SqsHelper.AcceptDistributedTraceHeaders(transaction, receiveMessageResponse.Messages[0].MessageAttributes);
            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }

    }
}
