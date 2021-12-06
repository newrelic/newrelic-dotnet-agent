// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// This wrapper instruments message receive for NServiceBus v6+ library.
    /// </summary>
    public class PipelineWrapper : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "PipelineWrapper";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var outgoingContext = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var typeName = outgoingContext.GetType().FullName;

            if (typeName == NServiceBusHelpers.OutgoingSendContextTypeName || typeName == NServiceBusHelpers.OutgoingPublishContextTypeName)
            {
                transaction.AttachToAsync();

                var message = NServiceBusHelpers.GetMessageFromOutgoingContext(outgoingContext);

                var queueName = NServiceBusHelpers.TryGetQueueName(message);
                var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, BrokerVendorName, queueName);

                NServiceBusHelpers.CreateOutboundHeaders(agent, outgoingContext);
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
            }

            return Delegates.NoOp;
        }
    }
}
