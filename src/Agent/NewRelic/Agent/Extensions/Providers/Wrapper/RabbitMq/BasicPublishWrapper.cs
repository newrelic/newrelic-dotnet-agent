/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class BasicPublishWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "RabbitMQ.Client", typeName: "RabbitMQ.Client.Framing.Impl.Model", methodName: "_Private_BasicPublish");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            // (IModel)void BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, IBasicProperties basicProperties, byte[] body)
            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            var destType = RabbitMqHelper.GetBrokerDestinationType(routingKey);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, routingKey);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Produce, RabbitMqHelper.VendorName, destName);
            return Delegates.GetDelegateFor(segment);
        }
    }
}
