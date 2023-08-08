// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace ReleaseNotesBuilder
{
    public class Options
    {
        [Option('v', "verbose", Default = false, Required = false, HelpText = "Output the release notes to console.")]
        public bool Verbose { get; set; }

        [Option('p', "pdata", Required = true, HelpText = "Path to the persisent data.")]
        public required string PersistentData { get; set; }

        [Option('c', "changelog", Required = true, HelpText = "Changelog.md file to process.")]
        public required string Changelog { get; set; }

        [Option('x', "checksums", Required = true, HelpText = "checksums.md file to process.")]
        public required string Checksums { get; set; }

        [Option('o', "output", Required = true, HelpText = "Where to save the release notes file.  Path only, file name is determined by version!")]
        public required string Output { get; set; }
    }
}
