// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.OpenSearch
{
    public class RequestWrapper : SearchRequestWrapperBase, IWrapper
    {
        private const string WrapperName = "OpenSearchRequestWrapper";

        public override DatastoreVendor Vendor => DatastoreVendor.OpenSearch;

        public override int RequestParamsIndex => 3;

        public override int RequestParamsIndexAsync => 4;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));

        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var isAsync = instrumentedMethodCall.IsAsync ||
                          instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName.EndsWith("RequestAsync");

            var segment = BuildSegment(isAsync, instrumentedMethodCall, transaction);

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
