// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.WireModels
{
    public interface IMetricBuilder
    {
        MetricWireModel TryBuildMemoryPhysicalMetric(double memoryPhysical);
        MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime);
        MetricWireModel TryBuildCpuUserUtilizationMetric(float cpuUtilization);
        MetricWireModel TryBuildCpuTimeRollupMetric(bool isWebTransaction, TimeSpan cpuTime);
        MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime);

        MetricWireModel TryBuildDotnetVersionMetric(string version);
        MetricWireModel TryBuildAgentVersionMetric(string agentVersion);
        MetricWireModel TryBuildAgentVersionByHostMetric(string hostName, string agentVersion);
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

        MetricWireModel TryBuildAgentApiMetric(string methodName);

        MetricWireModel TryBuildCustomTimingMetric(string suffix, TimeSpan time);
        MetricWireModel TryBuildCustomCountMetric(string suffix, int count = 1);

        MetricWireModel TryBuildLinuxOsMetric(bool isLinux);
        MetricWireModel TryBuildBootIdError();
    }
}
