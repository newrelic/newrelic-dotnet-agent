// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureServiceBus
{
    public class AzureServiceBusReceiverManagerWrapper : AzureServiceBusWrapperBase
    {
        private static Func<object, object> _receiverAccessor;
        private static Func<object, string> _entityPathAccessor;
        private static Func<object, string> _fullyQualifiedNamespaceAccessor;
        private static bool _badReceiverWarningLogged;

        public override bool IsTransactionRequired => false;

        public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiverManagerWrapper));
            return new CanWrapResponse(canWrap);
        }

        public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            string queueOrTopicName = null;
            string fqns = null;

            var receiverManager = instrumentedMethodCall.MethodCall.InvocationTarget;
            var receiverManagerType = receiverManager.GetType();
            _receiverAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(receiverManagerType, "Receiver");
            object receiver = _receiverAccessor(receiverManager);
            if (receiver == null)
            {
                if (!_badReceiverWarningLogged)
                {
                    agent.Logger.Warn("AzureServiceBusReceiverManagerWrapper: Unable to access Receiver property on ReceiverManager instance of type {ReceiverManagerType}. Unable to access queue/topic name or fully qualified namespace.", receiverManagerType);
                    _badReceiverWarningLogged = true;
                }
            }
            else
            {
                _entityPathAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(receiver.GetType(), "EntityPath");
                _fullyQualifiedNamespaceAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(receiver.GetType(), "FullyQualifiedNamespace");

                queueOrTopicName = _entityPathAccessor(receiver)?.ToString(); // some-queue|topic-name
                fqns = _fullyQualifiedNamespaceAccessor(receiver)?.ToString(); // some-service-bus-entity.servicebus.windows.net
            }

            var destinationType = GetMessageBrokerDestinationType(queueOrTopicName);

            // create a transaction for this method call. This method invokes the ProcessMessageAsync handler
            transaction = agent.CreateTransaction(
                destinationType: destinationType,
                BrokerVendorName,
                destination: queueOrTopicName);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.DetachFromPrimary();
                transaction.AttachToAsync();
            }

            // extract DT headers from the message and create a distributed trace payload
            ExtractAndAcceptDistributedTracePayload(agent, transaction, instrumentedMethodCall.MethodCall.MethodArguments[0]);

            // start a new MessageBroker segment that wraps ProcessOneMessage
            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                destinationType,
                MessageBrokerAction.Process,
                BrokerVendorName,
                GetQueueOrTopicName(destinationType, queueOrTopicName),
                serverAddress: fqns);

            return instrumentedMethodCall.IsAsync
                ?
                Delegates.GetAsyncDelegateFor<Task>(
                    agent,
                    segment,
                    false,
                    onComplete: _ =>
                    {
                        segment.End();
                        transaction.End();
                    }, TaskContinuationOptions.ExecuteSynchronously)
                :
                Delegates.GetDelegateFor(onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });
        }

        private void ExtractAndAcceptDistributedTracePayload(IAgent agent, ITransaction transaction, object receivedMessage)
        {
            dynamic msg = receivedMessage;
            if (msg.ApplicationProperties is ReadOnlyDictionary<string, object> applicationProperties)
            {
                transaction.LogFinest("ReceiveManagerWrapper: Accepting distributed trace headers");
                transaction.AcceptDistributedTraceHeaders(applicationProperties, ProcessHeaders, TransportType.Queue);
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
}
