// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureServiceBus
{
    public class AzureServiceBusReceiverManagerWrapper : AzureServiceBusWrapperBase
    {
        private Func<object, object> _receiverAccessor;
        public override bool IsTransactionRequired => true;

        public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiverManagerWrapper));
            return new CanWrapResponse(canWrap);
        }

        public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,
            ITransaction transaction)
        {
            var receiverManager = instrumentedMethodCall.MethodCall.InvocationTarget;
            _receiverAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(receiverManager.GetType(), "Receiver");
            dynamic receiver = _receiverAccessor(receiverManager);

            string queueName = receiver.EntityPath; // some-queue-name
            string fqns = receiver.FullyQualifiedNamespace; // some-service-bus-entity.servicebus.windows.net

            if (instrumentedMethodCall.IsAsync)
                transaction.AttachToAsync();

            // start a new MessageBroker segment that wraps ProcessOneMessageWithinScopeAsync
            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                MessageBrokerDestinationType.Queue,
                MessageBrokerAction.Process, // TODO: This is a new action, added for this instrumentation
                BrokerVendorName,
                queueName,
                serverAddress: fqns);

            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(
                    agent,
                    segment,
                    true,
                    onComplete: _ =>
                    {
                        segment.End();
                        transaction.End();
                    });
            }
            return Delegates.GetDelegateFor(onComplete: () =>
            {
                segment.End();
                transaction.End();
            });
        }
    }
}
