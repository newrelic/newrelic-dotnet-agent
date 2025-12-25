// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public class MeterBridgeConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly MeterFilterService _filterService;

        public MeterBridgeConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
            _filterService = new MeterFilterService(configuration);
        }

        public Uri BuildOtlpEndpoint(IConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
                return null;

            return new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, connectionInfo.Port, "/v1/metrics").Uri;
        }

        public bool IsMetricsEnabled() => _configuration.OpenTelemetryMetricsEnabled;

        public bool ShouldEnableInstrumentsInMeter(string meterName) => _filterService.ShouldEnableInstrumentsInMeter(meterName);
    }
}
