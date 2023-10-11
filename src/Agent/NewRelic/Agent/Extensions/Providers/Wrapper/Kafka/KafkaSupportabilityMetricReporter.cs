// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public static class KafkaSupportabilityMetricReporter
    {
        private static bool _kafkaClientVersionReported = false;

        public static void ReportKafkaSupportabilityMetric(IAgent agent, Type kafkaType)
        {
            if (_kafkaClientVersionReported) return;

            var assembly = Assembly.GetAssembly(kafkaType);
            var assemblyVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (assemblyVersion != null)
            {
                agent.RecordSupportabilityMetric($"Supportability/DotNet/MessageBroker/Confluent.Kafka/{assemblyVersion.Version}");
            }

            _kafkaClientVersionReported = true;
        }
    }
}
