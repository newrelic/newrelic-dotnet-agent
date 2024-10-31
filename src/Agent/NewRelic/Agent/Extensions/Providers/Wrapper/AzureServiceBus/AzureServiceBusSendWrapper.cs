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

public class AzureServiceBusSendWrapper : IWrapper
{
    private const string BrokerVendorName = "AzureServiceBus";
    private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusSendWrapper));
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath;
        string identifier = serviceBusReceiver.Identifier;
        string fqns = serviceBusReceiver.FullyQualifiedNamespace;

        // ???
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

        dynamic messages = instrumentedMethodCall.MethodCall.MethodArguments[0];
        // iterate all messages that are being sent,
        // insert DT headers into each message
        foreach (var message in messages)
        {
            if (message.ApplicationProperties is IDictionary<string, object> applicationProperties)
                transaction.InsertDistributedTraceHeaders(applicationProperties, ProcessHeaders);
        }

        // return an async delegate
        return Delegates.GetAsyncDelegateFor<Task>(agent,segment);
    }

    private void ProcessHeaders(IDictionary<string, object> applicationProperties, string key, string value)
    {
        applicationProperties.Add(key, value);
    }
}
