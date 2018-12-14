using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using System;
using System.Net;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.WireModels
{
	public interface IMetricBuilder
	{
		MetricWireModel TryBuildMemoryPhysicalMetric(Double memoryPhysical);
		MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime);
		MetricWireModel TryBuildCpuUserUtilizationMetric(Single cpuUtilization);
		MetricWireModel TryBuildCpuTimeRollupMetric(Boolean isWebTransaction, TimeSpan cpuTime);
		MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime);

		MetricWireModel TryBuildAgentVersionMetric([NotNull] String agentVersion);
		MetricWireModel TryBuildAgentVersionByHostMetric([NotNull] String hostName, [NotNull] String agentVersion);
		MetricWireModel TryBuildMetricHarvestAttemptMetric();

		MetricWireModel TryBuildTransactionEventReservoirResizedMetric();
		MetricWireModel TryBuildTransactionEventsRecollectedMetric(Int32 eventsRecollected);
		MetricWireModel TryBuildTransactionEventsSentMetric(Int32 eventCount);
		MetricWireModel TryBuildTransactionEventsSeenMetric();
		MetricWireModel TryBuildTransactionEventsCollectedMetric();

		MetricWireModel TryBuildCustomEventReservoirResizedMetric();
		MetricWireModel TryBuildCustomEventsRecollectedMetric(Int32 eventsRecollected);
		MetricWireModel TryBuildCustomEventsSentMetric(Int32 eventCount);
		MetricWireModel TryBuildCustomEventsSeenMetric();
		MetricWireModel TryBuildCustomEventsCollectedMetric();

		MetricWireModel TryBuildErrorTracesCollectedMetric();
		MetricWireModel TryBuildErrorTracesRecollectedMetric(Int32 errorTracesRecollected);
		MetricWireModel TryBuildErrorTracesSentMetric(Int32 errorTraceCount);

		MetricWireModel TryBuildErrorEventsSentMetric(Int32 eventCount);
		MetricWireModel TryBuildErrorEventsSeenMetric();

		MetricWireModel TryBuildSqlTracesRecollectedMetric(Int32 sqlTracesRecollected);
		MetricWireModel TryBuildSqlTracesSentMetric(Int32 sqlTraceCount);

		MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, String additionalData = null);

		MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, [NotNull] String wrapperName, [NotNull] String typeName, [NotNull] String methodName);

		MetricWireModel TryBuildFeatureEnabledMetric([NotNull] String featureName);

		MetricWireModel TryBuildAgentApiMetric(string methodName, int count);

		MetricWireModel TryBuildCustomTimingMetric([NotNull] String suffix, TimeSpan time);
		MetricWireModel TryBuildCustomCountMetric([NotNull] String suffix, Int32 count = 1);

		MetricWireModel TryBuildLinuxOsMetric(bool isLinux);
		MetricWireModel TryBuildBootIdError();

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
	}
}
