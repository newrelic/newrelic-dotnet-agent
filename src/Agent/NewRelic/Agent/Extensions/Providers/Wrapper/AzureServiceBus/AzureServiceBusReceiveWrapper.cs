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

    private Func<object, object> _innerReceiverAccessor;
    private Func<object, bool> _innerReceiverIsProcessorAccessor;

    public override bool IsTransactionRequired => false; // only partially true. See the code below...

    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiveWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        transaction.LogFinest("AzureServiceBusReceiveWrapper.BeforeWrappedMethod() is starting.");

        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath; // some-queue-name
        string fqns = serviceBusReceiver.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net

        _innerReceiverAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(serviceBusReceiver.GetType(), "InnerReceiver");
        object innerReceiver = _innerReceiverAccessor.Invoke(serviceBusReceiver);

        // use reflection to access the _isProcessor field of the inner receiver
        _innerReceiverIsProcessorAccessor ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<bool>(innerReceiver.GetType(), "_isProcessor");
        var isProcessor = _innerReceiverIsProcessorAccessor.Invoke(innerReceiver);

        var instrumentedMethodName = instrumentedMethodCall.MethodCall.Method.MethodName;

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
                "RenewMessageLockAsync" => MessageBrokerAction.Consume, // TODO This doesn't quite fit. OTEL uses a default action with no name for this
                _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unexpected instrumented method call: {instrumentedMethodName}")
            };

        // If the inner receiver is configured as a processor and this is a ReceiveMessagesAsync call, start a transaction.
        // The transaction will end at the conclusion of ReceiverManager.ProcessOneMessageWithinScopeAsync()
        if (isProcessor && instrumentedMethodName == "ReceiveMessagesAsync")
        {
            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                BrokerVendorName,
                destination: queueName);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.DetachFromPrimary();
            }

            transaction.LogFinest("Created transaction for ReceiveMessagesAsync in processor mode.");
        }
        else
        {
            transaction = agent.CurrentTransaction;
            transaction.LogFinest($"Using existing transaction for {instrumentedMethodName}.");
        }

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }


        // start a message broker segment (only happens if transaction is not NoOpTransaction)
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            MessageBrokerDestinationType.Queue,
            action,
            BrokerVendorName,
            queueName,
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

                        HandleReceiveResponse(responseTask, instrumentedMethodName, transaction, isProcessor);
                    }
                    catch (Exception ex)
                    {
                        transaction.LogFinest($"Unexpected exception: {ex.Message}");
                    }
                    finally
                    {
                        transaction.LogFinest($"Ending segment for {instrumentedMethodName}.");
                        segment.End();

                        if (isProcessor && responseTask.IsCanceled)
                        {
                            transaction.LogFinest("ReceiveMessagesAsync task was canceled in processor mode. Ignoring transaction.");
                            transaction.Ignore();
                        }
                    }
                },
                TaskContinuationOptions.ExecuteSynchronously)
            :
            Delegates.GetDelegateFor<object>(
                onFailure: transaction.NoticeError,
                onComplete: segment.End,
                onSuccess: (resultObj) => ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName, isProcessor));
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

    private static void HandleReceiveResponse(Task responseTask, string instrumentedMethodName, ITransaction transaction, bool isProcessor)
    {
        var resultObj = GetTaskResultFromObject(responseTask);
        ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName, isProcessor);
    }
    private static void ExtractDTHeadersIfAvailable(object resultObj, ITransaction transaction, string instrumentedMethodName, bool isProcessor)
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
                        transaction.LogFinest($"Received {messages.Count} message(s). Accepting DT headers.");
                        var msg = messages[0];
                        if (msg.ApplicationProperties is ReadOnlyDictionary<string, object> applicationProperties)
                        {
                            transaction.AcceptDistributedTraceHeaders(applicationProperties, ProcessHeaders, TransportType.Queue);
                        }
                    }
                    else if (messages.Count == 0 && isProcessor) // if there are no messages and the receiver is a processor, ignore the transaction we created
                    {
                        transaction.LogFinest("No messages received. Ignoring transaction.");
                        transaction.Ignore();
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
