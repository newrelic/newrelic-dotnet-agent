// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
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

    public override bool IsTransactionRequired => true;

    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiveWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueOrTopicName = serviceBusReceiver.EntityPath; // some-queue|topic-name

        var destinationType = GetMessageBrokerDestinationType(queueOrTopicName);
        queueOrTopicName = GetQueueOrTopicName(destinationType, queueOrTopicName);

        string fqns = serviceBusReceiver.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net

        var instrumentedMethodName = instrumentedMethodCall.MethodCall.Method.MethodName;

        // OTEL naming convention for message broker actions: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#operation-types
        MessageBrokerAction action =
            instrumentedMethodName switch
            {
                "ReceiveMessagesAsync" => MessageBrokerAction.Consume,
                "ReceiveDeferredMessagesAsync" => MessageBrokerAction.Consume,
                "PeekMessagesInternalAsync" => MessageBrokerAction.Peek,
                "AbandonMessageAsync" => MessageBrokerAction.Settle,
                "CompleteMessageAsync" => MessageBrokerAction.Settle,
                "DeadLetterInternalAsync" => MessageBrokerAction.Settle,
                "DeferMessageAsync" => MessageBrokerAction.Settle,
                "RenewMessageLockAsync" => MessageBrokerAction.Consume, //  OTEL uses a default action with no name for this, but we don't have that option
                _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unexpected instrumented method call: {instrumentedMethodName}")
            };

        transaction = agent.CurrentTransaction;

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        // start a message broker segment
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            destinationType,
            action,
            BrokerVendorName,
            queueOrTopicName,
            serverAddress: fqns);

        return instrumentedMethodCall.IsAsync
            ?
            // return an async delegate
            Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                (responseTask) =>
                {
                    try
                    {
                        if (responseTask.IsFaulted)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        HandleReceiveResponse(responseTask, instrumentedMethodName, transaction);
                    }
                    finally
                    {
                        segment.End();
                    }
                },
                TaskContinuationOptions.ExecuteSynchronously)
            :
            Delegates.GetDelegateFor<object>(
                onFailure: transaction.NoticeError,
                onComplete: segment.End,
                onSuccess: (resultObj) => ExtractDtHeadersIfAvailable(resultObj, transaction, instrumentedMethodName));
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
        if (task.IsCanceled)
        {
            return null;
        }

        var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
        return getResponse(task);
    }

    private static void HandleReceiveResponse(Task responseTask, string instrumentedMethodName, ITransaction transaction)
    {
        var resultObj = GetTaskResultFromObject(responseTask);
        ExtractDtHeadersIfAvailable(resultObj, transaction, instrumentedMethodName);
    }

    // For more details on DT for this library see: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/TROUBLESHOOTING.md#distributed-tracing
    private static void ExtractDtHeadersIfAvailable(object resultObj, ITransaction transaction, string instrumentedMethodName)
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
                    int messageCount = ((ICollection)resultObj).Count;

                    dynamic messages = resultObj; // so we can index into it
                    if (messageCount > 0)
                    {
                        transaction.LogFinest($"Received {messageCount} message(s). Accepting DT headers from the first message.");
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

    private static IEnumerable<string> ProcessHeaders(ReadOnlyDictionary<string, object> applicationProperties, string key)
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
