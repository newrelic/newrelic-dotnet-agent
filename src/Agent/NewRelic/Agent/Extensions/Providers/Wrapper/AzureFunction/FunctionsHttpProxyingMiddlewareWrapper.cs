// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

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
            switch (instrumentedMethodCall.MethodCall.Method.MethodName)
            {
                case "AddHttpContextToFunctionContext":
                    var httpContext = (HttpContext)instrumentedMethodCall.MethodCall.MethodArguments[1];

                    agent.CurrentTransaction.SetRequestMethod(httpContext.Request.Method);
                    agent.CurrentTransaction.SetUri(httpContext.Request.Path);
                    break;
                case "TryHandleHttpResult":
                    if (!agent.CurrentTransaction.HasHttpResponseStatusCode) // these handlers seem to get called more than once; only set the status code one time
                    {
                        object result = instrumentedMethodCall.MethodCall.MethodArguments[0];

                        httpContext = (HttpContext)instrumentedMethodCall.MethodCall.MethodArguments[2];
                        bool isInvocationResult = (bool)instrumentedMethodCall.MethodCall.MethodArguments[3];

                        agent.CurrentTransaction.SetHttpResponseStatusCode(httpContext.Response.StatusCode);
                    }
                    break;
                case "TryHandleOutputBindingsHttpResult":
                    if (!agent.CurrentTransaction.HasHttpResponseStatusCode) // these handlers seem to get called more than once; only set the status code one time
                    {
                        httpContext = (HttpContext)instrumentedMethodCall.MethodCall.MethodArguments[1];
                        agent.CurrentTransaction.SetHttpResponseStatusCode(httpContext.Response.StatusCode);
                    }
                    break;
            }
        }

        return Delegates.NoOp;
    }
}
