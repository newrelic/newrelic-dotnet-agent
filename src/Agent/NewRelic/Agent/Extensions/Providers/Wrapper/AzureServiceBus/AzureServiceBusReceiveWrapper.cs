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

public class AzureServiceBusReceiveWrapper : IWrapper
{
    private const string BrokerVendorName = "AzureServiceBus";
    private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiveWrapper));
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath;
        string identifier = serviceBusReceiver.Identifier;
        string fqns = serviceBusReceiver.FullyQualifiedNamespace;

        // create a transaction
        transaction = agent.CreateTransaction(
            destinationType: MessageBrokerDestinationType.Queue,
            brokerVendorName: BrokerVendorName,
            destination: queueName);

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
            transaction.DetachFromPrimary(); //Remove from thread-local type storage
        }

        // start a message broker segment
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            MessageBrokerDestinationType.Queue,
            MessageBrokerAction.Consume,
            BrokerVendorName, queueName);

        // return an async delegate
        return Delegates.GetAsyncDelegateFor<Task>(
            agent,
            segment,
            true,
            HandleResponse,
            TaskContinuationOptions.ExecuteSynchronously);

        void HandleResponse(Task responseTask)
        {
            try
            {
                if (responseTask.IsFaulted)
                {
                    // TODO: handle error here?
                    return;
                }

                // get the first message from the response and extract DT headers
                // per https://github.com/Azure/azure-sdk-for-net/issues/33652#issuecomment-1451320679
                // the headers are in the ApplicationProperties dictionary
                dynamic resultObj = GetTaskResult(responseTask);
                if (resultObj != null && resultObj.Count > 0)
                {
                    var msg = resultObj[0];
                    if (msg.ApplicationProperties is ReadOnlyDictionary<string, object> applicationProperties)
                    {
                        transaction.AcceptDistributedTraceHeaders(applicationProperties, ProcessHeaders, TransportType.Queue);
                    }
                }
            }
            finally
            {
                segment.End();
                transaction.End();
            }
        }
    }

    private IEnumerable<string> ProcessHeaders(ReadOnlyDictionary<string, object> applicationProperties, string key)
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
