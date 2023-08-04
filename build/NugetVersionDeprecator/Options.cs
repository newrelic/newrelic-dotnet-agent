// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace NugetVersionDeprecator;

public class Options
{
    [Option('t', "test-mode", Required = true, HelpText = "Test mode, report deprecated packages but don't create GH Issue")]
    public bool TestMode { get; set; }

    [Option('c', "config", Default = "config.yml", Required = false, HelpText = "Path to the configuration file.")]
    public required string ConfigurationPath { get; set; }

    [Option('g', "github-token", Required = true, HelpText = "The Github token to use when creating new issues")]
    public string GithubToken { get; set; }
}
