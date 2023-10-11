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
        private const string BrokerVendorName = "Kafka";

        public bool IsTransactionRequired => false;

        private static readonly ConcurrentDictionary<Type, Func<object, string>> TopicAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, string>>();

        private static readonly ConcurrentDictionary<Type, Func<object, object>> MessageAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // report kafka client version in a supportability metric, one time only
           KafkaSupportabilityMetricReporter.ReportKafkaSupportabilityMetric(agent, instrumentedMethodCall.MethodCall.Method.Type);

            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Topic,
                brokerVendorName: BrokerVendorName,
                destination: "unknown"); // placeholder since the topic name is unknown at this point

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Consume, "Kafka", "unknown");

            return Delegates.GetDelegateFor<object>(onSuccess: (resultAsObject) =>
            {
                if (resultAsObject == null) // null is a valid return value, so we have to handle it. 
                {
                    // TODO: Uncommenting the following lines results in *no* transactions for kafka consume. No idea why.
                    //segment.End();
                    //transaction.Ignore(); // nothing to see here, move along
                    return;
                }

                // result is actually ConsumeResult<TKey, TValue> - but, because of the generic parameters,
                // we have to reference it as object so we can use VisibilityBypasser on it
                var type = resultAsObject.GetType();

                // get the topic
                var topicAccessor = TopicAccessorDictionary.GetOrAdd(type, GetTopicAccessorFunc);
                string topic = topicAccessor(resultAsObject);

                // set the segment and transaction name
                segment.SetName($"MessageBroker/{BrokerVendorName}/Topic/Consume/Named/{topic}");
                transaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, BrokerVendorName, topic);

                // get the Message.Headers property and add distributed trace headers
                var messageAccessor = MessageAccessorDictionary.GetOrAdd(type, GetMessageAccessorFunc);
                var messageAsObject = messageAccessor(resultAsObject);

                if (messageAsObject is MessageMetadata messageMetaData)
                {
                    var headers = messageMetaData.Headers;

                    transaction.InsertDistributedTraceHeaders(headers, DistributedTraceHeadersSetter);
                }

                segment.End();
                transaction.End();
            });
        }

        private static Func<object, string> GetTopicAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Topic");
        private static Func<object, object> GetMessageAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Message");

        private static void DistributedTraceHeadersSetter(Headers carrier, string key, string value)
        {
            carrier ??= new Headers();
            carrier.Add(key, Encoding.ASCII.GetBytes(value));
        }
    }
}
