// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// This wrapper instruments message receive for NServiceBus v6+ library.
    /// </summary>
    public class LoadHandlersConnectorWrapper : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "LoadHandlersConnectorWrapper";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var incomingLogicalMessageContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

            var message = NServiceBusHelpers.GetMessageFromIncomingLogicalMessageContext(incomingLogicalMessageContext);
            if (message == null)
            {
                throw new NullReferenceException("logicalMessage");
            }

            var queueName = NServiceBusHelpers.TryGetQueueNameLoadHandlersConnector(message);

            //If the transaction does not exist.
            if (!transaction.IsValid)
            {
                transaction = agent.CreateTransaction(
                    destinationType: MessageBrokerDestinationType.Queue,
                    brokerVendorName: BrokerVendorName,
                    destination: queueName);

                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            var headers = NServiceBusHelpers.GetHeadersFromIncomingLogicalMessageContext(incomingLogicalMessageContext);
            NServiceBusHelpers.ProcessHeaders(headers, agent);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, BrokerVendorName, queueName);

            void OnComplete(Task task)
            {
                if (task == null)
                {
                    return;
                }

                if (task.Status == TaskStatus.Faulted)
                {
                    transaction.NoticeError(task.Exception);
                }

                if (task.Status == TaskStatus.RanToCompletion
                    || task.Status == TaskStatus.Canceled
                    || task.Status == TaskStatus.Faulted)
                {
                    segment.End();
                    transaction.End();
                }
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, false, OnComplete);
        }
    }
}
