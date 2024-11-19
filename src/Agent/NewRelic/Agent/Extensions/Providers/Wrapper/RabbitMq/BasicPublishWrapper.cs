// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class BasicPublishWrapper : IWrapper
    {
        private const string WrapperName = "BasicPublishWrapper";

        private const int BasicPropertiesIndex = 3;

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // 3.6.0+ (5.1.0+) (IModel)void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, byte[] body)
            // v7+:
            // public async ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            //   bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            //   CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            var rabbitMqVersion = RabbitMqHelper.GetRabbitMQVersion(instrumentedMethodCall);

            var segment = (rabbitMqVersion >= 6) ?
                RabbitMqHelper.CreateSegmentForPublishWrappers6Plus(instrumentedMethodCall, transaction, agent)
                    :
                RabbitMqHelper.CreateSegmentForPublishWrappers(instrumentedMethodCall, transaction, agent);

            if (rabbitMqVersion >= 6)
                RabbitMqHelper.InsertDTHeaders6Plus(instrumentedMethodCall, transaction, BasicPropertiesIndex);
            else
                RabbitMqHelper.InsertDTHeaders(instrumentedMethodCall, transaction, BasicPropertiesIndex);


            // TODO: Can we handle ValueTask<T> return type somehow? Without it, we can't properly manage a message broker segment that wraps the publish call
            return instrumentedMethodCall.IsAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment, false, (_) =>
                {
                    segment.End(); // TODO: this never gets called because the delegate is never invoked
                })
              : Delegates.GetDelegateFor(segment);
        }
    }
}
