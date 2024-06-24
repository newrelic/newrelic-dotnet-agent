// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
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
            string requestQueueUrl = request.QueueUrl;

            // Get the web request object (IRequest). This can be used to get the headers
            if (requestContext.Request == null)
            {
                agent.Logger.Debug("AwsSdkPipelineWrapper: requestContext.Request is null. Returning NoOp delegate.");
                return Delegates.NoOp;
            }
            dynamic webRequest = requestContext.Request;

            ISegment segment;

            switch (requestType)
            {
                case "SendMessageRequest":
                case "SendMessageBatchRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, MessageBrokerAction.Produce);
                    // This needs to happen at the end
                    //SqsHelper.InsertDistributedTraceHeaders(transaction, webRequest);
                    break;
                case "ReceiveMessageRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, MessageBrokerAction.Consume);
                    break;
                case "PurgeQueueRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, requestQueueUrl, MessageBrokerAction.Purge);
                    break;
                default:
                    agent.Logger.Debug($"AwsSdkPipelineWrapper: Request type {requestType} is not supported. Returning NoOp delegate.");
                    return Delegates.NoOp;
            }

            return Delegates.GetDelegateFor(segment);
        }
    }
}
