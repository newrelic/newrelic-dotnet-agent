// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class QueuePurgeWrapper : IWrapper
    {
        private const string WrapperName = "QueuePurgeWrapper";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // (IModel) uint QueuePurge(string queue)
            var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
            var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Purge, RabbitMqHelper.VendorName, destName);
            return Delegates.GetDelegateFor(segment);
        }
    }
}
