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
        public const string InstrumentedTypeName = "System.Net.Http.HttpClient";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            if (method.MatchesAny(assemblyName: "System.Net.Http", typeName: InstrumentedTypeName, methodName: "SendAsync"))
            {
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("System.Net.Http", "System.Net.Http.HttpClient", method.MethodName);
            }
            else
            {
                return new CanWrapResponse(false);
            }
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var httpRequestMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpRequestMessage>(0);
            var httpClient = (System.Net.Http.HttpClient)instrumentedMethodCall.MethodCall.InvocationTarget;
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
            segment.GetExperimentalApi().SetSegmentData(externalSegmentData);

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

            // 1.  Since this finishes on a background thread, it is possible it will race the end of
            //     the transaction. Using holdTransactionOpen = true to prevent the transaction from ending early.
            // 2.  Do not want to post to the sync context as this library is commonly used with the
            //     blocking TPL pattern of .Result or .Wait(). Posting to the sync context will result
            //     in recording time waiting for the current unit of work on the sync context to finish.
            //     This overload GetAsyncDelegateFor does not use the synchronization context's task scheduler.
            return Delegates.GetAsyncDelegateFor<Task<HttpResponseMessage>>(agent, segment, true, InvokeTryProcessResponse);

            void InvokeTryProcessResponse(Task<HttpResponseMessage> httpResponseMessage)
            {
                TryProcessResponse(agent, httpResponseMessage, transaction, segment, externalSegmentData);
            }
        }

        private static Uri TryGetAbsoluteUri(HttpRequestMessage httpRequestMessage, System.Net.Http.HttpClient httpClient)
        {
            // If RequestUri is specified and it is an absolute URI then we should use it
            if (httpRequestMessage.RequestUri?.IsAbsoluteUri == true)
                return httpRequestMessage.RequestUri;

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
            try
            {
                if (!ValidTaskResponse(response) || (segment == null))
                {
                    return;
                }

                var result = response?.Result;

                var httpStatusCode = result?.StatusCode;
                if (httpStatusCode.HasValue)
                {
                    externalSegmentData.SetHttpStatusCode((int)httpStatusCode);
                }

                var headers = result?.Headers?.ToList();
                if (headers == null)
                    return;

                var flattenedHeaders = headers.Select(Flatten);

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
