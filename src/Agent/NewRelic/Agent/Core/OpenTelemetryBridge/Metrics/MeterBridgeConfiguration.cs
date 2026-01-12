// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics
{
    public class MeterBridgeConfiguration : ConfigurationBasedService
    {
        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // Configuration is automatically updated by base class
        }

        public Uri BuildOtlpEndpoint(IConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
                return null;

            return new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, connectionInfo.Port, "/v1/metrics").Uri;
        }

        public bool IsMetricsEnabled() => _configuration.OpenTelemetryMetricsEnabled;

        public bool ShouldEnableInstrumentsInMeter(string meterName) => MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, meterName);
    }
}
