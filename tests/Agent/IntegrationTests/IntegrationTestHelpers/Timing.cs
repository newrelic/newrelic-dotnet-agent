// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Timing
    {
        public static readonly TimeSpan TimeToColdStart = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan TimeToConnect = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan TimeToWaitForLog = TimeSpan.FromMinutes(3);
        public static readonly TimeSpan TimeToApm = TimeSpan.FromMinutes(7);
        public static readonly TimeSpan TimeBetweenHarvests = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan ApmCheckInterval = TimeSpan.FromSeconds(5);
        // how long to keep pinging APM for data until we get expected results (this is after the metrics show up but are incorrect)
        public static readonly TimeSpan ApmRetyTimeout = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan TimeBetweenFileExistChecks = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan TimeToDockerComposeUp = TimeSpan.FromMinutes(5);
    }
}
