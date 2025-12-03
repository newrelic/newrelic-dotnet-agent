// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

namespace MockNewRelic.Models
{
    public class MetricsSummaryDto
    {
        public DateTime ReceivedAtUtc { get; set; }
        public List<ResourceSummary> Resources { get; set; } = new List<ResourceSummary>();
        public int TotalMetricCount { get; set; }
        public int TotalDataPointCount { get; set; }
    }

    public class ResourceSummary
    {
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public List<ScopeSummary> Scopes { get; set; } = new List<ScopeSummary>();
    }

    public class ScopeSummary
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<MetricSummary> Metrics { get; set; } = new List<MetricSummary>();
    }

    public class MetricSummary
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int DataPointCount { get; set; }
    }
}
