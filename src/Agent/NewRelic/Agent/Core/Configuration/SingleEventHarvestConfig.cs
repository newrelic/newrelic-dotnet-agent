// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Configuration;

public class SingleEventHarvestConfig
{
    [JsonProperty("report_period_ms")]
    public int ReportPeriodMs { get; set; }

    [JsonProperty("harvest_limit")]
    public int HarvestLimit { get; set; }

    public TimeSpan? HarvestCycle => TimeSpan.FromMilliseconds(ReportPeriodMs);
}