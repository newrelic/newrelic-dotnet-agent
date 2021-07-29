// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReportBuilder
{
    public class WikiMaker
    {
        public void UpdateWiki(string outputPath, string agentVersion, InstrumentationOverview instrumentationOverview)
        {
            var directoryInfo = SetupDirectoryStructure(outputPath, agentVersion);
            SetupDirectoryContents(agentVersion, directoryInfo, instrumentationOverview);
            UpdateMainHome(outputPath);
        }

        private DirectoryInfo SetupDirectoryStructure(string outputPath, string version)
        {
            var fullPath = Path.GetFullPath($"v{version}", outputPath);
            if (Directory.Exists(fullPath))
            {
                Directory.Move(fullPath, fullPath + "_old");
            }

            return Directory.CreateDirectory(fullPath);
        }

        private void SetupDirectoryContents(string agentVersion, DirectoryInfo directoryInfo, InstrumentationOverview instrumentationOverview)
        {
            var fileList = new List<string>();
            foreach (var report in instrumentationOverview.Reports)
            {
                var file = CreateFrameworkFile(agentVersion, directoryInfo, report);
                fileList.Add(file);
            }

            CreateVersionHomeFile(agentVersion, directoryInfo, fileList);
        }

        private string CreateFrameworkFile(string agentVersion, DirectoryInfo directoryInfo, KeyValuePair<string, List<PackageOverview>> report)
        {
            var fileName = $"v{agentVersion}-{report.Key}";
            var filePath = CreateFile($"{fileName}.md", directoryInfo.FullName);

            var builder = new StringBuilder();
            builder.AppendLine(string.Empty);

            foreach (var packageOverview in report.Value)
            {
                // Package_Name B
                builder.AppendLine(string.Empty);
                builder.AppendLine($"Package: {packageOverview.PackageName}");
                builder.AppendLine(string.Empty);

                builder.Append("| Method/Version ");
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    // | Method/Version | 1.0 | 2.0 | 3.0 | 4.0 |
                    builder.Append($"| {versionedAssemblyOverview.Key} ");
                }

                builder.AppendLine("|");

                // |---|---|---|---|---|
                builder.Append("|---");
                for (var i = 0; i < packageOverview.PackageVersions.Count; i++)
                {
                    builder.Append("|---");
                }

                builder.AppendLine("|");

                // | assembly.class.method() |   |   |   |   |
                var lineStore = new Dictionary<string, StringBuilder>();
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    var methodSignatures = versionedAssemblyOverview.Value;

                    // if no sigs, update all exist ones with false column
                    if (methodSignatures.Count == 0)
                    {
                        foreach (var line in lineStore)
                        {
                            line.Value.Append("| false ");
                        }

                        continue;
                    }

                    // sigs exists, add each to line with isValid
                    foreach (var methodSignature in methodSignatures)
                    {
                        if (!lineStore.TryGetValue(methodSignature.Key, out var sigBuilder))
                        {
                            lineStore[methodSignature.Key] = new StringBuilder($"| {methodSignature.Key} ");
                            sigBuilder = lineStore[methodSignature.Key];
                        }

                        sigBuilder.Append($"| {methodSignature.Value} ");
                    }
                }

                // we get list of methods from XML, not assembly, so they stay the same
                foreach (var line in lineStore)
                {
                    line.Value.Append("|");
                    builder.AppendLine(line.Value.ToString());
                }
            }

            var output = builder.ToString();
            AppendContent(filePath, output);
            return fileName;
        }

        private void CreateVersionHomeFile(string agentVersion, DirectoryInfo directoryInfo, List<string> fileList)
        {
            var filePath = CreateFile($"v{agentVersion}-Home.md", directoryInfo.FullName);
            var builder = new StringBuilder();
            foreach (var file in fileList)
            {
                builder.AppendLine($"[[{file}]]");
                builder.AppendLine(string.Empty);
            }
            AppendContent(filePath, builder.ToString());
        }

        private string CreateFile(string path, string basePath)
        {
            var filePath = Path.GetFullPath(path, basePath);
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }

            return filePath;
        }

        private void OverwriteContent(string filePath, string content)
        {
            WriteContent(filePath, content, false);
        }

        private void AppendContent(string filePath, string content)
        {
            WriteContent(filePath, content, true);
        }

        private void WriteContent(string filePath, string content, bool shouldAppend)
        {
            using (var writer = new StreamWriter(filePath, append: shouldAppend))
            {
                writer.WriteLine(content);
            }
        }

        private void UpdateMainHome(string outputPath)
        {
            // mvs root under wiki
            var directoryInfo = new DirectoryInfo(outputPath);

            // enumerate the version dirs to get names for homes
            var builder = new StringBuilder();
            builder.AppendLine("Welcome to the Multiverse Testing Report");
            builder.AppendLine(string.Empty);
            builder.AppendLine("Select an agent version from the list below to see the reports.");
            builder.AppendLine(string.Empty);

            foreach (var dir in directoryInfo.EnumerateDirectories())
            {
                builder.AppendLine($"[[{dir.Name} Home]]");
                builder.AppendLine(string.Empty);
            }

            var filePath = Path.GetFullPath("Multiverse-Report-Home.md", outputPath);
            OverwriteContent(filePath, builder.ToString()); ;
        }
    }
}
