// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace NugetValidator;

public class Options
{
    [Option('v', "version", Required = true, HelpText = "Package Version")]
    public string Version { get; set; }

    [Option('c', "config", Default = "config.yml", Required = false, HelpText = "Path to the configuration file.")]
    public required string ConfigurationPath { get; set; }
}
