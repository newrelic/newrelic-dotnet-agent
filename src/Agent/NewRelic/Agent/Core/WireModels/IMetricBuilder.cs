﻿using System;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.WireModels
{
    public interface IMetricBuilder
    {
        MetricWireModel TryBuildMemoryPhysicalMetric(Double memoryPhysical);
        MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime);
        MetricWireModel TryBuildCpuUserUtilizationMetric(Single cpuUtilization);
        MetricWireModel TryBuildCpuTimeRollupMetric(Boolean isWebTransaction, TimeSpan cpuTime);
        MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime);

        MetricWireModel TryBuildDotnetVersionMetric(string version);
        MetricWireModel TryBuildAgentVersionMetric(String agentVersion);
        MetricWireModel TryBuildAgentVersionByHostMetric(String hostName, String agentVersion);
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

        MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, String wrapperName, String typeName, String methodName);

        MetricWireModel TryBuildFeatureEnabledMetric(String featureName);

        MetricWireModel TryBuildAgentApiMetric(String methodName);

        MetricWireModel TryBuildCustomTimingMetric(String suffix, TimeSpan time);
        MetricWireModel TryBuildCustomCountMetric(String suffix, Int32 count = 1);

        MetricWireModel TryBuildLinuxOsMetric(bool isLinux);
        MetricWireModel TryBuildBootIdError();
    }
}
