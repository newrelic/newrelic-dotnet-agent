// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.HttpClient
{
    public class SendAsync : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string AssemblyName = "System.Net.Http";
        private const string HttpClientTypeName = "System.Net.Http.HttpClient";
        private const string SocketsHttpHandlerTypeName = "System.Net.Http.SocketsHttpHandler";
        private const string SendAsyncMethodName = "SendAsync";
        private const string SendMethodName = "Send";
        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            if (method.MatchesAny(assemblyName: AssemblyName, typeNames: new[] { HttpClientTypeName, SocketsHttpHandlerTypeName }, methodName: SendAsyncMethodName))
            {
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod(AssemblyName, HttpClientTypeName, method.MethodName);
            }
            else if (method.MatchesAny(assemblyName: AssemblyName, typeName: SocketsHttpHandlerTypeName, methodName: SendMethodName))
            {
                return new CanWrapResponse(true);
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var httpRequestMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpRequestMessage>(0);

            var httpClient = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.Http.HttpClient;
            var uri = TryGetAbsoluteUri(httpRequestMessage, httpClient);
            if (uri == null)
            {
                // It is possible for RequestUri to be null, but if it is then SendAsync method will eventually throw (which we will see). It would not be valuable to throw another exception here.
                return Delegates.NoOp;
            }

            var method = (httpRequestMessage.Method != null ? httpRequestMessage.Method.Method : "<unknown>") ?? "<unknown>";

            var transactionExperimental = transaction.GetExperimentalApi();

            var externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, method);
            var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
            segment.GetExperimentalApi()
                .SetSegmentData(externalSegmentData)
                .MakeLeaf();

            if (agent.Configuration.ForceSynchronousTimingCalculationHttpClient)
            {
                //When segments complete on a thread that is different than the thread of the parent segment,
                //we typically do not deduct the child segment's duration from the parent segment's duration
                //when calculating exclusive time for the parent. In versions of the agent prior to 6.20 this was not
                //the case and at least one customer is complaining about this. We are special-casing this behavior
                //for HttpClient to make this customer happier, and because HttpClient is not a real "async" method.
                //Please refer to the "total time" definition in https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
                //for more information.

                //This pattern should not be copied to other instrumentation without a good reason, because there may be a better
                //pattern to use for that use case.
                segment.DurationShouldBeDeductedFromParent = true;
            }


            // We cannot rely on SerializeHeadersWrapper to attach the headers because it is called on a thread that does not have access to the transaction
            TryAttachHeadersToRequest(agent, httpRequestMessage);

            if (instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName.Equals(SendMethodName))
            {
                return Delegates.GetDelegateFor<HttpResponseMessage>(
                    onSuccess: response =>
                    {
                        TryProcessResponse(agent, response, transaction, segment, externalSegmentData);
                        segment.End();
                    },
                    onFailure: exception =>
                    {
                        segment.End(exception);
                    });
            }
            else
            {
                // With .Net 6 the HttpClient headers are especially not thread safe (and weren't guaranteed to be before), so we need our continuation to happen synchronously.
                // This does mean any work done by TryProcessResponse will be included in the instrumented method segment.
                return Delegates.GetAsyncDelegateFor<Task<HttpResponseMessage>>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task<HttpResponseMessage> httpResponseMessage)
                {
                    TryProcessResponse(agent, httpResponseMessage, transaction, segment, externalSegmentData);
                }
            }
        }

        private static Uri TryGetAbsoluteUri(HttpRequestMessage httpRequestMessage, System.Net.Http.HttpClient httpClient)
        {
            // If RequestUri is specified and it is an absolute URI then we should use it
            if (httpRequestMessage.RequestUri?.IsAbsoluteUri == true)
                return httpRequestMessage.RequestUri;

            if (httpClient == null)
                return null;

            // If RequestUri is specified but isn't absolute then we need to combine it with the BaseAddress, as long as the BaseAddress is an absolute URI
            if (httpRequestMessage.RequestUri?.IsAbsoluteUri == false && httpClient.BaseAddress?.IsAbsoluteUri == true)
                return new Uri(httpClient.BaseAddress, httpRequestMessage.RequestUri);

            // If only BaseAddress is specified and it is an absolute URI then we can use it instead
            if (httpRequestMessage.RequestUri == null && httpClient.BaseAddress?.IsAbsoluteUri == true)
                return httpClient.BaseAddress;

            // In all other cases we cannot construct a valid absolute URI
            return null;
        }

        private static void TryAttachHeadersToRequest(IAgent agent, HttpRequestMessage httpRequestMessage)
        {
            var setHeaders = new Action<HttpRequestMessage, string, string>((carrier, key, value) =>
            {
                // Content headers should not contain the expected header keys, but sometimes they do,
                // and their presence can cause problems downstream by having 2 values for the same key.
                carrier.Content?.Headers?.Remove(key);
                // "Add" will throw if value exists, so we must remove it first
                carrier.Headers?.Remove(key);
                carrier.Headers?.Add(key, value);
            });

            try
            {
                agent.CurrentTransaction.InsertDistributedTraceHeaders(httpRequestMessage, setHeaders);
            }
            catch (Exception ex)
            {
                agent.HandleWrapperException(ex);
            }
        }

        private static void TryProcessResponse(IAgent agent, Task<HttpResponseMessage> response, ITransaction transaction, ISegment segment, IExternalSegmentData externalSegmentData)
        {

            if (!ValidTaskResponse(response) || (segment == null))
            {
                return;
            }

            TryProcessResponse(agent, response?.Result, transaction, segment, externalSegmentData);
        }

        private static void TryProcessResponse(IAgent agent, HttpResponseMessage response, ITransaction transaction, ISegment segment, IExternalSegmentData externalSegmentData)
        {
            try
            {
                if (response == null || segment == null)
                {
                    return;
                }

                var httpStatusCode = response.StatusCode;
                externalSegmentData.SetHttpStatusCode((int)httpStatusCode);

                // Everything after this is for CAT, so bail if we're not using it
                if (agent.Configuration.DistributedTracingEnabled || !agent.Configuration.CrossApplicationTracingEnabled)
                    return;

                var flattenedHeaders = response.Headers?.Select(Flatten);
                if (flattenedHeaders == null)
                    return;

                transaction.ProcessInboundResponse(flattenedHeaders, segment);
            }
            catch (Exception ex)
            {
                agent.HandleWrapperException(ex);
            }
        }


        private static bool ValidTaskResponse(Task<HttpResponseMessage> response)
        {
            return (response?.Status == TaskStatus.RanToCompletion);
        }

        private static KeyValuePair<string, string> Flatten(KeyValuePair<string, IEnumerable<string>> header)
        {
            var key = header.Key;
            var values = header.Value ?? Enumerable.Empty<string>();

            // According to RFC 2616 (http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2), multi-valued headers can be represented as a single comma-delimited list of values
            var flattenedValues = string.Join(",", values);

            return new KeyValuePair<string, string>(key, flattenedValues);
        }
    }
}
