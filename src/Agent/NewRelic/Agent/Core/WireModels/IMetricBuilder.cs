// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Core;
using System;
using System.Net;

namespace NewRelic.Agent.Core.WireModels
{
    public interface IMetricBuilder
    {
        MetricWireModel TryBuildMemoryPhysicalMetric(long memoryPhysical);

        MetricWireModel TryBuildMemoryWorkingSetMetric(long memoryWorkingSet);

        MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime);

        MetricWireModel TryBuildCpuUserUtilizationMetric(float cpuUtilization);

        MetricWireModel TryBuildCpuTimeRollupMetric(bool isWebTransaction, TimeSpan cpuTime);

        MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime);

        MetricWireModel TryBuildGCBytesMetric(GCSampleType sampleType, long value);

        MetricWireModel TryBuildGCCountMetric(GCSampleType sampleType, int value);

        MetricWireModel TryBuildGCPercentMetric(GCSampleType sampleType, float value);

        MetricWireModel TryBuildGCGaugeMetric(GCSampleType sampleType, float value);

        MetricWireModel TryBuildCATSupportabilityCountMetric(CATSupportabilityCondition conditionType, int count);

        MetricWireModel TryBuildDotnetFrameworkVersionMetric(DotnetFrameworkVersion version);

        MetricWireModel TryBuildDotnetCoreVersionMetric(DotnetCoreVersion version);

        MetricWireModel TryBuildAgentVersionMetric(string agentVersion);

        MetricWireModel TryBuildAgentVersionByHostMetric(string hostName, string agentVersion);

        MetricWireModel TryBuildThreadpoolUsageStatsMetric(ThreadType type, ThreadStatus status, int countThreadpoolThreads);

        MetricWireModel TryBuildThreadpoolThroughputStatsMetric(ThreadpoolThroughputStatsType type, int statsVal);

        MetricWireModel TryBuildLibraryVersionMetric(string assemblyName, string assemblyVersion);

        MetricWireModel TryBuildMetricHarvestAttemptMetric();

        MetricWireModel TryBuildTransactionEventReservoirResizedMetric();

        MetricWireModel TryBuildTransactionEventsRecollectedMetric(int eventsRecollected);

        MetricWireModel TryBuildTransactionEventsSentMetric(int eventCount);

        MetricWireModel TryBuildTransactionEventsSeenMetric();

        MetricWireModel TryBuildTransactionEventsCollectedMetric();

        MetricWireModel TryBuildCustomEventReservoirResizedMetric();

        MetricWireModel TryBuildCustomEventsRecollectedMetric(int eventsRecollected);

        MetricWireModel TryBuildCustomEventsSentMetric(int eventCount);

        MetricWireModel TryBuildCustomEventsSeenMetric();

        MetricWireModel TryBuildCustomEventsCollectedMetric();

        MetricWireModel TryBuildErrorTracesCollectedMetric();

        MetricWireModel TryBuildErrorTracesRecollectedMetric(int errorTracesRecollected);

        MetricWireModel TryBuildErrorTracesSentMetric(int errorTraceCount);

        MetricWireModel TryBuildErrorEventsSentMetric(int eventCount);

        MetricWireModel TryBuildErrorEventsSeenMetric();

        MetricWireModel TryBuildSqlTracesRecollectedMetric(int sqlTracesRecollected);

        MetricWireModel TryBuildSqlTracesSentMetric(int sqlTraceCount);

        MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, string additionalData = null);

        MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, string wrapperName, string typeName, string methodName);

        MetricWireModel TryBuildFeatureEnabledMetric(string featureName);

        MetricWireModel TryBuildAgentApiMetric(string methodName, int count);

        MetricWireModel TryBuildCustomTimingMetric(string suffix, TimeSpan time);

        MetricWireModel TryBuildCustomCountMetric(string suffix, int count = 1);

        MetricWireModel TryBuildLinuxOsMetric(bool isLinux);

        MetricWireModel TryBuildBootIdError();

        MetricWireModel TryBuildKubernetesUsabilityError();

        MetricWireModel TryBuildAwsUsabilityError();

        MetricWireModel TryBuildAzureUsabilityError();

        MetricWireModel TryBuildPcfUsabilityError();

        MetricWireModel TryBuildGcpUsabilityError();

        MetricWireModel TryBuildAgentTimingMetric(string suffix, TimeSpan time);

        // Distributed Tracing (New Relic Payload)
        /// <summary>Created when AcceptDistributedTracePayload was called successfully</summary>
        MetricWireModel TryBuildAcceptPayloadSuccess(int count);

        /// <summary>Created when AcceptDistributedTracePayload had a generic exception</summary>
        MetricWireModel TryBuildAcceptPayloadException { get; }

        /// <summary>Created when AcceptDistributedTracePayload had a parsing exception</summary>
        MetricWireModel TryBuildAcceptPayloadParseException { get; }

        /// <summary>Created when AcceptDistributedTracePayload was ignored because CreatePayload had already been called</summary>
        MetricWireModel TryBuildAcceptPayloadIgnoredCreateBeforeAccept { get; }

        /// <summary>Created when AcceptDistributedTracePayload was ignored because AcceptPayload had already been called</summary>
        MetricWireModel TryBuildAcceptPayloadIgnoredMultiple { get; }

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload's major version was greater than the agent's</summary>
        MetricWireModel TryBuildAcceptPayloadIgnoredMajorVersion { get; }

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was null</summary>
        MetricWireModel TryBuildAcceptPayloadIgnoredNull { get; }

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was untrusted</summary>
        MetricWireModel TryBuildAcceptPayloadIgnoredUntrustedAccount();

        /// <summary>Created when CreateDistributedTracePayload was called successfully</summary>
        MetricWireModel TryBuildCreatePayloadSuccess(int count);

        /// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
        MetricWireModel TryBuildCreatePayloadException { get; }

        //Tracestate

        /// <summary>The agent successfully accepted inbound traceparent and tracestate headers.</summary>
        MetricWireModel TryBuildTraceContextAcceptSuccess(int count);

        /// <summary>The agent successfully created the outbound payloads.</summary>
        MetricWireModel TryBuildTraceContextCreateSuccess(int count);

        /// <summary>A generic exception occurred unrelated to parsing while accepting either payload.</summary>
        MetricWireModel TryBuildTraceContextAcceptException { get; }

        /// <summary>The inbound traceparent header could not be parsed.</summary>
        MetricWireModel TryBuildTraceContextTraceParentParseException { get; }

        /// <summary>The inbound tracestate header could not be parsed.</summary>
        MetricWireModel TryBuildTraceContextTraceStateParseException { get; }

        /// <summary>A generic exception occurred while creating the outbound payloads.</summary>
        MetricWireModel TryBuildTraceContextCreateException { get; }

        /// <summary>The inbound tracestate header exists, and was accepted, but the New Relic entry was invalid.</summary>
        MetricWireModel TryBuildTraceContextTraceStateInvalidNrEntry { get; }

        /// <summary>The traceparent header exists, and was accepted, but the tracestate header did not contain a trusted New Relic entry.</summary>
        MetricWireModel TryBuildTraceContextTraceStateNoNrEntry { get; }

        MetricWireModel TryBuildSupportabilityErrorHttpStatusCodeFromCollector(HttpStatusCode statusCode);

        MetricWireModel TryBuildSupportabilityEndpointMethodErrorAttempts(string endpointMethod);

        MetricWireModel TryBuildSupportabilityEndpointMethodErrorDuration(string endpointMethod, TimeSpan duration);

        MetricWireModel TryBuildSpanEventsSeenMetric(int count);

        MetricWireModel TryBuildSpanEventsSentMetric(int count);

        MetricWireModel TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit(string endpoint, int count = 1);

        MetricWireModel TryBuildInstallTypeMetric(string installType);

        MetricWireModel TryBuildSupportabilityCountMetric(string metricName, long count = 1);

        MetricWireModel TryBuildSupportabilityDataUsageMetric(string metricName, long callCount, float dataSent, float dataReceived);

        MetricWireModel TryBuildSupportabilitySummaryMetric(string metricName, float totalValue, int countSamples, float minValue, float maxValue);

        MetricWireModel TryBuildSupportabilityGaugeMetric(string metricName, float value);

        MetricWireModel TryBuildLoggingMetricsLinesCountBySeverityMetric(string logLevel, int count);

        MetricWireModel TryBuildLoggingMetricsLinesCountMetric(int count);

        MetricWireModel TryBuildLoggingMetricsDeniedCountBySeverityMetric(string logLevel, int count);

        MetricWireModel TryBuildLoggingMetricsDeniedCountMetric(int count);

        MetricWireModel TryBuildSupportabilityLoggingEventsCollectedMetric();

        MetricWireModel TryBuildSupportabilityLoggingEventsSentMetric(int loggingEventCount);

        MetricWireModel TryBuildSupportabilityLoggingEventsDroppedMetric(int droppedCount);
    }
}
