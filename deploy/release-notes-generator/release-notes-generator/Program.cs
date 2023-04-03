using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace release_notes_generator
{
    internal class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Testfiles are included as content in the project

                if (args != null && args.Length != 4)
                {
                    PrintUsage();
                }

                var templateFilePath = args[0];
                if (!File.Exists(templateFilePath))
                {
                    Console.WriteLine($"Template input file {templateFilePath} does not exist");
                    PrintUsage();
                    return -1;
                }

                var checksumFilePath = args[1];
                if (!File.Exists(checksumFilePath))
                {
                    Console.WriteLine($"Checksum input file {checksumFilePath} does not exist");
                    PrintUsage();
                    return -1;
                }

                var releaseNotesFilePath = args[2];
                if (!File.Exists(releaseNotesFilePath))
                {
                    Console.WriteLine($"Release notes input file {releaseNotesFilePath} does not exist");
                    PrintUsage();
                    return -1;
                }

                var outputDirectory = args[3];
                if (!Directory.Exists(outputDirectory))
                {
                    Console.WriteLine($"Output directory {outputDirectory} does not exist");
                    PrintUsage();
                    return -1;
                }

                Console.WriteLine($"templateFilePath: {templateFilePath}");
                Console.WriteLine($"checksumFilePath: {checksumFilePath}");
                Console.WriteLine($"releaseNotesFilePath: {releaseNotesFilePath}");
                Console.WriteLine($"outputDirectory: {outputDirectory}");

                var outputFile = GenerateReleaseNotes(templateFilePath, checksumFilePath, releaseNotesFilePath, outputDirectory);
            }
            catch (Exception ex)
            {
                PrintUsage();
                Console.WriteLine($"Error: {ex}");
                return -1;
            }

            return 0;
        }

        private static string GenerateReleaseNotes(string templateFilePath, string checksumFilePath, string releaseNotesFilePath, string outputDirectory)
        {
            var templateFileContent = File.ReadAllText(templateFilePath);
            var checksumFileContent = File.ReadAllText(checksumFilePath);
            var releaseNotesFileContent = File.ReadAllLines(releaseNotesFilePath);
            Console.WriteLine("Source content read.");

            // Extract version and date from the release notes file
            // Sample first line:
            // ## [1.1.0](https://github.com/JcolemanNR/release-please-testing/compare/v1.0.0...v1.1.0) (2023-03-28)
            var matchString = @"## \[(\d*\.\d*\.\d*)\].*\((\d\d\d\d-\d\d-\d\d)\)";
            var match = Regex.Match(releaseNotesFileContent[0], matchString, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new Exception("Unable to extract version number or release date from the release notes file!");
            }

            // Release Version
            var capturedVersionNumber = match.Groups[1].Value;
            Console.WriteLine($"Detected Release Version: {capturedVersionNumber}");
            templateFileContent = templateFileContent.Replace("{version}", capturedVersionNumber);

            // Release Date
            var capturedReleaseDate = DateTime.ParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Console.WriteLine($"Detected Release Date: {capturedReleaseDate.Date.ToShortDateString()}");
            templateFileContent = templateFileContent.Replace("{date}", capturedReleaseDate.ToString("yyyy-MM-dd"));

            // Interpret release file content
            var releaseContentMap = GetReleaseSectionEntries(releaseNotesFileContent);
            foreach (var section in releaseContentMap)
            {
                Console.WriteLine($"Detected Section: {section.Key}");
                foreach (var item in section.Value)
                {
                    Console.WriteLine(" - " + item);
                }
            }

            // Handle Frontmatter
            var newFeaturesFrontmatter = PopulateFrontmatterContent("New Features", releaseContentMap);
            templateFileContent = templateFileContent.Replace("{feature_summary_list}", GenerateFrontMatterJsonFromList(newFeaturesFrontmatter));

            var newSecurityFrontmatter = PopulateFrontmatterContent("Security", releaseContentMap);
            templateFileContent = templateFileContent.Replace("{security_summary_list}", GenerateFrontMatterJsonFromList(newSecurityFrontmatter));

            var newFixesFrontmatter = PopulateFrontmatterContent("Fixes", releaseContentMap);
            templateFileContent = templateFileContent.Replace("{bug_summary_list}", GenerateFrontMatterJsonFromList(newFixesFrontmatter));

            // Handle release content
            string releaseContent = GetReleaseContent(releaseNotesFileContent);
            templateFileContent = templateFileContent.Replace("{changes_content}", releaseContent);

            // Checksum file content
            templateFileContent = templateFileContent.Replace("{checksum_content}", checksumFileContent);

            // Calculate output filename/path
            var outputFilename = Path.Combine(outputDirectory, $"net-agent-{capturedVersionNumber.Replace('.', '-')}.mdx");
            Console.WriteLine($"Output file: {outputFilename}");
            File.WriteAllText(outputFilename, templateFileContent);

            return outputFilename;
        }

        private static string GetReleaseContent(string[] releaseNotesFileContent)
        {
            var result = new StringBuilder();
            for (int i = 1; i < releaseNotesFileContent.Length; i++)
            {
                if (result.Length == 0 && string.IsNullOrWhiteSpace(releaseNotesFileContent[i]))
                {
                    continue;
                }
                result.Append(releaseNotesFileContent[i] + Environment.NewLine);
            }
            return result.ToString();
        }

        private static string GenerateFrontMatterJsonFromList(List<string> newFeaturesFrontmatter)
        {
            var result = string.Empty;
            for (int i = 0; i < newFeaturesFrontmatter.Count; i++)
            {
                if (i > 0)
                {
                    result += ", ";
                }
                result += $"\"{newFeaturesFrontmatter[i]}\"";
            }
            return result;
        }

        private static List<string> PopulateFrontmatterContent(string section, Dictionary<string, List<string>> releaseContent)
        {
            var result = new List<string>();
            if (releaseContent.TryGetValue(section, out List<string> entries))
            {
                foreach (var entry in entries)
                {
                    Console.WriteLine($"New {section} Pre Frontmatter: {entry}");
                    var reducedFrontmatter = FrontmatterFormatter.FormatStringForFrontmatter(entry);
                    result.Add(reducedFrontmatter);
                    Console.WriteLine($"New {section} Post Frontmatter: {reducedFrontmatter}");
                }
            }
            return result;
        }

        private static Dictionary<string, List<string>> GetReleaseSectionEntries(string[] fileContent)
        {
            var result = new Dictionary<string, List<string>>();

            var currentCategory = string.Empty;
            foreach (var entry in fileContent)
            {
                // We are looking for headings to determine how to group things... examples:
                // ### New Features
                // ### Security
                var contentHeaderMatch = Regex.Match(entry, "### (.*)", RegexOptions.IgnoreCase);
                if (contentHeaderMatch.Success)
                {
                    // start a new category section
                    currentCategory = contentHeaderMatch.Groups[1].Value.Trim();
                }
                else if (string.IsNullOrWhiteSpace(currentCategory))
                {
                    // There is no point in reading content without a category
                    continue;
                }

                if (!result.ContainsKey(currentCategory))
                {
                    result[currentCategory] = new List<string>();
                }

                // We have a header section, and we are reading content between/after them... we are insterested in lines like this:
                // * Fix hypothetical issues in the bleep bloop compensator (No PR) ([e5c9fcf](https://github.com/JcolemanNR/release-please-testing/commit/e5c9fcf80aeaa81e21fae5078cc0f59fdc7a1eda))
                // * Resolve hypothetical security issue (no PR) ([42f4013](https://github.com/JcolemanNR/release-please-testing/commit/42f40135f673b246ea908e4e0ce6dfbaf2022b0a))
                var contentItemMatch = Regex.Match(entry, @"^\* (.*)", RegexOptions.IgnoreCase);
                if (contentItemMatch.Success)
                {
                    // start a new category section
                    result[currentCategory].Add(contentItemMatch.Groups[1].Value.Trim());
                }
            }

            return result;

        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: release_notes_generator <Version> <TemplateFile> <ChecksumFile> <ReleaseNotesFile> <OutputPath>");
        }
    }
}


