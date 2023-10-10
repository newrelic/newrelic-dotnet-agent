// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class KafkaConsumerWrapper : IWrapper
    {
        private const string WrapperName = "KafkaConsumerWrapper";

        public bool IsTransactionRequired => false;

        private static ConcurrentDictionary<Type, Func<object, string>> topicAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, string>>();

        private static ConcurrentDictionary<Type, Func<object, object>> messageAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }


        private static Func<object, string> GetTopicAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Topic");
        private static Func<object, object> GetMessageAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Message");

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Topic,
                brokerVendorName: "Kafka",
                destination: "");

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Consume, "Kafka");

            return Delegates.GetDelegateFor<object>(onSuccess: (x) =>
            {
                if (x == null) // null can be returned, so we have to handle it. 
                {
                    // ?? Set segment name here? Possibly get topic from Consume instance? 
                    // segment.SegmentNameOverride = "MessageBroker/Kafka/Topic/Consume/" + topic;
                    segment.End();
                    return;
                }

                var type = x.GetType();

                var topicAccessor = topicAccessorDictionary.GetOrAdd(type, GetTopicAccessorFunc);
                string topic = topicAccessor(x);

                segment.SegmentNameOverride = "MessageBroker/Kafka/Topic/Consume/" + topic;

                var messageAccessor = messageAccessorDictionary.GetOrAdd(type, GetMessageAccessorFunc);
                var messageAsObject = messageAccessor(x);

                if (messageAsObject is MessageMetadata messageMetaData)
                {
                    var headers = messageMetaData.Headers;

                    var setHeaders = new Action<Headers, string, string>((carrier, key, value) =>
                    {
                        carrier ??= new Headers();
                        carrier.Add(key, Encoding.ASCII.GetBytes(value));
                    });

                    transaction.InsertDistributedTraceHeaders(headers, setHeaders);
                }

                segment.End();
            });
        }
    }
}
