// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelineWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "AwsSdkPipelineWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // Get the IExecutionContext (the only parameter)
            dynamic executionContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

            // Get the IRequestContext
            if (executionContext.RequestContext == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: RequestContext is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic requestContext = executionContext.RequestContext;

            if (requestContext.ServiceMetaData == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.ServiceMetaData is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic metadata = requestContext.ServiceMetaData;
            string requestId = metadata.ServiceId; // SQS?

            // Get the AmazonWebServiceRequest being invoked. The name will tell us the type of request
            if (requestContext.OriginalRequest == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.OriginalRequest is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic request = requestContext.OriginalRequest;
            string requestType = request.GetType().Name;

            MessageBrokerAction action;
            var insertDistributedTraceHeaders = false;
            switch (requestType)
            {
                case "SendMessageRequest":
                case "SendMessageBatchRequest":
                    action = MessageBrokerAction.Produce;
                    insertDistributedTraceHeaders = true;
                    break;
                case "ReceiveMessageRequest":
                    action = MessageBrokerAction.Consume;
                    break;
                case "PurgeQueueRequest":
                    action = MessageBrokerAction.Purge;
                    break;
                default:
                    agent.Logger.Debug($"AwsSdkPipelineWrapper: Request type {requestType} is not supported. Returning NoOp delegate.");
                    return Delegates.NoOp;
            }

            string requestQueueUrl = request.QueueUrl;
            ISegment segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, action);
            if (insertDistributedTraceHeaders)
            {
                // This needs to happen at the end
                if (requestContext.Request == null)
                    agent.Logger.Finest("AwsSdkPipelineWrapper: requestContext.Request is null, unable to insert distributed trace headers.");
                else
                {
                    dynamic webRequest = requestContext.Request;
                    SqsHelper.InsertDistributedTraceHeaders(transaction, webRequest);
                }
            }

            return Delegates.GetDelegateFor(segment);
        }
    }
}
