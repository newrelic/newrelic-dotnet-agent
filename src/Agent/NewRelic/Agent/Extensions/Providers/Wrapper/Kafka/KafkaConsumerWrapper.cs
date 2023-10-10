// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
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

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Consume, "Kafka");

            //return Delegates.GetDelegateFor<ConsumeResult<string, string>>(
            return Delegates.GetDelegateFor<dynamic>(
                onSuccess: (result) =>
                {
                    if (result == null)
                        return;

                    var setHeaders = new Action<Headers, string, string>((carrier, key, value) =>
                    {
                        carrier ??= new Headers();
                        carrier.Add(key, Encoding.ASCII.GetBytes(value));
                    });

                    transaction.InsertDistributedTraceHeaders(result.Message.Headers, setHeaders);

                    string topic = result.Topic;

                    segment.SegmentNameOverride = "MessageBroker/Kafka/Topic/Consume/" + topic;
                    segment.End();
                });
        }
    }
}
