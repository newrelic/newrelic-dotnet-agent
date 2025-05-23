// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Caching;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AwsSdkPipelineWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "AwsSdkPipelineWrapper";
        private static ConcurrentHashSet<string> _unsupportedRequestTypes = new();
        private static bool _reportBadAccountId = true;
        private static bool _reportBadArnBuilder = false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        private ArnBuilder CreateArnBuilder(IAgent agent, dynamic requestContext)
        {
            string partition = null;
            string systemName = null;
            string accountId = null;
            try
            {
                var clientConfig = requestContext.ClientConfig;
                accountId = GetAccountId(agent, clientConfig);
                if (clientConfig.RegionEndpoint != null)
                {
                    var regionEndpoint = clientConfig.RegionEndpoint;
                    systemName = regionEndpoint.SystemName;
                    partition = regionEndpoint.PartitionName;
                }
            }
            catch (Exception e)
            {
                if (_reportBadArnBuilder)
                {
                    agent.Logger.Debug(e, $"AwsSdkPipelineWrapper: Unable to get required ARN components from requestContext.");
                    _reportBadArnBuilder = false;
                }
            }

            return new ArnBuilder(partition, systemName, accountId);
        }

        private string GetAccountId(IAgent agent, object clientConfig)
        {
            var cacheKey = new WeakReferenceKey<object>(clientConfig);
            string accountId = AmazonServiceClientWrapper.AwsAccountIdByClientConfigCache.ContainsKey(cacheKey) ? AmazonServiceClientWrapper.AwsAccountIdByClientConfigCache.Get(cacheKey) : agent.Configuration.AwsAccountId;

            if (accountId != null)
            {
                if ((accountId.Length != 12) || accountId.Any(c => (c < '0') || (c > '9')))
                {
                    if (_reportBadAccountId)
                    {
                        agent.Logger.Warn("Supplied AWS Account ID appears to be invalid");
                        _reportBadAccountId = false;
                    }
                }
            }

            return accountId;
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
            ArnBuilder builder = CreateArnBuilder(agent, requestContext);

            if (requestType.StartsWith("Amazon.SQS"))
            {
                return SQSRequestHandler.HandleSQSRequest(instrumentedMethodCall, agent, transaction, request, isAsync, executionContext);
            }

            if (requestType == "Amazon.Lambda.Model.InvokeRequest")
            {
                return LambdaInvokeRequestHandler.HandleInvokeRequest(instrumentedMethodCall, agent, transaction, request, isAsync, builder);
            }

            if (requestType.StartsWith("Amazon.DynamoDBv2"))
            {
                return DynamoDbRequestHandler.HandleDynamoDbRequest(instrumentedMethodCall, agent, transaction, request, isAsync, builder);
            }

            if (requestType.StartsWith("Amazon.KinesisFirehose"))
            {
                return FirehoseRequestHandler.HandleFirehoseRequest(instrumentedMethodCall, agent, transaction, request, isAsync, builder);
            }

            if (requestType.StartsWith("Amazon.Kinesis."))
            {
                return KinesisRequestHandler.HandleKinesisRequest(instrumentedMethodCall, agent, transaction, request, isAsync, builder);
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
