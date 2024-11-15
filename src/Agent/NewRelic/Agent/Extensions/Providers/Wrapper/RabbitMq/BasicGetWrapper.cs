// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class BasicGetWrapper : IWrapper
    {
        private const string WrapperName = "BasicGetWrapper";
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
            var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                destType,
                MessageBrokerAction.Consume,
                RabbitMqHelper.VendorName,
                destName,
                serverAddress: RabbitMqHelper.GetServerAddress(instrumentedMethodCall, agent),
                serverPort: RabbitMqHelper.GetServerPort(instrumentedMethodCall, agent),
                routingKey: queue); // no way to get routing key from BasicGet

            return instrumentedMethodCall.IsAsync ?
                Delegates.GetAsyncDelegateFor<Task>(
                    agent, segment, false,
                    onComplete: (t) =>
                    {
                        if (t.IsFaulted)
                        {
                            transaction.NoticeError(t.Exception);
                        }

                        segment.End();
                    })
                :
                Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: segment.End
            );
        }
    }
}
