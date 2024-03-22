// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using NewRelic.Agent.Extensions.Lambda;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class HandlerMethodWrapper : IWrapper
    {
        public List<string> WebResponseHeaders = ["Content-Type", "Content-Length"];

        private static Func<object, object> _getRequestResponseFromGeneric;
        private static Func<object, string> _getFunctionNameFromLambdaContext;
        private static Func<object, string> _getFunctionVersionFromLambdaContext;
        private static Func<object, string> _getAwsRequestIdFromLambdaContext;
        private static Func<object, string> _getInvokedFunctionArnFromLambdaContext;


        public bool IsTransactionRequired => false;

        private static bool _coldStart = true;
        private static bool IsColdStart => _coldStart && !(_coldStart = false);

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var isAsync = instrumentedMethodCall.IsAsync;

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var lambdaContext = instrumentedMethodCall.MethodCall.MethodArguments[1]; // TODO handle case where this doesn't exist

            var functionNameGetter = _getFunctionNameFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionName");
            var functionVersionGetter = _getFunctionVersionFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionVersion");
            var requestIdGetter = _getAwsRequestIdFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "AwsRequestId");
            var functionArnGetter = _getInvokedFunctionArnFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "InvokedFunctionArn");

            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"input object fullname = {inputObject.GetType().FullName}");

            var eventType = inputObject.GetType().FullName.ToEventType();
            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"eventType = {eventType}");

            var lambdaFunctionName = functionNameGetter(lambdaContext);
            var lambdaFunctionArn = functionArnGetter(lambdaContext);
            var lambdaFunctionVersion = functionVersionGetter(lambdaContext);

            transaction = agent.CreateTransaction(
                isWeb: eventType.IsWebEvent(),
                category: "Lambda", // TODO: is this is correct/useful?
                transactionDisplayName: lambdaFunctionName,
                doNotTrackAsUnitOfWork: true);

            var attributes = new Dictionary<string, string>();

            attributes.AddEventSourceAttribute("eventType", eventType.ToEventTypeString());

            attributes.Add("aws.requestId", requestIdGetter(lambdaContext));
            attributes.Add("aws.lambda.arn", lambdaFunctionArn);

            if (IsColdStart) // only report this attribute if it's a cold start
                attributes.Add("aws.coldStart", "true");

            agent.SetServerlessParameters(lambdaFunctionVersion ?? "$LATEST", lambdaFunctionArn);

            LambdaEventHelpers.AddEventTypeAttributes(agent, transaction, eventType, inputObject, attributes);

            transaction.AddLambdaAttributes(attributes);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, lambdaFunctionName);

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    if (!ValidTaskResponse(responseTask) || (segment == null))
                    {
                        return;
                    }
                    var responseGetter = _getRequestResponseFromGeneric ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(responseTask.GetType(), "Result");
                    var response = responseGetter(responseTask);
                    CaptureResponseData(transaction, response);
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                        onSuccess: response =>
                        {
                            CaptureResponseData(transaction, response);

                            segment.End();
                            transaction.End();
                        },
                        onFailure: exception =>
                        {
                            segment.End(exception);
                            transaction.End();
                        });
            }
        }

        private void CaptureResponseData(ITransaction transaction, object response)
        {
            var responseTypeName = response.GetType().FullName;
            if (responseTypeName.EndsWith("APIGatewayProxyResponse") || responseTypeName.EndsWith("ApplicationLoadBalancerResponse"))
            {
                dynamic apiResponse = response;
                transaction.SetHttpResponseStatusCode(apiResponse.StatusCode); // StatusCode is a public property on both APIGatewayProxyResponse and ApplicationLoadBalancerResponse
                IDictionary<string, string> responseHeaders = apiResponse.Headers; // Headers is a public property of type IDictionary<string,string> on both types
                foreach (var header in WebResponseHeaders)
                {
                    if (responseHeaders.TryGetValue(header, out var value))
                    {
                        transaction.AddCustomAttribute(header, value);
                    }
                }
            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }

    }
}
