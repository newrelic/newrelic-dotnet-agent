// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using NewRelic.Agent.Extensions.Lambda;
using NewRelic.Reflection;
using NewRelic.Collections;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class HandlerMethodWrapper : IWrapper
    {
        private class FunctionDetails
        {
            public string FunctionName { get; private set; }
            public string FunctionVersion { get; private set; }
            public string Arn { get; private set; }
            private int ContextIdx = -1;
            private int InputIdx = -1;
            private Func<object, string> _requestIdGetter;
            public AwsLambdaEventType EventType { get; private set; } = AwsLambdaEventType.Unknown;

            public bool HasContext() => ContextIdx != -1;
            public bool HasInputObject() => InputIdx != -1;

            public void SetContext(object lambdaContext, int contextIdx)
            {
                ContextIdx = contextIdx;
                SetName(lambdaContext);
                SetVersion(lambdaContext);
                SetArn(lambdaContext);
                SetRequestIdGetter(lambdaContext);
            }

            public bool SetEventType(string fullName, int idx)
            {
                var eventType = fullName.ToEventType();
                if (eventType != AwsLambdaEventType.Unknown)
                {
                    InputIdx = idx;
                    EventType = eventType;
                    return true;
                }
                return false;
            }

            public void Validate(string fallbackName)
            {
                ValidateName(fallbackName);
                ValidateVersion();
            }

            private void SetName(object lambdaContext)
            {
                var functionNameGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionName");
                FunctionName = functionNameGetter(lambdaContext);
            }

            private void ValidateName(string fallbackName)
            {
                if (string.IsNullOrEmpty(_functionDetails.FunctionName))
                {
                    FunctionName = System.Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") ?? fallbackName;
                }
            }

            private void SetVersion(object lambdaContext)
            {
                var functionVersionGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionVersion");
                FunctionVersion = functionVersionGetter(lambdaContext);
            }

            private void ValidateVersion()
            {
                if (string.IsNullOrEmpty(FunctionVersion))
                {
                    FunctionVersion = System.Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION") ?? "$LATEST";
                }
            }

            private void SetArn(object lambdaContext)
            {
                var functionArnGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "InvokedFunctionArn");
                Arn = functionArnGetter(lambdaContext);
            }

            private void SetRequestIdGetter(object lambdaContext)
            {
                _requestIdGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "AwsRequestId");
            }

            public string GetRequestId(InstrumentedMethodCall instrumentedMethodCall)
            {
                if (HasContext() && (ContextIdx < instrumentedMethodCall.MethodCall.MethodArguments.Length))
                {
                    return _requestIdGetter(instrumentedMethodCall.MethodCall.MethodArguments[ContextIdx]);
                }
                return null;
            }

            public object GetInputObject(InstrumentedMethodCall instrumentedMethodCall)
            {
                if (HasInputObject() && (InputIdx < instrumentedMethodCall.MethodCall.MethodArguments.Length))
                {
                    return instrumentedMethodCall.MethodCall.MethodArguments[InputIdx];
                }
                return null;
            }

            public bool IsWebRequest => EventType is AwsLambdaEventType.APIGatewayProxyRequest or AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest or AwsLambdaEventType.ApplicationLoadBalancerRequest;
        }

        private List<string> _webResponseHeaders = ["content-type", "content-length"];

        private static Func<object, object> _getRequestResponseFromGeneric;
        private static object _initLock = new object();
        private static FunctionDetails _functionDetails = null;

        public bool IsTransactionRequired => false;

        private static bool _coldStart = true;
        private ConcurrentHashSet<string> _unexpectedResponseTypes = new();
        private ConcurrentHashSet<string> _unsupportedInputTypes = new();

        private static bool IsColdStart => _coldStart && !(_coldStart = false);

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod".Equals(methodInfo.RequestedWrapperName));
        }

        private void InitLambdaData(InstrumentedMethodCall instrumentedMethodCall, IAgent agent)
        {
            _functionDetails = new FunctionDetails();

            for (int idx = 0; idx < instrumentedMethodCall.MethodCall.MethodArguments.Length; idx++)
            {
                var arg = instrumentedMethodCall.MethodCall.MethodArguments[idx];

                if (!_functionDetails.HasContext())
                {
                    var iLambdaContext = arg.GetType().GetInterface("ILambdaContext");
                    if (iLambdaContext != null)
                    {
                        _functionDetails.SetContext(arg, idx);
                        continue; // go to the next arg
                    }
                }

                string name = arg.GetType().FullName;
                agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Checking parameter: {name}");
                if (_functionDetails.SetEventType(name, idx))
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Supported Event Type found: {_functionDetails.EventType}");
                }
                else if (!_unsupportedInputTypes.Contains(name))
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Unsupported input object type: {name}. Unable to provide additional instrumentation.");
                    _unsupportedInputTypes.Add(name);
                }
            }

            _functionDetails.Validate(instrumentedMethodCall.MethodCall.Method.MethodName);

            if (!_functionDetails.HasContext())
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"No Lambda context information found");
            }

            agent.SetServerlessParameters(_functionDetails.FunctionVersion, _functionDetails.Arn);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (_functionDetails == null)
            {
                lock (_initLock)
                {
                    if (_functionDetails == null)
                    {
                        try
                        {
                            InitLambdaData(instrumentedMethodCall, agent);
                        }
                        catch (Exception ex)
                        {
                            agent.Logger.Log(Agent.Extensions.Logging.Level.Error, $"Could not initialize lambda data: {ex.Message}");
                        }
                    }
                }
            }

            var isAsync = instrumentedMethodCall.IsAsync;
            string requestId = _functionDetails!.GetRequestId(instrumentedMethodCall);
            var inputObject = _functionDetails.GetInputObject(instrumentedMethodCall);

            transaction = agent.CreateTransaction(
                isWeb: _functionDetails.EventType.IsWebEvent(),
                category: "Lambda",
                transactionDisplayName: _functionDetails.FunctionName,
                doNotTrackAsUnitOfWork: true);

            if (isAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            if (_functionDetails.EventType != AwsLambdaEventType.Unknown)
            {
                transaction.AddEventSourceAttribute("eventType", _functionDetails.EventType.ToEventTypeString());
            }

            if (requestId != null)
            {
                transaction.AddLambdaAttribute("aws.requestId", requestId);
            }
            if (_functionDetails.Arn != null)
            {
                transaction.AddLambdaAttribute("aws.lambda.arn", _functionDetails.Arn);
            }

            if (IsColdStart) // only report this attribute if it's a cold start
                transaction.AddLambdaAttribute("aws.lambda.coldStart", "true");

            LambdaEventHelpers.AddEventTypeAttributes(agent, transaction, _functionDetails.EventType, inputObject);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, _functionDetails.FunctionName);

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    try
                    {

                        if (responseTask.Status == TaskStatus.Faulted)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        if (!ValidTaskResponse(responseTask) || (segment == null))
                        {
                            return;
                        }

                        // capture response data for specific request / response types
                        if (_functionDetails.IsWebRequest)
                        {
                            var responseGetter = _getRequestResponseFromGeneric ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(responseTask.GetType(), "Result");
                            var response = responseGetter(responseTask);
                            CaptureResponseData(transaction, response, agent);
                        }
                    }
                    finally
                    {
                        segment?.End();
                        transaction.End();
                    }
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                        onSuccess: response =>
                        {
                            if (_functionDetails.IsWebRequest)
                                CaptureResponseData(transaction, response, agent);

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

        private void CaptureResponseData(ITransaction transaction, object response, IAgent agent)
        {
            if (response == null)
                return;

            // check response type based on request type to be sure it has the properties we're looking for 
            var responseType = response.GetType().FullName;
            if ((_functionDetails.EventType == AwsLambdaEventType.APIGatewayProxyRequest && responseType != "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse")
                ||
                (_functionDetails.EventType == AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest && responseType != "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse")
                ||
                (_functionDetails.EventType == AwsLambdaEventType.ApplicationLoadBalancerRequest && responseType != "Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse"))
            {
                if (!_unexpectedResponseTypes.Contains(responseType))
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Unexpected response type {responseType} for request event type {_functionDetails.EventType}. Not capturing any response data.");
                    _unexpectedResponseTypes.Add(responseType);
                }

                return;
            }

            dynamic webResponse = response;
            transaction.SetHttpResponseStatusCode(webResponse.StatusCode);

            IDictionary<string, string> responseHeaders = webResponse.Headers;
            if (webResponse.Headers != null)
            {
                // copy and lowercase the headers
                Dictionary<string, string> copiedHeaders = new Dictionary<string, string>();
                foreach(var kvp in responseHeaders)
                    copiedHeaders.Add(kvp.Key.ToLower(), kvp.Value);

                foreach (var header in _webResponseHeaders) // only capture specific headers
                {
                    if (copiedHeaders.TryGetValue(header, out var value))
                    {
                        transaction.AddLambdaAttribute($"response.headers.{header.ToLower()}", value);
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
