// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
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
        private static readonly ConcurrentDictionary<Type, Func<object, object>> KeyAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> ValueAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction = agent.CreateKafkaTransaction(
                destinationType: MessageBrokerDestinationType.Topic,
                brokerVendorName: BrokerVendorName,
                destination: "unknown"); // placeholder since the topic name is unknown at this point

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Consume, BrokerVendorName, "unknown");

            return Delegates.GetDelegateFor<object>(onSuccess: (resultAsObject) =>
            {
                try
                {
                    if (resultAsObject == null) // null is a valid return value, so we have to handle it. 
                    {
                        transaction.Ignore();
                        return;
                    }

                    // result is actually ConsumeResult<TKey, TValue> - but, because of the generic parameters,
                    // we have to reference it as object so we can use VisibilityBypasser on it
                    var type = resultAsObject.GetType();

                    // get the topic
                    var topicAccessor = TopicAccessorDictionary.GetOrAdd(type, GetTopicAccessorFunc);
                    string topic = topicAccessor(resultAsObject);

                    // set the segment and transaction name
                    segment.SetMessageBrokerDestination(topic);
                    transaction.SetKafkaMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, BrokerVendorName, topic);

                    // get the Message.Headers property and add distributed trace headers
                    var messageAccessor = MessageAccessorDictionary.GetOrAdd(type, GetMessageAccessorFunc);
                    var messageAsObject = messageAccessor(resultAsObject);

                    var headersSize = 0L;
                    if (messageAsObject is MessageMetadata messageMetaData)
                    {
                        headersSize = GetHeadersSize(messageMetaData.Headers);

                        transaction.InsertDistributedTraceHeaders(messageMetaData.Headers, DistributedTraceHeadersSetter);
                    }

                    ReportSizeMetrics(agent, transaction, topic, headersSize, messageAsObject);
                }
                finally
                {
                    // need to guarantee that the segment and transaction are terminated
                    segment.End();
                    transaction.End();
                }
            });
        }

        private static long GetHeadersSize(Headers headers)
        {
            var headersSize = 0L;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    headersSize += Encoding.UTF8.GetByteCount(header.Key);
                    headersSize += header.GetValueBytes().Length;
                }
            }
            return headersSize;
        }

        private static void ReportSizeMetrics(IAgent agent, ITransaction transaction, string topic, long headersSize, object messageAsObject)
        {
            // get the message Key and Value properties so we can try to get their size
            var messageType = messageAsObject.GetType();
            var keyAccessor = KeyAccessorDictionary.GetOrAdd(messageType, GetKeyAccessorFunc);
            var valueAccessor = ValueAccessorDictionary.GetOrAdd(messageType, GetValueAccessorFunc);

            var keyAsObject = keyAccessor(messageAsObject);
            var valueAsObject = valueAccessor(messageAsObject);

            var totalSize = headersSize + TryGetSize(keyAsObject) + TryGetSize(valueAsObject);

            if (totalSize > 0)
            {
                transaction.AddCustomAttribute("kafka.consume.byteCount", totalSize);
            }

            // Add metrics for bytes received and messages received
            var agentExp = agent.GetExperimentalApi();
            agentExp.RecordCountMetric($"Message/Kafka/Topic/Named/{topic}/Received/Messages", 1);
            agentExp.RecordByteMetric($"Message/Kafka/Topic/Named/{topic}/Received/Bytes", totalSize);
        }

        private static Func<object, string> GetTopicAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Topic");
        private static Func<object, object> GetMessageAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Message");
        private static Func<object, object> GetKeyAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Key");
        private static Func<object, object> GetValueAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Value");

        private static void DistributedTraceHeadersSetter(Headers carrier, string key, string value)
        {
            carrier ??= new Headers();
            carrier.Add(key, Encoding.ASCII.GetBytes(value));
        }

        private static long TryGetSize(object obj)
        {
            if (obj == null)
                return 0;

            // get the UTF8 byte count if it's a string,
            // the array length if it's a byte array
            // or zero if it's something else
            return obj is string str ? Encoding.UTF8.GetByteCount(str) :
                   obj is byte[] bytes ? bytes.Length :
                   0;
        }
    }
}
