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

public class AzureServiceBusReceiveWrapper : AzureServiceBusWrapperBase
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();

    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiveWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath; // marty-test-queue
        //string identifier = serviceBusReceiver.Identifier; // -9e860ed4-b16b-4d02-96e4-d8ed224ae24b
        string fqns = serviceBusReceiver.FullyQualifiedNamespace; // mt-test-servicebus.servicebus.windows.net   

        MessageBrokerAction action =
            instrumentedMethodCall.MethodCall.Method.MethodName switch
            {
                "ReceiveMessagesAsync" => MessageBrokerAction.Consume,
                "ReceiveDeferredMessagesAsync" => MessageBrokerAction.Consume,
                "PeekMessagesInternalAsync" => MessageBrokerAction.Peek,
                "AbandonMessageAsync" => MessageBrokerAction.Purge, // TODO is this correct ???,
                "CompleteMessageAsync" => MessageBrokerAction.Consume,
                "DeadLetterInternalAsync" => MessageBrokerAction.Purge,  // TODO is this correct ???
                "DeferMessageAsync" => MessageBrokerAction.Consume, // TODO is this correct or should we extend MessageBrokerAction with more values???
                "RenewMessageLockAsync" => MessageBrokerAction.Consume, // TODO is this correct or should we extend MessageBrokerAction with more values???
                _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unexpected instrumented method call: {instrumentedMethodCall.MethodCall.Method.MethodName}")
            };

        // start a message broker segment
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            MessageBrokerDestinationType.Queue,
            action,
            BrokerVendorName,
            queueName,
            serverAddress: fqns );

        if (instrumentedMethodCall.IsAsync)
        {
            // return an async delegate
            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                HandleResponse,
                TaskContinuationOptions.ExecuteSynchronously);

            void HandleResponse(Task responseTask)
            {
                try
                {
                    if (responseTask.IsFaulted)
                    {
                        transaction.NoticeError(responseTask.Exception); // TODO ??? 
                        return;
                    }

                    var resultObj = GetTaskResultFromObject(responseTask);
                    ExtractDTHeadersIfAvailable(resultObj);
                }
                finally
                {
                    segment.End();
                }
            }
        }

        return Delegates.GetDelegateFor<object>(
            onFailure: transaction.NoticeError,
            onComplete: () => segment.End(),
            onSuccess: ExtractDTHeadersIfAvailable);


        void ExtractDTHeadersIfAvailable(object resultObj)
        {
            if (resultObj != null)
            {
                switch (instrumentedMethodCall.MethodCall.Method.MethodName)
                {
                    case "ReceiveMessagesAsync":
                    case "ReceiveDeferredMessagesAsync":
                    case "PeekMessagesInternalAsync":
                        // if the response contains a list of messages,
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
            IEnumerable<string> ProcessHeaders(ReadOnlyDictionary<string, object> applicationProperties, string key)
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
    }

    private static object GetTaskResultFromObject(object taskObj)
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

        var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
        return getResponse(task);
    }
}
