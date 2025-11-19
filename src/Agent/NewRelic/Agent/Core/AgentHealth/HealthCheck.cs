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
            lock (this)
            {
                if (IsHealthy != healthStatus.IsHealthy)
                {
                    IsHealthy = healthStatus.IsHealthy;
                }

                if (string.IsNullOrEmpty(Status) || !Status.Equals(healthStatus.Code, StringComparison.OrdinalIgnoreCase))
                {
                    Status = statusParams is { Length: > 0 } ?
                        string.Format(healthStatus.Status, statusParams)
                        :
                        healthStatus.Status;
                }

                if (string.IsNullOrEmpty(LastError) || !LastError.Equals(healthStatus.Code, StringComparison.OrdinalIgnoreCase))
                {
                    LastError = healthStatus.Code;
                }

                StatusTime = DateTime.UtcNow;
            }
        }

        public string ToYaml(string entityGuid)
        {
            lock (this)
            {
                return
                    $"entity_guid: {entityGuid}\nhealthy: {IsHealthy}\nstatus: {Status}\nlast_error: {LastError}\nstart_time_unix_nano: {StartTime.ToUnixTimeMilliseconds() * NanoSecondsPerMillisecond}\nstatus_time_unix_nano: {StatusTime.ToUnixTimeMilliseconds() * NanoSecondsPerMillisecond}";
            }
        }
    }
}
