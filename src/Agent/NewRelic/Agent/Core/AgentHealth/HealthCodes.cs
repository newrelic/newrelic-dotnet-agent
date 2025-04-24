// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.AgentHealth
{
    public static class HealthCodes
    {
        /// <summary>
        /// Healthy
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) Healthy = (true, "NR-APM-000",
            "Healthy");

        /// <summary>
        /// Invalid license key (HTTP status code 401)
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) LicenseKeyInvalid = (false, "NR-APM-001",
            "Invalid license key (HTTP status code 401)");

        /// <summary>
        /// License key missing in configuration
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) LicenseKeyMissing = (false, "NR-APM-002",
            "License key missing in configuration");

        /// <summary>
        /// Forced disconnect received from New Relic (HTTP status code 410)
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) ForceDisconnect = (false, "NR-APM-003",
            "Forced disconnect received from New Relic (HTTP status code 410)");

        /// <summary>
        /// HTTP error response code [%s] received from New Relic while sending data type [%s]
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) HttpError = (false, "NR-APM-004",
            "HTTP error response code {0} received from New Relic while sending data type {1}");

        /// <summary>
        /// Missing application name in agent configuration
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) ApplicationNameMissing = (false, "NR-APM-005",
            "Missing application name in agent configuration");

        /// <summary>
        /// The maximum number of configured app names (3) exceeded
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) MaxApplicationNamesExceeded = (false, "NR-APM-006",
            "The maximum number of configured app names (3) exceeded");

        /// <summary>
        /// HTTP Proxy configuration error; response code [%s]
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) HttpProxyError = (false, "NR-APM-007",
            "HTTP Proxy configuration error; response code {0}");

        /// <summary>
        /// Agent is disabled via configuration
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) AgentDisabledByConfiguration = (false, "NR-APM-008",
            "Agent is disabled via configuration");

        /// <summary>
        /// Failed to connect to New Relic data collector
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) FailedToConnect = (false, "NR-APM-009",
            "Failed to connect to New Relic data collector");

        /// <summary>
        /// Agent has shutdown
        /// Only be reported if agent is "healthy" on shutdown.
        /// If the agent status is not Healthy on agent shutdown, the existing error MUST not be overwritten.
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) AgentShutdownHealthy = (true, "NR-APM-099",
            "Agent has shutdown");

        // Agent health codes for the .NET agent are 200-299

        /// <summary>
        /// Agent has shutdown with exception [%s]
        /// </summary>
        public static readonly (bool IsHealthy, string Code, string Status) AgentShutdownError = (false, "NR-APM-200",
            "Agent has shutdown with exception {0}");
    }
}
