// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelineWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;
        private string _accountId = null;

        private const string WrapperName = "AwsSdkPipelineWrapper";
        private static HashSet<string> _unsupportedRequestTypes = new();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        private string GetRegion(IAgent agent, dynamic requestContext)
        {
            try
            {
                var clientconfig = requestContext.ClientConfig;
                var regionEndpoint = clientconfig.RegionEndpoint;
                var systemName = regionEndpoint.SystemName;
                return systemName;
            }
            catch (Exception e)
            {
                agent.Logger.Debug(e, $"AwsSdkPipelineWrapper: Unable to get region from requestContext.");
            }

            return "";
        }

        private string GetAccountId(IAgent agent)
        {
            if (_accountId != null)
            {
                return _accountId;
            }
            _accountId = agent.Configuration.AwsAccountId;
            if (_accountId == null)
            {
                _accountId = "";
            }
            else if ((_accountId.Length != 12) || _accountId.Any(c => (c < '0') || (c > '9')))
            {
                agent.Logger.Warn("Supplied AWS Account Id appears to be invalid: {0}", _accountId);
            }
            return _accountId;
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
            string accountId = agent.Configuration.AwsAccountId;

            if (requestType.StartsWith("Amazon.SQS"))
            {
                return SQSRequestHandler.HandleSQSRequest(instrumentedMethodCall, agent, transaction, request, isAsync, executionContext);
            }
            else if (requestType == "Amazon.Lambda.Model.InvokeRequest")
            {
                return LambdaInvokeRequestHandler.HandleInvokeRequest(instrumentedMethodCall, agent, transaction, request, isAsync, GetRegion(agent, requestContext), accountId);
            }

            if (_unsupportedRequestTypes.Add(requestType)) // log once per unsupported request type
                agent.Logger.Debug($"AwsSdkPipelineWrapper: Unsupported request type: {requestType}. Returning NoOp delegate.");

            return Delegates.NoOp;
        }
    }
}
