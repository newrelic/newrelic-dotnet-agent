// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelineWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "AwsSdkPipelineWrapper";
        private static ConcurrentHashSet<string> _unsupportedRequestTypes = new();

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
            string requestType = request.GetType().FullName;

            if (requestType.StartsWith("Amazon.SQS"))
            {
                return SQSRequestHandler.HandleSQSRequest(instrumentedMethodCall, agent, transaction, request, isAsync, executionContext);
            }
            else if (requestType.StartsWith("Amazon.DynamoDBv2"))
            {
                return DynamoDbRequestHandler.HandleDynamoDbRequest(instrumentedMethodCall, agent, transaction, request, isAsync, executionContext);
            }

            if (!_unsupportedRequestTypes.Contains(requestType))  // log once per unsupported request type
            {                
                agent.Logger.Debug($"AwsSdkPipelineWrapper: Unsupported request type: {requestType}. Returning NoOp delegate.");
                _unsupportedRequestTypes.Add(requestType);
            }

            return Delegates.NoOp;
        }
    }
}
