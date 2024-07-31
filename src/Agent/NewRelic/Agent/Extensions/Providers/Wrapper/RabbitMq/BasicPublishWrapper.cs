// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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

            var segment = (RabbitMqHelper.GetRabbitMQVersion(instrumentedMethodCall) >= 6) ?
                RabbitMqHelper.CreateSegmentForPublishWrappers6Plus(instrumentedMethodCall, transaction, BasicPropertiesIndex, agent) :
                RabbitMqHelper.CreateSegmentForPublishWrappers(instrumentedMethodCall, transaction, BasicPropertiesIndex, agent);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
