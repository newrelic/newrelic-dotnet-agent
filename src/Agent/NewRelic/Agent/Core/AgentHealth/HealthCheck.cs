// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.AgentHealth
{
    public class HealthCheck
    {
        private const int NanoSecondsPerMillisecond = 1000000;

        public bool IsHealthy { get; internal set; }
        public string Status { get; internal set; }
        public string LastError { get; internal set; }
        public DateTime StartTime { get; } = DateTime.UtcNow;
        public DateTime StatusTime { get; internal set; }
        public string FileName { get; } = "health-" + System.Guid.NewGuid().ToString("N") + ".yml";

        /// <summary>
        /// Set the health status of the agent, but only update changed values.
        /// </summary>
        /// <param name="healthy"></param>
        /// <param name="healthStatus"></param>
        /// <param name="statusParams"></param>
        public void TrySetHealth((bool IsHealthy, string Code, string Status) healthStatus, params string[] statusParams)
        {
            // Threading!
            if (IsHealthy != healthStatus.IsHealthy)
            {
                IsHealthy = healthStatus.IsHealthy;
            }

            if (!Status.Equals(healthStatus.Code, StringComparison.OrdinalIgnoreCase))
            {
                if (statusParams != null && statusParams.Length > 0)
                {
                    Status = string.Format(Status, statusParams);
                }
                else
                {
                    Status = healthStatus.Status;
                }
            }

            if (!LastError.Equals(healthStatus.Code, StringComparison.OrdinalIgnoreCase))
            {
                LastError = healthStatus.Code;
            }
        }

        public string ToYaml()
        {
            StatusTime = DateTime.UtcNow;
            return $"healthy: {IsHealthy}\nstatus: {Status}\nlast_error: {LastError}\nstatus_time_unix_nano: {StatusTime.ToUnixTimeMilliseconds() * NanoSecondsPerMillisecond}\nstart_time_unix_nano: {StartTime.ToUnixTimeMilliseconds() * NanoSecondsPerMillisecond}";
        }
    }
}
