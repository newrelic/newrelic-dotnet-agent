// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RestSharp
{
    public class ExecuteTaskAsync : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            //Because this does not leverage the sync context, it does not need to check for the legacy pipepline
            return new CanWrapResponse("NewRelic.Providers.Wrapper.RestSharp.ExecuteTaskAsync".Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var restClient = instrumentedMethodCall.MethodCall.InvocationTarget;
            var restRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];

            Uri uri;

            try
            {
                uri = RestSharpHelper.BuildUri(restClient, restRequest);
            }
            catch (Exception)
            {
                //BuildUri will throw an exception in RestSharp if the user does not have BaseUrl set.
                //Since the request will never execute, we will just NoOp.
                return Delegates.NoOp;
            }

            var method = RestSharpHelper.GetMethod(restRequest).ToString();

            var transactionExperimental = transaction.GetExperimentalApi();

            var externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, method);
            var segment = agent.CurrentTransaction.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, method);
            segment.GetExperimentalApi().SetSegmentData(externalSegmentData);

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse);

            void InvokeTryProcessResponse(Task completedTask)
            {
                TryProcessResponse(agent, completedTask, transaction, segment, externalSegmentData);
            }
        }

        private static void TryProcessResponse(IAgent agent, Task responseTask, ITransaction transaction, ISegment segment, IExternalSegmentData externalSegmentData)
        {
            try
            {
                if (!ValidTaskResponse(responseTask) || (segment == null))
                {
                    return;
                }

                var restResponse = RestSharpHelper.GetRestResponse(responseTask);

                var statusCode = RestSharpHelper.GetResponseStatusCode(restResponse);
                if (statusCode != 0)
                {
                    externalSegmentData.SetHttpStatus(statusCode);
                }

                var headers = RestSharpHelper.GetResponseHeaders(restResponse);
                if (headers == null)
                {
                    return;
                }

                transaction.ProcessInboundResponse(headers, segment);
            }
            catch (Exception ex)
            {
                agent.HandleWrapperException(ex);
            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return (response?.Status == TaskStatus.RanToCompletion);
        }

    }
}
