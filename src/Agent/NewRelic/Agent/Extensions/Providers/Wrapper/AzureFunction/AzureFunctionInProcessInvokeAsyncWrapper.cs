// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class AzureFunctionInProcessInvokeAsyncWrapper : IWrapper
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(nameof(AzureFunctionInProcessInvokeAsyncWrapper).Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (!agent.Configuration.AzureFunctionModeEnabled) // bail early if azure function mode isn't enabled
        {
            return Delegates.NoOp;
        }

        var trigger = transaction.GetFaasAttribute("faas.trigger") as string;
        var name = transaction.GetFaasAttribute("faas.name") as string;
        var functionName = name?.Substring(name.LastIndexOf('/') + 1);

        object[] args = (object[])instrumentedMethodCall.MethodCall.MethodArguments[1];

        bool handledHttpArg = false;
        if (trigger == "http")
        {
            // iterate each argument to find one with a type we can work with
            foreach (object arg in args)
            {
                var argType = arg?.GetType().FullName;
                handledHttpArg = TryHandleHttpTrigger(arg, argType, transaction, agent);
                if (handledHttpArg)
                    break;
            }

            if (!handledHttpArg)
            {
                var argTypeNames = string.Join(", ", args.Select(a => a?.GetType().FullName));
                agent.Logger.Debug($"Unable to extract HttpTrigger attributes from request argument(s): {argTypeNames}");
            }
        }

        var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, functionName);

        return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                InvokeFunctionAsyncResponse,
                TaskContinuationOptions.ExecuteSynchronously);

        void InvokeFunctionAsyncResponse(Task responseTask)
        {
            try
            {
                if (responseTask.IsFaulted)
                {
                    transaction.NoticeError(responseTask.Exception);
                    return;
                }

                var result = GetTaskResult(responseTask);
                if (result == null)
                {
                    return;
                }

                var resultType = result.GetType();
                agent.Logger.Debug($"Azure Function response type: {resultType.FullName}");

                // if the trigger is HTTP, try to set the StatusCode
                if (trigger == "http" && handledHttpArg)
                {
                    TrySetHttpResponseStatusCode(result, resultType, transaction, agent);
                }
            }
            finally
            {
                segment.End();
            }
        }
    }

    private bool TrySetHttpResponseStatusCode(dynamic result, Type resultType, ITransaction transaction, IAgent agent)
    {
        // make sure there's a StatusCode property on the result object
        var statusCodeProperty = resultType.GetProperty("StatusCode");
        if (statusCodeProperty != null)
        {
            var statusCode = statusCodeProperty.GetValue(result);
            if (statusCode != null)
            {
                transaction.SetHttpResponseStatusCode((int)statusCode);
                return true;
            }
        }

        agent.Logger.Debug($"Could not find StatusCode property on response object type {result?.GetType().FullName}");
        return false;
    }

    private bool TryHandleHttpTrigger(dynamic arg, string argTypeName, ITransaction transaction, IAgent agent)
    {
        if (arg is System.Net.Http.HttpRequestMessage httpTriggerArg)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = httpTriggerArg.Headers; // IEnumerable<KeyValuePair<string, IEnumerable<string>>>

            Uri requestUri = httpTriggerArg.RequestUri;
            string requestPath = requestUri.AbsolutePath;
            transaction.SetUri(requestPath);

            string requestMethod = httpTriggerArg.Method.Method;
            transaction.SetRequestMethod(requestMethod);

            if (headers != null)
            {
                transaction.AcceptDistributedTraceHeaders(headers, GetHeaderValueFromIEnumerable, TransportType.HTTP);
            }

            return true;
        }

        if (argTypeName is "Microsoft.AspNetCore.Http.DefaultHttpRequest" or "Microsoft.AspNetCore.Http.HttpRequest")
        {
            IDictionary<string, Microsoft.Extensions.Primitives.StringValues> headers = arg.Headers;

            var requestMethod = arg.Method;
            var requestPath = arg.Path.Value;

            transaction.SetRequestMethod(requestMethod);
            transaction.SetUri(requestPath);

            if (headers?.Count != 0)
            {
                transaction.AcceptDistributedTraceHeaders(headers, GetHeaderValueFromIDictionary, TransportType.HTTP);
            }

            return true;
        }

        return false;
    }

    private IEnumerable<string> GetHeaderValueFromIEnumerable(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string key)
    {
        var val = headers.FirstOrDefault(kvp => kvp.Key == key).Value;
        return val;
    }

    private IEnumerable<string> GetHeaderValueFromIDictionary(IDictionary<string, Microsoft.Extensions.Primitives.StringValues> headers, string key)
    {
        var val = headers.FirstOrDefault(kvp => kvp.Key == key).Value;
        return val;
    }


    private static object GetTaskResult(object task)
    {
        if (((Task)task).IsFaulted)
        {
            return null;
        }

        var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
        return getResponse(task);
    }
}
