// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Linq;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelineWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "AwsSdkPipelineWrapper";
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _getRequestResponseFromGeneric = new();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // Get the IExecutionContext (the only parameter)
            dynamic executionContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

            var isAsync = instrumentedMethodCall.IsAsync ||
                          instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName == "InvokeAsync";

            if (isAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            // Get the IRequestContext
            if (executionContext.RequestContext == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: RequestContext is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic requestContext = executionContext.RequestContext;

            // Get the AmazonWebServiceRequest being invoked. The name will tell us the type of request
            if (requestContext.OriginalRequest == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic request = requestContext.OriginalRequest;
            string requestType = request.GetType().Name;

            agent.Logger.Finest("AwsSdkPipelineWrapper: Request type is " + requestType);

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
                    agent.Logger.Finest($"AwsSdkPipelineWrapper: Request type {requestType} is not supported. Returning NoOp delegate.");
                    return Delegates.NoOp;
            }

            var dtHeaders = agent.GetConfiguredDTHeaders();

            string requestQueueUrl = request.QueueUrl;
            ISegment segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, action);
            if (action == MessageBrokerAction.Produce)
            {
                if (requestType == "SendMessageRequest")
                {
                    if (request.MessageAttributes == null)
                    {
                        agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest.MessageAttributes is null, unable to insert distributed trace headers.");
                    }
                    else
                    {
                        SqsHelper.InsertDistributedTraceHeaders(transaction, request, dtHeaders.Count);
                    }
                }
                else if (requestType == "SendMessageBatchRequest")
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
            }

            // modify the request to ask for DT headers in the response message attributes.
            if (action == MessageBrokerAction.Consume)
            {
                if (request.MessageAttributeNames == null)
                    request.MessageAttributeNames = new List<string>();

                foreach(var header in dtHeaders)
                    request.MessageAttributeNames.Add(header);
            }

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, ProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

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

            return Delegates.GetDelegateFor(
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
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }
    }
}
