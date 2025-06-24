// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Helpers;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Agent.Extensions.SystemExtensions.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public class ServiceChannelProxyWrapper : IWrapper
    {
        private const string ServiceModelAssembly = "System.ServiceModel";
        private const string ServiceModelInternalsAssembly = "System.ServiceModel.Internals";
        private const string ServiceChannelProxyType = "System.ServiceModel.Channels.ServiceChannelProxy";
        private const string MethodDataType = "System.ServiceModel.Channels.ServiceChannelProxy+MethodData";
        private const string AsyncResultType = "System.Runtime.AsyncResult";
        private const string InvokeMethod = "Invoke";
        private const string WrapperName = "ServiceChannelProxyWrapper";

        public bool IsTransactionRequired => true;

        //Access to the channel to get the URI
        private Func<object, IServiceChannel> _getServiceChannel;
        private Func<object, IServiceChannel> GetServiceChannel() { return _getServiceChannel ?? (_getServiceChannel = VisibilityBypasser.Instance.GenerateFieldReadAccessor<IServiceChannel>(ServiceModelAssembly, ServiceChannelProxyType, "serviceChannel")); }

        //used to get the original AsyncCallback so that we can wrap it inside our own callback.
        private Func<object, AsyncCallback> _getAsyncCallbackReadAccessor;
        private Func<object, AsyncCallback> GetAsyncCallbackReadAccessor() { return _getAsyncCallbackReadAccessor ?? (_getAsyncCallbackReadAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<AsyncCallback>(ServiceModelInternalsAssembly, AsyncResultType, "callback")); }

        //Used to write our AsyncCallback wrapper back to the callback on the IAsyncResult.
        private Action<object, AsyncCallback> _setAsyncCallbackWriteAccessor;
        private Action<object, AsyncCallback> SetAsyncCallbackWriteAccessor() { return _setAsyncCallbackWriteAccessor ?? (_setAsyncCallbackWriteAccessor = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<AsyncCallback>(ServiceModelInternalsAssembly, AsyncResultType, "callback")); }

        //used to get the AsyncCallback exception so that we can log it.
        private Func<object, Exception> _getAsyncCallbackException;
        private Func<object, Exception> GetAsyncCallbackException() { return _getAsyncCallbackException ?? (_getAsyncCallbackException = VisibilityBypasser.Instance.GenerateFieldReadAccessor<Exception>(ServiceModelInternalsAssembly, AsyncResultType, "exception")); }

        // used to allow us to call System.ServiceModel.Channels.ServiceChannelProxy.GetMethodData to determine what type of call we are dealing with
        private Func<object, System.Runtime.Remoting.Messaging.IMethodCallMessage, object> _getMethodDataMethod;
        private Func<object, System.Runtime.Remoting.Messaging.IMethodCallMessage, object> GetMethodDataMethod() { return _getMethodDataMethod ?? (_getMethodDataMethod = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<System.Runtime.Remoting.Messaging.IMethodCallMessage, object>(ServiceModelAssembly, ServiceChannelProxyType, "GetMethodData")); }

        //Used as getter to get MethodType
        private static MethodInfo _methodTypeMethodInfo;

        private static object _serviceEnum;
        private static object _beginServiceEnum;
        private static object _taskServiceEnum;
        private static object _invocationLock = new object();
        private static List<string> _invocationsSent = new List<string>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var serviceChannelProxy = instrumentedMethodCall.MethodCall.InvocationTarget;
            var message = instrumentedMethodCall.MethodCall.MethodArguments[0] as System.Runtime.Remoting.Messaging.IMethodCallMessage;
            var methodData = GetMethodDataMethod()(serviceChannelProxy, message);
            if (_methodTypeMethodInfo == null)
            {
                _methodTypeMethodInfo = methodData.GetType().GetProperty("MethodType").GetMethod;
            }

            var methodType = _methodTypeMethodInfo.Invoke(methodData, null);
            if (_serviceEnum == null || _beginServiceEnum == null || _taskServiceEnum == null)
            {
                var type = methodType.GetType();
                _serviceEnum = Enum.Parse(type, "Service");
                _beginServiceEnum = Enum.Parse(type, "BeginService");
                _taskServiceEnum = Enum.Parse(type, "TaskService");
            }

            if (!methodType.Equals(_serviceEnum) && !methodType.Equals(_beginServiceEnum) && !methodType.Equals(_taskServiceEnum))
            {
                return Delegates.NoOp;
            }

            var transactionExperimental = transaction.GetExperimentalApi();

            var name = GetName(instrumentedMethodCall.MethodCall);
            var uri = GetUri(instrumentedMethodCall);
            var externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, name);
            var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
            segment.GetExperimentalApi()
                .SetSegmentData(externalSegmentData)
                .MakeLeaf();

            return Delegates.GetDelegateFor<System.Runtime.Remoting.Messaging.IMethodReturnMessage>(
                onSuccess: OnSuccess,
                onFailure: OnFailure
            );

            void OnSuccess(System.Runtime.Remoting.Messaging.IMethodReturnMessage methodReturnMessage)
            {
                if (methodType.Equals(_serviceEnum))
                {
                    if (methodReturnMessage.Exception != null)
                    {
                        HandleException(methodReturnMessage.Exception);
                        segment.End(methodReturnMessage.Exception);
                    }
                    else
                    {
                        segment.End();
                    }

                    TrySendInvocationMetric("Sync", agent);
                }
                else if (methodType.Equals(_taskServiceEnum))
                {
                    segment.RemoveSegmentFromCallStack();
                    transaction.Hold();
                    var task = (Task)methodReturnMessage.ReturnValue;
                    task.ContinueWith(ContinueWork, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.HideScheduler);

                    void ContinueWork(Task t)
                    {
                        var aggregateException = t.Exception as AggregateException;
                        var protocolException = aggregateException?.InnerExceptions.FirstOrDefault(IsProtocolException);
                        if (protocolException != null)
                        {
                            HandleException(protocolException);
                            segment.End(protocolException);
                        }
                        else if (t.Exception != null)
                        {
                            segment.End(t.Exception);
                        }
                        else
                        {
                            segment.End();
                        }

                        TrySendInvocationMetric("TAP", agent);
                        transaction.Release();
                    }
                }
                else if (methodType.Equals(_beginServiceEnum))
                {
                    segment.RemoveSegmentFromCallStack();
                    transaction.Hold();
                    var originalCallback = GetAsyncCallbackReadAccessor().Invoke(methodReturnMessage.ReturnValue);
                    SetAsyncCallbackWriteAccessor().Invoke(methodReturnMessage.ReturnValue, (AsyncCallback)WrappedAsyncCallback);

                    void WrappedAsyncCallback(IAsyncResult asyncResult)
                    {
                        var exception = GetAsyncCallbackException().Invoke(asyncResult);
                        if (exception != null)
                        {
                            HandleException(exception);
                            segment.End(exception);
                        }
                        else
                        {
                            segment.End();
                        }

                        TrySendInvocationMetric("APM", agent);
                        originalCallback?.Invoke(asyncResult);
                        transaction.Release();
                    }
                }
            }

            void OnFailure(Exception exception)
            {
                HandleException(exception);
                segment.End(exception);
            }

            void HandleException(Exception exception)
            {
                var protocolException = exception as ProtocolException;
                if (protocolException == null)
                {
                    var aggregateException = exception as AggregateException;
                    protocolException = aggregateException?.InnerExceptions.FirstOrDefault(IsProtocolException) as ProtocolException;
                }

                var webException = protocolException?.InnerException as WebException;
                if (webException != null)
                {
                    // This is needed because the message inspector which normally handles CAT/DT headers doesn't run if there is a protocol exception
                    var response = webException.Response;
                    var httpResponse = response as HttpWebResponse;

                    var statusCode = httpResponse?.StatusCode;
                    if (statusCode.HasValue)
                    {
                        externalSegmentData.SetHttpStatus((int)statusCode.Value);
                    }

                    transaction.ProcessInboundResponse(response?.Headers?.ToDictionary(), segment);
                }
            }
        }

        private static string GetName(MethodCall methodCall)
        {
            var methodCallMessage = methodCall.MethodArguments.ExtractAs<System.Runtime.Remoting.Messaging.IMethodCallMessage>(0);
            if (methodCallMessage == null)
            {
                throw new NullReferenceException("methodCallMessage");
            }

            var typeName = methodCallMessage.TypeName;
            if (typeName == null)
            {
                throw new NullReferenceException("typeName");
            }

            // The type name is the full class name followed by a comma and the assembly info.  We need to cut off at the comma.
            typeName = typeName.TrimAfterAChar(StringSeparators.CommaChar);
            var methodName = methodCallMessage.MethodName;
            if (methodName == null)
            {
                throw new NullReferenceException("methodName");
            }

            return $"{typeName}.{methodName}";
        }

        private Uri GetUri(InstrumentedMethodCall instrumentedMethodCall)
        {
            var serviceChannel = GetServiceChannel().Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
            return serviceChannel.RemoteAddress.Uri;
        }

        private static bool IsProtocolException(Exception e)
        {
            return e is ProtocolException;
        }

        private static void TrySendInvocationMetric(string style, IAgent agent)
        {
            var sendMetric = false;
            lock (_invocationLock)
            {
                if (!string.IsNullOrEmpty(style) && !_invocationsSent.Contains(style))
                {
                    _invocationsSent.Add(style);
                    sendMetric = true;
                }
            }

            if (sendMetric)
            {
                agent.GetExperimentalApi().RecordSupportabilityMetric($"WCFClient/InvocationStyle/{style}");
            }
        }
    }
}
