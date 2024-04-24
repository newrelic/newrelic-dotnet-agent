// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class KafkaProducerWrapper : IWrapper
    {
        private const string WrapperName = "KafkaProducerWrapper";
        private const string BrokerVendorName = "Kafka";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var topicPartition = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<TopicPartition>(0);
            var messageMetadata = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<MessageMetadata>(1);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Produce, BrokerVendorName, topicPartition.Topic);

            transaction.InsertDistributedTraceHeaders(messageMetadata, DistributedTraceHeadersSetter);

            return instrumentedMethodCall.MethodCall.Method.MethodName == "Produce" ? Delegates.GetDelegateFor(segment) : Delegates.GetAsyncDelegateFor<Task>(agent, segment);
        }

        private static void DistributedTraceHeadersSetter(MessageMetadata carrier, string key, string value)
        {
            carrier.Headers ??= new Headers();
            if (!string.IsNullOrEmpty(key))
            {
                carrier.Headers.Remove(key);
                carrier.Headers.Add(key, Encoding.ASCII.GetBytes(value));
            }
        }

    }
}
