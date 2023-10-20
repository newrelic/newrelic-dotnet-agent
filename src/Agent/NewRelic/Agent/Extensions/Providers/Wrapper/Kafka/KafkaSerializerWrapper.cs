// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class KafkaSerializerWrapper : IWrapper
    {
        private const string WrapperName = "KafkaSerializerWrapper";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // Serialize has 2 args, Deserialize has 3
            var context = instrumentedMethodCall.MethodCall.MethodArguments.Length == 2
                ? (SerializationContext)instrumentedMethodCall.MethodCall.MethodArguments[1]
                : (SerializationContext)instrumentedMethodCall.MethodCall.MethodArguments[2];

            // MessageBroker/Kafka/Topic/Named/{topic_name}/Serialization/Value
            var segment = transaction.StartMessageBrokerSerializationSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Produce, "Kafka", context.Topic, context.Component.ToString());

            return Delegates.GetDelegateFor(segment);
        }
    }
}
