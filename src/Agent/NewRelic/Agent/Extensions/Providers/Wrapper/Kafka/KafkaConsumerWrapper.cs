// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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

        public bool IsTransactionRequired => true;

        private static readonly ConcurrentDictionary<Type, Func<object, string>> TopicAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, string>>();

        private static readonly ConcurrentDictionary<Type, Func<object, object>> MessageAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> KeyAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> ValueAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> OffsetAccessorDictionary =
            new ConcurrentDictionary<Type, Func<object, object>>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Consume, BrokerVendorName, "unknown");

            // if the overload that takes a CancellationToken was used, then we want to make this a leaf segment
            // because that overload calls the overload that takes an int (timeout) many times in a loop until the token is cancelled or a message is received
            // and we don't want to create a segment for each of those calls.
            var isCancellationTokenOverload = instrumentedMethodCall.MethodCall.MethodArguments[0] is CancellationToken;
            if (isCancellationTokenOverload)
                ((ISegmentExperimental)segment).MakeLeaf();

            return Delegates.GetDelegateFor<object>(onSuccess: (resultAsObject) =>
            {
                try
                {
                    // null is a valid return value, so we have to handle it.
                    if (resultAsObject == null) 
                    {
                        return;
                    }

                    // result is actually ConsumeResult<TKey, TValue> - but, because of the generic parameters,
                    // we have to reference it as object so we can use VisibilityBypasser on it
                    var type = resultAsObject.GetType();

                    // get the topic
                    var topicAccessor = TopicAccessorDictionary.GetOrAdd(type, GetTopicAccessorFunc);
                    string topic = topicAccessor(resultAsObject);

                    // set the segment name now that we have the topic
                    segment.SetMessageBrokerDestination(topic);

                    if (KafkaHelper.TryGetBootstrapServersFromCache(instrumentedMethodCall.MethodCall.InvocationTarget, out var bootstrapServers))
                    {
                        KafkaHelper.RecordKafkaNodeMetrics(agent, topic, bootstrapServers, false);
                    }

                    // get the Message.Headers property and process distributed trace headers
                    var messageAccessor = MessageAccessorDictionary.GetOrAdd(type, GetMessageAccessorFunc);
                    var messageAsObject = messageAccessor(resultAsObject);

                    var headersSize = 0L;
                    if (messageAsObject is MessageMetadata messageMetaData)
                    {
                        headersSize = GetHeadersSize(messageMetaData.Headers);
                        transaction.AcceptDistributedTraceHeaders(messageMetaData, DistributedTraceHeadersGetter, TransportType.Kafka);
                    }

                    ReportSizeMetrics(agent, transaction, topic, headersSize, messageAsObject);
                    ReportOffsetMetrics(agent, transaction, topic, resultAsObject);
                }
                finally
                {
                    // need to guarantee that the segment is ended
                    segment.End();
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

        private static void ReportOffsetMetrics(IAgent agent, ITransaction transaction, string topic, object resultAsObject)
        {
            // get the message Key and Value properties so we can try to get their size
            var resultType = resultAsObject.GetType();
            var offsetAccessor = OffsetAccessorDictionary.GetOrAdd(resultType, GetOffsetAccessorFunc);

            var offsetAsObject = offsetAccessor(resultAsObject);

            var offsetValueAccessor = ValueAccessorDictionary.GetOrAdd(offsetAsObject.GetType(), GetValueAccessorFunc);

            var offsetValueAsObject = offsetValueAccessor(offsetAsObject);

            // This makes sense, I think
            transaction.AddCustomAttribute("kafka.consume.offset", offsetValueAsObject);

            // Add metric for offset value - not sure if this makes any sense at all
            var agentExp = agent.GetExperimentalApi();
            agentExp.RecordCountMetric($"Message/Kafka/Topic/Named/{topic}/Offset", (long)offsetValueAsObject);
        }

        private static Func<object, string> GetTopicAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Topic");
        private static Func<object, object> GetMessageAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Message");
        private static Func<object, object> GetKeyAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Key");
        private static Func<object, object> GetValueAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Value");
        private static Func<object, object> GetOffsetAccessorFunc(Type t) =>
            VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Offset");

        private static IEnumerable<string> DistributedTraceHeadersGetter(MessageMetadata carrier, string key)
        {
            if (carrier.Headers != null)
            {
                var headerValues = new List<string>();
                foreach (var item in carrier.Headers)
                {
                    if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        var decodedHeaderValue = Encoding.UTF8.GetString(item.GetValueBytes());
                        headerValues.Add(decodedHeaderValue);
                    }
                }
                return headerValues;
            }
            return null;
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
