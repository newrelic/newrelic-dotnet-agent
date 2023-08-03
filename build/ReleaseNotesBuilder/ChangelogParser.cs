// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReleaseNotesBuilder
{
    internal static class ChangelogParser
    {
        private const string ChangeLogHeader = "# Changelog";
        private const string EndSquareBracket = "]";
        private const string ReleaseVersionPrefix = "## [";

        /// <summary>
        /// Parses the changelog.md file, update the ReleaseNotesModel and return the extracted release version.
        /// </summary>
        /// <param name="changelog"></param>
        /// <param name="maker"></param>
        /// <returns>Extracted release version.</returns>
        public static string Parse(List<string> changelog, ReleaseNotesModel maker)
        {
            try
            {
                // Try to confirm that this is a changelog in our format.
                if (changelog[0] != ChangeLogHeader)
                {
                    Program.ExitWithError(ExitCode.NotAChangelog, $"The file does not appear to be a changelog file.  Has the format change?");
                }

                // starting point for other searches
                var currentReleaseIndex = changelog.FindIndex(0, 25, (x) => x.StartsWith(ReleaseVersionPrefix));
                if (currentReleaseIndex == -1)
                {
                    Program.ExitWithError(ExitCode.NotAChangelog, $"The file does not appear to have a release version.  Has the format change?");
                }

                var releaseVersion = ParseReleaseVersion(changelog, currentReleaseIndex);
                maker.SetReleaseVersion(releaseVersion);

                // upper bound on the searches
                var previousReleaseIndex = changelog.FindIndex(currentReleaseIndex + 1, 50, (x) => x.StartsWith(ReleaseVersionPrefix));

                //Get change type starting points.
                var newFeatures = ParseSection(changelog, Program.NewFeaturesSection, currentReleaseIndex, previousReleaseIndex);
                maker.AddFeatures(newFeatures);

                var fixes = ParseSection(changelog, Program.FixesSection, currentReleaseIndex, previousReleaseIndex);
                maker.AddBugsAndFixes(fixes);

                var security = ParseSection(changelog, Program.SecuritySection, currentReleaseIndex, previousReleaseIndex);
                maker.AddSecurity(security);

                var notice = ParseSection(changelog, Program.NoticeSection, currentReleaseIndex, previousReleaseIndex);
                maker.AddNotice(notice);

                return releaseVersion;
            }
            catch (Exception ex)
            {
                Program.ExitWithError(ExitCode.InvalidData, $"Problem parsing changelog: " + Environment.NewLine + ex.Message);
                return string.Empty;
            }
        }

        private static string ParseReleaseVersion(List<string> changelog, int releaseIndex)
        {
            var line = changelog[releaseIndex];
            var endIndex = line.IndexOf(EndSquareBracket);

            return line[4..endIndex];
        }

        private static List<Entry> ParseSection(List<string> changelog, string sectionLabel, int startingIndex, int maxIndex)
        {
            var maxLines = GetMaxLines(startingIndex, maxIndex);
            var sectionIndex = changelog.FindIndex(startingIndex, maxLines, (x) => x.StartsWith(sectionLabel));
            if (sectionIndex == -1 || sectionIndex >= maxIndex)
            {
                return new List<Entry>();
            }

            return GetChangeEntries(changelog, sectionIndex, maxIndex);
        }

        private static List<Entry> GetChangeEntries(List<string> changelog, int startingIndex, int maxIndex)
        {
            var entries = new List<Entry>();
            var nextSectionIndex = changelog.FindIndex(startingIndex + 1, GetMaxLines(startingIndex, maxIndex), (x) => x.StartsWith(Program.SectionPrefix));
            var sectionEndIndex = nextSectionIndex == -1 ? maxIndex : nextSectionIndex;
            var maxLines = GetMaxLines(startingIndex, sectionEndIndex);
            for (int i = startingIndex; i < sectionEndIndex; i++)
            {
                var line = changelog[i];
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("* "))
                {
                    continue;
                }

                var frontEntry = GetFrontEntry(line);
                entries.Add(new Entry(frontEntry, line)); // we want the entire line for the body as-is - no need to alter it later.
            }

            return entries;
        }

        private static string GetFrontEntry(string line)
        {
            var descEndIndex = line.IndexOf("([");
            var bodyEntry = line[2..descEndIndex].Trim(); // drop the "* "

            var sentenceEndIndex = bodyEntry.IndexOf(". ", 0) + 1;
            if (sentenceEndIndex <= 0) // account for padding above.
            {
                sentenceEndIndex = bodyEntry.Length;
            }

            var frontEntry = bodyEntry[0..sentenceEndIndex].Trim();
            if (!frontEntry.EndsWith("."))
            {
                frontEntry += ".";
            }

            return CleanFrontEntry(frontEntry);
        }

        private static string CleanFrontEntry(string frontEntry)
        {
            // Front matter entries are wrapped in single quotes so we need to escape them and remove newlines.
            var cleanedEntry = frontEntry.Replace("\'", "\'\'").Replace("\n", "").Replace("\r", "");
            var illegalChars = new[] { '&', '|', ':' }; // must be wrapped in double quotes
            foreach (var illegalChar in illegalChars)
            {
                // Purposefully break the entry removing any double quoted illegal chars
                var damagedEntry = cleanedEntry.Replace($"\"{illegalChar}\"", $"{illegalChar}");

                // Re-wrap the illegal chars and any others that were not wrapped in the original entry
                cleanedEntry = damagedEntry.Replace($"{illegalChar}", $"\"{illegalChar}\"");
            }

            return cleanedEntry;
        }

        private static int GetMaxLines(int startingIndex, int maxIndex)
        {
            return maxIndex - startingIndex;
        }
    }
}
