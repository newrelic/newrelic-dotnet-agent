// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Elasticsearch
{

    public class RequestWrapper : SearchRequestWrapperBase, IWrapper
    {
        private const string WrapperName = "ElasticsearchRequestWrapper";
        private const int RequestParamsIndex = 3;
        private const int RequestParamsIndexAsync = 4;

        public override DatastoreVendor Vendor => DatastoreVendor.Elasticsearch;

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var isAsync = instrumentedMethodCall.IsAsync ||
                          instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName.EndsWith("RequestAsync");

            var indexOfRequestParams = RequestParamsIndex;

            if (isAsync)
            {
                transaction.AttachToAsync();
                var parameterTypeNamesList = instrumentedMethodCall.InstrumentedMethodInfo.Method.ParameterTypeNames.Split(',');
                if (parameterTypeNamesList[RequestParamsIndexAsync] == "Elasticsearch.Net.IRequestParameters")
                {
                    indexOfRequestParams = RequestParamsIndexAsync;
                }
            }

            var segment = BuildSegment(indexOfRequestParams, instrumentedMethodCall, transaction);

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    if (!ValidTaskResponse(responseTask) || (segment == null))
                    {
                        return;
                    }
                    var responseGetter = GetRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
                    var response = responseGetter(responseTask);
                    TryProcessResponse(agent, transaction, response, segment);
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                        onSuccess: response =>
                        {
                            TryProcessResponse(agent, transaction, response, segment);
                        },
                        onFailure: exception =>
                        {
                            // Don't know how valid this is
                            segment.End(exception);
                        });
            }
        }
    }
}
