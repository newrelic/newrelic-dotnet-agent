// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace ReleaseNotesBuilder
{
    internal class Program
    {
        internal const string SectionPrefix = "### ";
        internal const string SecuritySection = SectionPrefix + "Security";
        internal const string NewFeaturesSection = SectionPrefix + "New Features";
        internal const string FixesSection = SectionPrefix + "Fixes";
        internal const string NoticeSection = SectionPrefix + "Notice";

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(ValidateOptions)
                .WithNotParsed(HandleParseError);

            var persistentData = LoadPersisentData(result.Value.PersistentData);
            var checksums = LoadChecksums(result.Value.Checksums);
            var maker = new ReleaseNotesModel(persistentData, checksums);
            var changelog = File.ReadAllLines(result.Value.Changelog).ToList();
            var releaseVersion = ChangelogParser.Parse(changelog, maker);

            var releaseNotes = maker.GetDocumentContents();

            if (result.Value.Verbose)
            {
                Console.WriteLine(releaseNotes);
            }

            // File name format: net-agent-10-13-0.mdx
            var version = new Version(releaseVersion);
            var filePath = $"{result.Value.Output}{Path.DirectorySeparatorChar}net-agent-{version.Major}-{version.Minor}-{version.Build}.mdx";
            File.WriteAllText(filePath, releaseNotes);
            Console.WriteLine(filePath);
        }

        private static void ValidateOptions(Options opts)
        {
            if (string.IsNullOrWhiteSpace(opts.Changelog)
                || string.IsNullOrWhiteSpace(opts.PersistentData)
                || string.IsNullOrWhiteSpace(opts.Checksums)
                || string.IsNullOrWhiteSpace(opts.Output))
            {
                ExitWithError(ExitCode.BadArguments, "Arguments were empty or whitespace.");
            }

            if (!File.Exists(opts.PersistentData))
            {
                ExitWithError(ExitCode.FileNotFound, $"Persistent data file did not exist at {opts.PersistentData}.");
            }

            if (!File.Exists(opts.Changelog))
            {
                ExitWithError(ExitCode.FileNotFound, $"Changelog file did not exist at {opts.Changelog}.");
            }

            if (!File.Exists(opts.Checksums))
            {
                ExitWithError(ExitCode.FileNotFound, $"Checksums file did not exist at {opts.Checksums}.");
            }

            if (!Path.Exists(opts.Output))
            {
                ExitWithError(ExitCode.FileNotFound, $"Output path did not exist at {opts.Output}.");
            }
        }

        private static PersistentData LoadPersisentData(string path)
        {
            try
            {
                var input = File.ReadAllText(path);
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                return deserializer.Deserialize<PersistentData>(input);
            }
            catch (Exception ex)
            {
                ExitWithError(ExitCode.InvalidData, "Error loading persustent data: " + Environment.NewLine + ex.Message);
                return new PersistentData(); ;
            }
        }

        private static string LoadChecksums(string path)
        {
            try
            {
                // This file contains exactly what goes into the release notes, no changes needed.
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                ExitWithError(ExitCode.InvalidData, "Error loading checksums: " + Environment.NewLine + ex.Message);
                return string.Empty;
            }
            
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            ExitWithError(ExitCode.BadArguments, "Error occurred while parsing command line arguments.");
        }

        public static void ExitWithError(ExitCode exitCode, string message)
        {
            Console.WriteLine(message);
            Environment.Exit((int)exitCode);
        }
    }
}
