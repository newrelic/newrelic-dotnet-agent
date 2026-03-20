// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace ReportGenerator;

public class Options
{
    [Option("input-dir", Required = true, HelpText = "Directory containing perf-results-* subdirectories.")]
    public string InputDir { get; set; } = string.Empty;

    [Option("output-dir", Required = true, HelpText = "Directory where charts and summary.md will be written.")]
    public string OutputDir { get; set; } = string.Empty;
}
