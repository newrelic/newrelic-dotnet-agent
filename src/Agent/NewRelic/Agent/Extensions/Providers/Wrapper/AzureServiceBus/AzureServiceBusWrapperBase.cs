// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public abstract class AzureServiceBusWrapperBase : IWrapper
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();

    protected const string BrokerVendorName = "AzureServiceBus";

    public virtual bool IsTransactionRequired => true; // only instrument service bus methods if we're already in a transaction

    public abstract CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo);

    public abstract AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction);

    protected static object GetTaskResultFromObject(object taskObj)
    {
        var task = taskObj as Task;
        if (task == null)
        {
            return null;
        }
        if (task.IsFaulted)
        {
            return null;
        }
        if (task.IsCanceled)
        {
            return null;
        }

        var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
        return getResponse(task);
    }

    protected void HandleReceiveResponse(Task responseTask, string instrumentedMethodName, ITransaction transaction)
    {
        if (responseTask.IsCanceled)
            return;

        if (responseTask.IsFaulted)
        {
            transaction.NoticeError(responseTask.Exception);
            return;
        }

        var resultObj = GetTaskResultFromObject(responseTask);
        ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName);
    }
    protected void ExtractDTHeadersIfAvailable(object resultObj, ITransaction transaction, string instrumentedMethodName)
    {
        if (resultObj != null)
        {
            switch (instrumentedMethodName)
            {
                case "ReceiveMessagesAsync":
                case "ReceiveDeferredMessagesAsync":
                case "PeekMessagesInternalAsync":
                    // the response contains a list of messages.
                    // get the first message from the response and extract DT headers
                    dynamic messages = resultObj;
                    if (messages.Count > 0)
                    {
                        var msg = messages[0];
                        if (msg.ApplicationProperties is ReadOnlyDictionary<string, object> applicationProperties)
                        {
                            transaction.AcceptDistributedTraceHeaders(applicationProperties, ProcessHeaders, TransportType.Queue);
                        }
                    }
                    break;
            }
        }
    }

    protected IEnumerable<string> ProcessHeaders(ReadOnlyDictionary<string, object> applicationProperties, string key)
    {
        var headerValues = new List<string>();
        foreach (var item in applicationProperties)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                headerValues.Add(item.Value as string);
            }
        }

        return headerValues;
    }
}
