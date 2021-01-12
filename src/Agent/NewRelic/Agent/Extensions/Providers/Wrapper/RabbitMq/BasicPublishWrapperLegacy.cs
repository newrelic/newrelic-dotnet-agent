// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class BasicPublishWrapperLegacy : IWrapper
    {
        private const int BasicPropertiesIndex = 4;

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "RabbitMQ.Client", typeName: "RabbitMQ.Client.Framing.Impl.Model",
                methodSignatures: new[]
                {
                    new MethodSignature("_Private_BasicPublish","System.String,System.String,System.Boolean,System.Boolean,RabbitMQ.Client.IBasicProperties,System.Byte[]"), // 3.5.X
				});
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // 3.5.X (IModel)void BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, IBasicProperties basicProperties, byte[] body)
            var segment = RabbitMqHelper.CreateSegmentForPublishWrappers(instrumentedMethodCall, transaction, BasicPropertiesIndex);
            return Delegates.GetDelegateFor(segment);
        }
    }
}
