// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class FunctionsHttpProxyingMiddlewareWrapper : IWrapper
{
    private const string WrapperName = "FunctionsHttpProxyingMiddlewareWrapper";

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
    }

    /// <summary>
    /// Gets request method / path for Azure function HttpTrigger invocations
    /// in apps that use the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore package
    /// </summary>
    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (agent.Configuration.AzureFunctionModeEnabled)
        {
            dynamic httpContext;
            switch (instrumentedMethodCall.MethodCall.Method.MethodName)
            {
                case "AddHttpContextToFunctionContext":
                    httpContext = instrumentedMethodCall.MethodCall.MethodArguments[1];

                    agent.CurrentTransaction.SetRequestMethod(httpContext.Request.Method);
                    agent.CurrentTransaction.SetUri(httpContext.Request.Path);

                    // Only need to accept DT headers from incoming request.
                    var headers = httpContext.Request.Headers as IDictionary<string, StringValues>;
                    transaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);
                    break;
                case "TryHandleHttpResult":
                    if (!agent.CurrentTransaction.HasHttpResponseStatusCode) // these handlers seem to get called more than once; only set the status code one time
                    {
                        httpContext = instrumentedMethodCall.MethodCall.MethodArguments[2];
                        agent.CurrentTransaction.SetHttpResponseStatusCode(httpContext.Response.StatusCode);
                    }
                    break;
                case "TryHandleOutputBindingsHttpResult":
                    if (!agent.CurrentTransaction.HasHttpResponseStatusCode) // these handlers seem to get called more than once; only set the status code one time
                    {
                        httpContext = instrumentedMethodCall.MethodCall.MethodArguments[1];
                        agent.CurrentTransaction.SetHttpResponseStatusCode(httpContext.Response.StatusCode);
                    }
                    break;
            }
        }

        return Delegates.NoOp;

        static IEnumerable<string> GetHeaderValue(IDictionary<string, StringValues> headers, string key)
        {
            if (!headers.ContainsKey(key))
                return [];

            return headers[key].ToArray();
        }
    }
}
