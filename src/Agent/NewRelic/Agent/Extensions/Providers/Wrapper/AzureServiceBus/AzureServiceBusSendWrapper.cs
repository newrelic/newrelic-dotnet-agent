// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public class AzureServiceBusSendWrapper : AzureServiceBusWrapperBase
{
    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusSendWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath; // some-queue-name
        string fqns = serviceBusReceiver.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net   

        // determine message broker action based on method name
        MessageBrokerAction action =
            instrumentedMethodCall.MethodCall.Method.MethodName switch
            {
                "SendMessagesAsync" => MessageBrokerAction.Produce,
                "ScheduleMessagesAsync" => MessageBrokerAction.Produce,
                "CancelScheduledMessagesAsync" => MessageBrokerAction.Purge, // TODO is this correct ???
                _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unexpected instrumented method call: {instrumentedMethodCall.MethodCall.Method.MethodName}")
            };

        // start a message broker segment
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            MessageBrokerDestinationType.Queue,
            action,
            BrokerVendorName,
            queueName,
            serverAddress: fqns);

        if (action == MessageBrokerAction.Produce)
        {
            dynamic messages = instrumentedMethodCall.MethodCall.MethodArguments[0];

            // iterate all messages that are being sent,
            // insert DT headers into each message
            foreach (var message in messages)
            {
                if (message.ApplicationProperties is IDictionary<string, object> applicationProperties)
                    transaction.InsertDistributedTraceHeaders(applicationProperties, ProcessHeaders);
            }

            void ProcessHeaders(IDictionary<string, object> applicationProperties, string key, string value)
            {
                applicationProperties.Add(key, value);
            }
        }

        return instrumentedMethodCall.IsAsync ? Delegates.GetAsyncDelegateFor<Task>(agent, segment) : Delegates.GetDelegateFor(segment);
    }
}
