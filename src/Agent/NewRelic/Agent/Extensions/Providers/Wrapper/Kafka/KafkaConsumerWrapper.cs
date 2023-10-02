// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class KafkaConsumerWrapper : IWrapper
    {
        private const string WrapperName = "KafkaConsumerWrapper";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Topic,
                brokerVendorName: "Kafka",
                destination: "");

            return Delegates.GetDelegateFor<ConsumeResult<object, object>>(
                onSuccess: (result) =>
                {
                    // Message/Kafka/Topic/Consume/Named/{topic}
                    transaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "Kafka", result.Topic);
                    transaction.End();
                });

            
            
        }
    }
}
