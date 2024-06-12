// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusReceiveMessage
    /// </summary>
    public class ReceiveMessageWrapper : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "ReceiveMessageWrapper";

        private const int IncomingContextIndex = 0;

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var incomingContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<object>(IncomingContextIndex);

            var logicalMessage = NServiceBusHelpers.GetIncomingLogicalMessage(incomingContext);
            if (logicalMessage == null)
            {
                throw new NullReferenceException("logicalMessage");
            }

            var queueName = NServiceBusHelpers.TryGetQueueNameReceiveMessage(logicalMessage);

            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                brokerVendorName: BrokerVendorName,
                destination: queueName);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, BrokerVendorName, queueName);

            var headers = NServiceBusHelpers.GetHeadersReceiveMessage(logicalMessage);
            NServiceBusHelpers.ProcessHeaders(headers, agent);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });
        }

    }
}
