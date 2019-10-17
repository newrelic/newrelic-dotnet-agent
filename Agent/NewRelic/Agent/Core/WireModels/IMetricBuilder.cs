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

		MetricWireModel TryBuildSupportabilityErrorHttpStatusCodeFromCollector(HttpStatusCode statusCode);

		MetricWireModel TryBuildSupportabilityEndpointMethodErrorAttempts(string endpointMethod);

		MetricWireModel TryBuildSupportabilityEndpointMethodErrorDuration(string endpointMethod, TimeSpan duration);

		MetricWireModel TryBuildSpanEventsSeenMetric(int count);

		MetricWireModel TryBuildSpanEventsSentMetric(int count);

		MetricWireModel TryBuildSqlParsingCacheCountMetric(string name, int count);

		MetricWireModel TryBuildSqlParsingCacheSizeMetric(string name, int size);

		MetricWireModel TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit(string endpoint, int count = 1);

		MetricWireModel TryBuildInstallTypeMetric(string installType);

		MetricWireModel TryBuildSupportabilityCountMetric(string metricName, int count = 1);
	}
}
