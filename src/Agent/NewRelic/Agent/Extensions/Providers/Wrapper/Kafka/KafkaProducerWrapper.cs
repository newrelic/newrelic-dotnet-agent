// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Kafka;

public class KafkaProducerWrapper : IWrapper
{
    private const string WrapperName = "KafkaProducerWrapper";

    private static readonly ClusterIdLookup ClusterIdLookupFunc = KafkaClusterIdResolver.TryGetClusterId;

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var topicPartition = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<TopicPartition>(0);
        var messageMetadata = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<MessageMetadata>(1);

        Log.Finest("KafkaProducerWrapper: BeforeWrappedMethod called, topic: '{0}', partition: {1}", topicPartition.Topic, topicPartition.Partition);

        var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Produce, MessageBrokerVendorConstants.Kafka, topicPartition.Topic);

        transaction.InsertDistributedTraceHeaders(messageMetadata, DistributedTraceHeadersSetter);

        if (KafkaHelper.TryGetClientInfo(instrumentedMethodCall.MethodCall.InvocationTarget, out var clientInfo))
        {
            KafkaHelper.RecordKafkaNodeMetrics(agent, topicPartition.Topic, clientInfo, true);

            KafkaClusterIdResolver.RefreshClientReference(clientInfo.BootstrapServers, instrumentedMethodCall.MethodCall.InvocationTarget);
            KafkaClusterMetricsHelper.RecordClusterMetrics(agent, segment, clientInfo.BootstrapServers, topicPartition.Topic, KafkaClusterOperation.Produce, ClusterIdLookupFunc);
        }

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
