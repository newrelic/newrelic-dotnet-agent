// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReportGenerator;

public record RunMetrics(
    string Label,
    double RequestsPerSec,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double AvgCpuPct,
    double MaxCpuPct,
    double AvgMemMb);
