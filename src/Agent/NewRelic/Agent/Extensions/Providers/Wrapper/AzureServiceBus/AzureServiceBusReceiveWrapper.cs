// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public class AzureServiceBusReceiveWrapper : AzureServiceBusWrapperBase
{
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
        dynamic serviceBusReceiver = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusReceiver.EntityPath; // some-queue-name
        string fqns = serviceBusReceiver.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net

        _innerReceiverAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(serviceBusReceiver.GetType(), "InnerReceiver");
        object innerReceiver = _innerReceiverAccessor.Invoke(serviceBusReceiver);

        // use reflection to access the _isProcessor field of the inner receiver
        _innerReceiverIsProcessorAccessor ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<bool>(innerReceiver.GetType(), "_isProcessor");
        var isProcessor = _innerReceiverIsProcessorAccessor.Invoke(innerReceiver);

        MessageBrokerAction action =
            instrumentedMethodCall.MethodCall.Method.MethodName switch
            {
                "ReceiveMessagesAsync" => MessageBrokerAction.Consume,
                "ReceiveDeferredMessagesAsync" => MessageBrokerAction.Consume,
                "PeekMessagesInternalAsync" => MessageBrokerAction.Peek,
                "AbandonMessageAsync" => MessageBrokerAction.Purge, // TODO is this correct ??? Abandon sends the message back to the queue for re-delivery
                "CompleteMessageAsync" => MessageBrokerAction.Consume,
                "DeadLetterInternalAsync" => MessageBrokerAction.Purge,  // TODO is this correct ???
                "DeferMessageAsync" => MessageBrokerAction.Consume, // TODO is this correct or should we extend MessageBrokerAction with more values???
                "RenewMessageLockAsync" => MessageBrokerAction.Consume, // TODO is this correct or should we extend MessageBrokerAction with more values???
                _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unexpected instrumented method call: {instrumentedMethodCall.MethodCall.Method.MethodName}")
            };

        // if the inner receiver is configured as a processor and this is a ReceiveMessagesAsync call, start a transaction
        // the transaction will end at the conclusion of ReceiverManager.ProcessOneMessage()
        if (isProcessor && instrumentedMethodCall.MethodCall.Method.MethodName == "ReceiveMessagesAsync")
        {
            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                BrokerVendorName,
                destination: queueName);
        }

        // start a message broker segment
        var segment = transaction.StartMessageBrokerSegment(
            instrumentedMethodCall.MethodCall,
            MessageBrokerDestinationType.Queue,
            action,
            BrokerVendorName,
            queueName,
            serverAddress: fqns);

        var instrumentedMethodName = instrumentedMethodCall.MethodCall.Method.MethodName;

        return instrumentedMethodCall.IsAsync
            ?
            // return an async delegate
            Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                true,
                (responseTask) =>
                {
                    try
                    {
                        HandleReceiveResponse(responseTask, instrumentedMethodCall.MethodCall.Method.MethodName, transaction);
                    }
                    finally
                    {
                        segment.End();
                    }
                },
                TaskContinuationOptions.ExecuteSynchronously)
            : Delegates.GetDelegateFor<object>(
                onFailure: transaction.NoticeError,
                onComplete: segment.End,
                onSuccess: (resultObj) => ExtractDTHeadersIfAvailable(resultObj, transaction, instrumentedMethodName));
    }
}
