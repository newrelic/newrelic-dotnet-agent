// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public class AzureServiceBusProcessorWrapper : AzureServiceBusWrapperBase
{
    public override bool IsTransactionRequired => false;

    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusProcessorWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        dynamic serviceBusProcessor = instrumentedMethodCall.MethodCall.InvocationTarget;
        string queueName = serviceBusProcessor.EntityPath; // some-queue-name
        string fqns = serviceBusProcessor.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net

        transaction = agent.CreateTransaction(
            destinationType: MessageBrokerDestinationType.Queue,
            BrokerVendorName,
            destination: queueName);

        // ???
        var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, "Azure.Messaging.ServiceBus.ServiceBusProcessor", "ProcessMessageAsync");

        //// start a message broker segment ???
        //var segment = transaction.StartMessageBrokerSegment(
        //    instrumentedMethodCall.MethodCall,
        //    MessageBrokerDestinationType.Queue,
        //    MessageBrokerAction.Consume,
        //    BrokerVendorName,
        //    queueName,
        //    serverAddress: fqns);


        return instrumentedMethodCall.IsAsync ?
            Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                true, // TODO Is this correct?? 
                t =>
            {
                if (t.IsFaulted)
                {
                    transaction.NoticeError(t.Exception);
                }

                segment.End();
                transaction.End();
            }, TaskContinuationOptions.ExecuteSynchronously)
            :
            Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });
    }
}
