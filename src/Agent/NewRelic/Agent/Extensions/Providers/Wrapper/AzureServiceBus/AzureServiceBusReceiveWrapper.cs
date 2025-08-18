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

    private static Func<object, object> _innerReceiverAccessor;
    private static Func<object, bool> _innerReceiverIsProcessorAccessor;

    public override bool IsTransactionRequired => false; // only partially true. See the code below...

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

        _innerReceiverAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(serviceBusReceiver.GetType(), "InnerReceiver");
        object innerReceiver = _innerReceiverAccessor.Invoke(serviceBusReceiver);

        // use reflection to access the _isProcessor field of the inner receiver
        _innerReceiverIsProcessorAccessor ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<bool>(innerReceiver.GetType(), "_isProcessor");
        var isProcessor = _innerReceiverIsProcessorAccessor.Invoke(innerReceiver);

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

        // If the inner receiver is configured as a processor and this is a ReceiveMessagesAsync call, start a transaction.
        // The transaction will end at the conclusion of ReceiverManager.ProcessOneMessageWithinScopeAsync()
        if (isProcessor && instrumentedMethodName == "ReceiveMessagesAsync")
        {
            transaction = agent.CreateTransaction(
                destinationType: destinationType,
                BrokerVendorName,
                destination: queueOrTopicName);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.DetachFromPrimary();
            }

            transaction.LogFinest("Created transaction for ReceiveMessagesAsync in processor mode.");
        }
        else
        {
            transaction = agent.CurrentTransaction;

            if (!transaction.IsValid)
            {
                // transaction is required when we're not in processor mode
                transaction.LogFinest($"No transaction. Not creating MessageBroker segment for {instrumentedMethodName}.");
                return Delegates.NoOp;
            }

            transaction.LogFinest($"Using existing transaction for {instrumentedMethodName}.");
        }

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        // start a message broker segment (only happens if transaction is not NoOpTransaction)
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
                    bool noMessagesReceived = false;
                    try
                    {
                        if (responseTask.IsFaulted)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        HandleReceiveResponse(responseTask, instrumentedMethodName, transaction, isProcessor, out noMessagesReceived);
                    }
                    finally
                    {
                        segment.End();

                        // if we are in processor mode and the task was canceled or no messages were received, ignore the transaction
                        if (isProcessor && (responseTask.IsCanceled || noMessagesReceived))
                        {
                            transaction.LogFinest($"{(responseTask.IsCanceled ? "ReceiveMessagesAsync task was canceled in processor mode" : "No messages received")}. Ignoring transaction.");

                            // Ignore and end the transaction here since end in the AzureServiceBusReceiverManagerWrapper is never called
                            // In FW this results in a transaction has been garbage collected message, probably due to different GC settings in FW vs. Core
                            transaction.Ignore();
                            transaction.End();
                        }
                    }
                },
                TaskContinuationOptions.ExecuteSynchronously)
            :
            Delegates.GetDelegateFor<object>(
                onFailure: transaction.NoticeError,
                onComplete: segment.End,
                onSuccess: (resultObj) => ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName, isProcessor, out _));
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

    private static void HandleReceiveResponse(Task responseTask, string instrumentedMethodName, ITransaction transaction, bool isProcessor, out bool noMessagesReceived)
    {
        var resultObj = GetTaskResultFromObject(responseTask);
        ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName, isProcessor, out noMessagesReceived);
    }

    // For more details on DT for this library see: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/TROUBLESHOOTING.md#distributed-tracing
    private static void ExtractDTHeadersIfAvailable(object resultObj, ITransaction transaction, string instrumentedMethodName, bool isProcessor, out bool noMessagesReceived)
    {
        noMessagesReceived = false;
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
                    else if (messageCount == 0 && isProcessor) // if there are no messages and the receiver is a processor, ignore the transaction we created
                    {
                        noMessagesReceived = true;
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
