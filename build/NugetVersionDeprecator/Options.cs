// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace NugetVersionDeprecator;

public class Options
{
    [Option('t', "test-mode", Required = false, HelpText = "Test mode: Report deprecated packages but don't create GH Issue")]
    public bool TestMode { get; set; }

    [Option('c', "config", Default = "config.yml", Required = true, HelpText = "Path to the configuration file.")]
    public required string ConfigurationPath { get; set; }

    [Option('g', "github-token", Required = false, HelpText = "The Github token to use when creating new issues. Not required in Test mode. Defaults to the GITHUB_TOKEN environment variable.")]
    public string GithubToken { get; set; }

    [Option('a', "api-key", Required = false, HelpText = "The NewRelic API Key for executing NerdGraph queries. Defaults to the NEW_RELIC_API_KEY environment variable.")]
    public string ApiKey { get; set; }
}
