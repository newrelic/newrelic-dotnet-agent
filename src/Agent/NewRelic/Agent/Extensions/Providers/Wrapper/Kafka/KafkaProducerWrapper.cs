// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
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

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var topicPartition = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<TopicPartition>(0);
            var messageMetadata = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<MessageMetadata>(1);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Produce, "Kafka", topicPartition.Topic);

            var setHeaders = new Action<Headers, string, string>((carrier, key, value) =>
            {
                carrier ??= new Headers();
                carrier.Add(key, Encoding.ASCII.GetBytes(value));
            });

            transaction.InsertDistributedTraceHeaders(messageMetadata.Headers, setHeaders);

            if (instrumentedMethodCall.MethodCall.Method.MethodName == "Produce")
            {
                return Delegates.GetDelegateFor(segment);
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
        }
    }
}
