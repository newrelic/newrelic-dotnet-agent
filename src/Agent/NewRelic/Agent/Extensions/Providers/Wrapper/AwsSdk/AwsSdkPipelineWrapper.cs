// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelinekWrapper : IWrapper
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
            dynamic requestContext = executionContext.RequestContext;

            dynamic metadata = requestContext.ServiceMetaData;
            string requestId = metadata.ServiceId; // SQS?

            // Get the AmazonWebServiceRequest being invoked. The name will tell us the type of request
            dynamic request = requestContext.OriginalRequest;
            string requestType = request.GetType().Name;

            // Get the web request object (IRequest). This can be used to get the headers
            dynamic webRequest = requestContext.Request;

            ISegment segment;

            switch (requestType)
            {
                case "SendMessageRequest":
                case "SendMessageBatchRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, request.QueueUrl, MessageBrokerAction.Produce);
                    // This needs to happen at the end
                    //SqsHelper.InsertDistributedTraceHeaders(transaction, webRequest);
                    break;
                case "ReceiveMessageRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, request.QueueUrl, MessageBrokerAction.Consume);
                    break;
                case "PurgeQueueRequest":
                    segment = SqsHelper.GenerateSegment(transaction, instrumentedMethodCall.MethodCall, request.QueueUrl, MessageBrokerAction.Purge);
                    break;
                default:
                    return Delegates.NoOp;
            }

            return Delegates.GetDelegateFor(segment);
        }
    }
}
