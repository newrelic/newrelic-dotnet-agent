// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusSendMessage
    /// </summary>
    public class SendMessageWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "SendMessageWrapper";
        private const string BrokerVendorName = "NServiceBus";

        private const string LogicalMessageType = "NServiceBus.Unicast.Messages.LogicalMessage";
        private const int LogicalMessageIndex = 1;


        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logicalMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<object>(LogicalMessageIndex);

            //If the NServiceBus version doesn't provide the LogicalMessage parameter we just bail.
            if (logicalMessage.GetType().FullName != LogicalMessageType)
            {
                return Delegates.NoOp;
            }

            var queueName = NServiceBusHelpers.TryGetQueueNameSendMessage(logicalMessage);
            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, BrokerVendorName, queueName);

            NServiceBusHelpers.CreateOutboundHeadersSendMessage(agent, logicalMessage);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
