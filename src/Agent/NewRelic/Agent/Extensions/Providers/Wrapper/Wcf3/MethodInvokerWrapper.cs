// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.SystemExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace NewRelic.Providers.Wrapper.Wcf3
{

    /// <summary>
    /// The table below illustrates the various invocation types, the methods that are instrumented, and which aspects of
    /// the transaction are accomplished during instrumentation.
    ///
    /// Invocation    Instrumented        Start        Inbound      Start       End         End       Outbound
    /// Method        Method              Trx          CAT/DT       Segment     Seg         Trx       CAT/DT
    ///---------------------------------------------------------------------------------------------------------------------------
    /// Sync          Invoke              Yes*         Yes*         Yes         Yes         Yes       Yes            Segment #1
    ///
    /// Begin/End     InvokeBegin         Yes*         Yes*         Yes         Yes         Error     Error          Segment #1
    ///               InvokeEnd           No           No           Yes         Yes         Yes       Yes            Segment #2
    ///
    /// TAP Async     InvokeAsync         Yes*         Yes*         Yes         Yes         No        No             Segment #1
    ///               EndInvoke           No           No           No          No          Yes       Yes            n/a
    /// 
    /// Yes*     =   Perform action if not already performed by an upstream wrapper
    /// Error    =   Perform in the case of an errror, but otherwise not
    /// </summary>
    public class MethodInvokerWrapper : IWrapper
    {
        private static readonly object _wrapperToken = new object();

        // these must be lazily instatiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
        private static class Statics
        {
            //Supporting synchronous invoke
            public static readonly Func<object, MethodInfo> GetSyncMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, SyncTypeName, "method");

            //Supporting BeginInvoke/EndInvoke async Style
            public static Func<object, MethodInfo> GetAsyncBeginMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "beginMethod");
            public static Func<object, MethodInfo> GetAsyncEndMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "endMethod");

            //Supporting Task based Async style
            public static Func<object, MethodInfo> GetTAPAsyncTaskMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, TAPTypeName, "taskMethod");
        }

        private const string AssemblyName = "System.ServiceModel";
        private const string SyncTypeName = "System.ServiceModel.Dispatcher.SyncMethodInvoker";
        private const string AsyncTypeName = "System.ServiceModel.Dispatcher.AsyncMethodInvoker";
        private const string TAPTypeName = "System.ServiceModel.Dispatcher.TaskMethodInvoker";
        private const string TAPTypeNameShort = "TaskMethodInvoker";

        private const string SyncMethodName = "Invoke";
        private const string InvokeBeginMethodName = "InvokeBegin";
        private const string InvokeEndMethodName = "InvokeEnd";
        private const string InvokeAsyncMethodName = "InvokeAsync";
        private const string WrapperName = "MethodInvokerWrapper";
        /// <summary>
        /// Translates the method name to the type of invocation
        /// </summary>
        private readonly Dictionary<string, string> _methodNameInvocationTypesDic = new Dictionary<string, string>()
        {
            { SyncMethodName, "Sync" },
            { InvokeBeginMethodName, "APM" },
            { InvokeAsyncMethodName, "TAP" }
        };

        private readonly string[] _methodNamesStart = new[] { SyncMethodName, InvokeBeginMethodName, InvokeAsyncMethodName };
        private readonly string[] _methodNamesEndTrx = new[] { SyncMethodName, InvokeEndMethodName };

        private readonly List<string> _rptSupMetric_InvocType = new List<string>();
        private readonly object _rptSupMetric_InvocType_Lock = new object();

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        private static IEnumerable<string> ExtractHeaderValue(OperationContext context, string key)
        {
            string headerValue = null;

            if(context.IncomingMessageProperties != null && context.IncomingMessageProperties.TryGetValue(HttpRequestMessageProperty.Name, out var httpRequestMessageAsObject))
            {
                var httpRequestMessage = httpRequestMessageAsObject as HttpRequestMessageProperty;
                if (httpRequestMessage != null && httpRequestMessage.Headers != null)
                {
                    headerValue = httpRequestMessage.Headers.Get(key);
                }
            }

            if(headerValue == null && context.IncomingMessageHeaders != null)
            {
                try
                {
                    var headerIdx = context.IncomingMessageHeaders.FindHeader(key, string.Empty);
                    if (headerIdx != -1)
                    {
                        headerValue = context.IncomingMessageHeaders.GetHeader<string>(headerIdx);
                    }
                }
                catch
                {
                    //Some of the headers cannot be extracted as strings.
                    //This catch will prevent this from bubbling up.
                    //Nonetheless, this header cannot be retrieved using this method.
                }
            }

            return headerValue != null
                            ? new[] { headerValue }
                            : null;
        }

        private string GetHeaderValueFromWebHeaderCollection(System.Collections.Specialized.NameValueCollection headers, string key)
        {
            return headers[key];
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var transactionAlreadyExists = transaction.IsValid;

            var methodInfo = TryGetMethodInfo(instrumentedMethodCall);
            if (methodInfo == null)
            {
                throw new NullReferenceException("methodInfo");
            }

            var instrumentedMethodName = instrumentedMethodCall.MethodCall.Method.MethodName;
            var parameters = GetParameters(instrumentedMethodCall.MethodCall, methodInfo, instrumentedMethodCall.MethodCall.MethodArguments, agent);

            ReportSupportabilityMetric_InvocationMethod(agent, instrumentedMethodName);

            var isTAP = instrumentedMethodCall.InstrumentedMethodInfo.Method.Type.Name == TAPTypeNameShort;
            var shouldTryProcessInboundCatOrDT = _methodNamesStart.Contains(instrumentedMethodName);
            var shouldTryEndTransaction = _methodNamesEndTrx.Contains(instrumentedMethodName);

            var uri = OperationContext.Current?.IncomingMessageHeaders?.To;

            var transactionName = GetTransactionName(agent, uri, methodInfo);

            // In all cases, we should record this work in a transaction.
            // either create it or use the one that is already there
            // For InvokeEnd, we expect a transaction to be there, but create it
            // just in case.
            if (!transactionAlreadyExists)
            {
                transaction = agent.CreateTransaction(
                    isWeb: true,
                    category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.WCF),
                    transactionDisplayName: "Windows Communication Foundation",
                    doNotTrackAsUnitOfWork: false);

                transaction.GetExperimentalApi().SetWrapperToken(_wrapperToken);

                CaptureHttpRequestHeadersAndMethod(agent, transaction);
            }

            var requestPath = uri?.AbsolutePath;
            if (!string.IsNullOrEmpty(requestPath))
            {
                transaction.SetUri(requestPath);
            }

            // For InvokeBegin, Invoke, or InvokeAsync, Set the transaction name and process
            // CAT or DT request information.
            if (shouldTryProcessInboundCatOrDT)
            {
                if (instrumentedMethodCall.IsAsync)
                {
                    transaction.AttachToAsync();
                }

                transaction.SetWebTransactionName(WebTransactionType.WCF, transactionName, TransactionNamePriority.FrameworkHigh);
                transaction.SetRequestParameters(parameters);

                if (!transactionAlreadyExists)
                {
                    var transportType = TransportType.Other;

                    var msgProperties = OperationContext.Current?.IncomingMessageProperties;
                    if (msgProperties != null && msgProperties.TryGetValue(HttpRequestMessageProperty.Name, out var httpRequestMessageObject))
                    {
                        if (httpRequestMessageObject is HttpRequestMessageProperty)
                        {
                            transportType = TransportType.HTTP;
                        }
                    }

                    transaction.AcceptDistributedTraceHeaders(OperationContext.Current, ExtractHeaderValue, transportType);
                }
            }

            // Don't create a segment to cover the EndInvoke on TAP Invocation
            // but we need to instrument the EndInvoke so that we can close the
            // transaction and send the CAT Response.  The continuation that
            // is used for TAP will not reliably have access to the OperationContext
            ISegment segment = null;
            if (!isTAP || _methodNamesStart.Contains(instrumentedMethodName))
            {
                segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, transactionName);
                if (isTAP)
                {
                    segment.AlwaysDeductChildDuration = true;
                }
            }

            Guid? wrapperExecutionID = null;
            void WriteLogMessage(string message)
            {
                if (!agent.Logger.IsEnabledFor(Agent.Extensions.Logging.Level.Finest))
                {
                    return;
                }

                if (wrapperExecutionID == null)
                {
                    wrapperExecutionID = Guid.NewGuid();
                }

                transaction.LogFinest($"Execution {wrapperExecutionID} - {instrumentedMethodName}: {message}");
            }


            var handledException = false;
            var isTAPContinuation = false;

            // This continuation method is meant for TAP Async only (InvokeAsync)
            // We don't end the transaction at this point because we dont
            // have access to the OperationContext.  We rely on the (InvokeEnd)
            // to handle this part.
            void HandleContinuation(System.Threading.Tasks.Task t)
            {
                WriteLogMessage("Continuation");

                if (t.IsFaulted)
                {
                    WriteLogMessage("Continuation - Notice Exception");
                    transaction.NoticeError(t.Exception);
                }

                if (segment != null)
                {
                    WriteLogMessage("Continuation - End Segment");
                    segment.End();
                }
            }

            return Delegates.GetDelegateFor(

                // This could occur on the Invoke, InvokeBegin, InvokeEnd
                onFailure: (Exception ex) =>
                {
                    WriteLogMessage("OnFailure");

                    transaction.NoticeError(ex);
                    handledException = true;
                },

                // This will get called on both Begin/End and TAP (InvokeAsync)
                onSuccess: (System.Threading.Tasks.Task result) =>
                {
                    // If it is TAP, need to wait until the async work is done.
                    // attach continuation to determine if exception has occurred and
                    // to end the segment.  For Begin/End, there is nothing to do
                    // in the continuation.
                    if (isTAP)
                    {
                        WriteLogMessage("OnSuccess - Schedule Continuation");
                        isTAPContinuation = true;
                        result.ContinueWith(HandleContinuation);
                    }
                },

                onComplete: () =>
                {
                    // If this is the TAP InvokeAsync, there is nothing to do here
                    // The continuation has been set up and it will determine if there 
                    // are any problems and to end the segment.
                    if (isTAPContinuation)
                    {
                        WriteLogMessage("OnComplete - NoOp wait for continuation");
                        return;
                    }

                    // In all cases, there is a segment, we want to end it.
                    // The only case where we wouldn't have a segment would be
                    // in the InvokeEnd for TAP
                    if (segment != null)
                    {
                        WriteLogMessage("OnComplete - End Segment");
                        segment.End();
                    }

                    // If an exception has occurred or if this is Invoke or InvokeEnd,
                    // the transaction should be closed and CAT Response prepared.
                    if (handledException || shouldTryEndTransaction)
                    {
                        var wcfStartedTransaction = transaction.GetExperimentalApi().GetWrapperToken() == _wrapperToken;

                        if (wcfStartedTransaction)
                        {
                            WriteLogMessage("OnComplete - End transaction");
                            transaction.End();

                            ProcessResponse(transaction, OperationContext.Current);
                        }
                    }
                });
        }

        private void CaptureHttpRequestHeadersAndMethod(IAgent agent, ITransaction transaction)
        {
            var context = OperationContext.Current;
            if (context.IncomingMessageProperties != null
                && context.IncomingMessageProperties.TryGetValue(HttpRequestMessageProperty.Name, out var httpRequestMessageAsObject)
                && httpRequestMessageAsObject is HttpRequestMessageProperty httpRequestMessage)
            {
                transaction.SetRequestMethod(httpRequestMessage.Method);

                if (httpRequestMessage.Headers != null)
                {
                    var headersToCapture = agent.Configuration.AllowAllRequestHeaders ? httpRequestMessage.Headers.AllKeys : Agent.Extensions.Providers.Wrapper.Statics.DefaultCaptureHeaders;

                    transaction.SetRequestHeaders(httpRequestMessage.Headers, headersToCapture, GetHeaderValueFromWebHeaderCollection);
                }
            }
        }

        /// <summary>
        /// Records supportability metric for the type of invocation.
        /// Only need to do this once per wrapper.
        /// </summary>
        private void ReportSupportabilityMetric_InvocationMethod(IAgent agent, string methodName)
        {
            //Since we share EndInvoke for both TAP and Begin/End Async, it is not 
            //contained in the dictionary
            if (!_methodNameInvocationTypesDic.TryGetValue(methodName, out string invocationTypeName))
            {
                return;
            }

            var shouldRecordMetric = false;
            lock (_rptSupMetric_InvocType_Lock)
            {
                shouldRecordMetric = !_rptSupMetric_InvocType.Contains(invocationTypeName);

                if (shouldRecordMetric)
                {
                    _rptSupMetric_InvocType.Add(invocationTypeName);
                }
            }

            if (shouldRecordMetric)
            {
                agent.GetExperimentalApi().RecordSupportabilityMetric($"WCFService/InvocationStyle/{invocationTypeName}");
            }
        }


        private void ProcessResponse(ITransaction transaction, OperationContext context)
        {
            var wcfStartedTransaction = transaction.GetExperimentalApi().GetWrapperToken() == _wrapperToken;
            if (!wcfStartedTransaction)
            {
                return;
            }

            var headersToAttach = transaction.GetResponseMetadata();
            foreach (var header in headersToAttach)
            {
                //Supporting non HTTP
                var outgoingMessageHeaders = context.OutgoingMessageHeaders;
                if (outgoingMessageHeaders != null && outgoingMessageHeaders.MessageVersion.Envelope != EnvelopeVersion.None)
                {
                    context.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(header.Key, "", header.Value));
                }

                AddHeaderToHttpResponsePropertyForOutgoingMessage(header, context);
            }
        }


        private void AddHeaderToHttpResponsePropertyForOutgoingMessage(KeyValuePair<string, string> header, OperationContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.OutgoingMessageProperties.TryGetValue(HttpResponseMessageProperty.Name, out var httpResponseMessagePropertyObject))
            {
                var httpResponseMessageProperty = httpResponseMessagePropertyObject as HttpResponseMessageProperty;
                httpResponseMessageProperty?.Headers.Add(header.Key, header.Value);
            }
            else
            {
                var httpResponseMessageProperty = new HttpResponseMessageProperty();
                httpResponseMessageProperty.Headers.Add(header.Key, header.Value);
                context.OutgoingMessageProperties.Add(HttpResponseMessageProperty.Name, httpResponseMessageProperty);
            }
        }

        private string GetTransactionName(IAgent agent, Uri uri, MethodInfo methodInfo)
        {
            if (agent.Configuration.UseResourceBasedNamingForWCFEnabled)
            {
                if (uri != null)
                {
                    return UriHelpers.GetTransactionNameFromPath(uri.AbsolutePath);
                }
            }

            var typeName = GetTypeName(methodInfo);
            var methodName = GetMethodName(methodInfo);

            return $"{typeName}.{methodName}";
        }

        private MethodInfo TryGetMethodInfo(InstrumentedMethodCall instrumentedMethodCall)
        {
            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
            var invocationTarget = instrumentedMethodCall.MethodCall.InvocationTarget;
            var isTAP = instrumentedMethodCall.InstrumentedMethodInfo.Method.Type.Name == TAPTypeNameShort;

            if (methodName == SyncMethodName)
            {
                return Statics.GetSyncMethodInfo(invocationTarget);
            }
            else if (methodName == InvokeBeginMethodName)
            {
                return isTAP ? Statics.GetTAPAsyncTaskMethodInfo(invocationTarget) : Statics.GetAsyncBeginMethodInfo(invocationTarget);
            }
            else if (methodName == InvokeEndMethodName)
            {
                return isTAP ? Statics.GetTAPAsyncTaskMethodInfo(invocationTarget) : Statics.GetAsyncEndMethodInfo(invocationTarget);
            }
            else if (methodName == InvokeAsyncMethodName)
            {
                return Statics.GetTAPAsyncTaskMethodInfo(invocationTarget);
            }

            throw new Exception($"Unexpected instrumented method in wrapper: {instrumentedMethodCall.MethodCall.Method.MethodName}");
        }

        private string GetTypeName(MethodInfo methodInfo)
        {
            var type = methodInfo.DeclaringType;
            if (type == null)
                throw new NullReferenceException("type");

            var name = type.FullName;
            if (name == null)
                throw new NullReferenceException("name");

            return name;
        }

        private string GetMethodName(MethodInfo methodInfo)
        {
            var name = methodInfo.Name;
            if (name == null)
                throw new NullReferenceException("name");

            return name;
        }

        private IEnumerable<KeyValuePair<string, string>> GetParameters(MethodCall methodCall, MethodInfo methodInfo, object[] arguments, IAgent agent)
        {
            // only the begin methods will have parameters, end won't
            if (methodCall.Method.MethodName != SyncMethodName
                && methodCall.Method.MethodName != InvokeBeginMethodName)
                return Enumerable.Empty<KeyValuePair<string, string>>();

            var parameters = arguments.ExtractNotNullAs<object[]>(1);

            var parameterInfos = methodInfo.GetParameters();
            if (parameterInfos == null)
                throw new Exception("MethodInfo did not contain parameters!");

            // if this occurs their app will throw an exception as well, which we will hopefully notice
            if (parameters.Length > parameterInfos.Length)
                return Enumerable.Empty<KeyValuePair<string, string>>();

            var result = new Dictionary<string, string>();
            for (var i = 0; i < parameters.Length; ++i)
            {
                var parameterInfo = parameterInfos[i];
                if (parameterInfo == null)
                    throw new Exception("There was a null parameterInfo in the parameter infos array at index " + i);
                if (parameterInfo.Name == null)
                    throw new Exception("A parameterInfo at index " + i + " did not have a name.");
                var keyString = parameterInfo.Name;

                var value = parameters[i];
                var valueString = (value == null) ? "null" : value.ToString();
                result.Add(keyString, valueString);
            }

            return result;
        }

    }
}
