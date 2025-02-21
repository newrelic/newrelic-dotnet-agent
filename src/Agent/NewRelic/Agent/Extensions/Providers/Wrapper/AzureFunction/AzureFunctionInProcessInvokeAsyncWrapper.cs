// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // get the transaction and find an attribute called faas.trigger
        var trigger = transaction.GetFaasAttribute("faas.trigger") as string;

        bool handledHttpArg = false;
        if (trigger == "http") // this is (currently) the only trigger we care about
        {
            object[] args = (object[])instrumentedMethodCall.MethodCall.MethodArguments[1];

            // iterate each argument to find one with a type we can work with
            foreach (object arg in args)
            {
                var argType = arg?.GetType().FullName;
                handledHttpArg = TryHandleHttpTrigger(arg, argType, transaction);
                if (handledHttpArg)
                    break;
            }

            if (!handledHttpArg)
            {
                agent.Logger.Info("Unable to set http-specific attributes on this transaction; could not find suitable function argument type.");
            }
        }

        return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                transaction.CurrentSegment,
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

                if (trigger == "http" && handledHttpArg) // don't try to set the response code if we didn't handle the http trigger arg
                {
                    TrySetHttpResponseStatusCode(responseTask, transaction, agent);
                }
            }
            catch (Exception ex)
            {
                agent.Logger.Warn(ex, "Error processing Azure Function response.");
                throw;
            }
        }
    }

    private bool TrySetHttpResponseStatusCode(Task responseTask, ITransaction transaction, IAgent agent)
    {
        var result = GetTaskResult(responseTask);
        // make sure there's a StatusCode property on the result object
        var statusCodeProperty = result?.GetType().GetProperty("StatusCode");
        if (statusCodeProperty != null)
        {
            var statusCode = statusCodeProperty.GetValue(result);
            if (statusCode != null)
            {
                transaction.SetHttpResponseStatusCode((int)statusCode);
                return true;
            }
        }
        else
        {
            agent.Logger.Debug($"Could not find StatusCode property on response object type {result?.GetType().FullName}");
        }

        return false;
    }

    private bool TryHandleHttpTrigger(dynamic arg, string argTypeName, ITransaction transaction)
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
