// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ReportBuilder
{
    public class HtmlMaker
    {
        public void UpdatePages(string outputPath, string agentVersion, InstrumentationOverview instrumentationOverview)
        {
            var directoryInfo = SetupDirectoryStructure(outputPath, agentVersion);
            SetupDirectoryContents(agentVersion, directoryInfo, instrumentationOverview);
            UpdateMainIndexFile(outputPath);
            AddStyleSheet(outputPath);
        }

        public void SaveRawReport(string reportFilePath, string outputPath, string agentVersion)
        {
            var fileName = Path.GetFileName(reportFilePath);
            var versionPath = Path.GetFullPath($"v{agentVersion}", outputPath);
            var versionReportPath = Path.GetFullPath(fileName, versionPath);
            File.Copy(reportFilePath, versionReportPath, true);
        }

        private DirectoryInfo SetupDirectoryStructure(string outputPath, string agentVersion)
        {
            var fullPath = Path.GetFullPath($"v{agentVersion}", outputPath);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
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

            CreateVersionIndexFile(agentVersion, directoryInfo, fileList);
        }

        private string CreateFrameworkFile(string agentVersion, DirectoryInfo directoryInfo, KeyValuePair<string, List<PackageOverview>> report)
        {
            var filePath = CreateFile($"{report.Key}.html", directoryInfo.FullName);

            var builder = new StringBuilder();
            AddUpperBoilerplate(report.Key, builder);

            builder.AppendLine($"[ <a href=\"../index.html\">main page</a> | <a href=\"index.html\">v{agentVersion}</a> ]");
            builder.AppendLine(string.Empty);
            builder.AppendLine(string.Empty);
            foreach (var packageOverview in report.Value)
            {
                // Add package name
                builder.AppendLine(string.Empty);
                builder.AppendLine($"<h3>Package: {packageOverview.PackageName}</h3>");
                builder.AppendLine(string.Empty);
                builder.AppendLine("<table>");

                // add header row
                builder.AppendLine("  <tr>");
                builder.AppendLine("    <th>Method/Version</th>");
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    builder.AppendLine($"    <th>{versionedAssemblyOverview.Key}</th>");
                }

                builder.AppendLine("  </tr>");

                // add methods sig rows
                var rowStore = new Dictionary<string, StringBuilder>();
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    var methodSignatures = versionedAssemblyOverview.Value;

                    // if no sigs, update all exist ones with false column
                    if (methodSignatures.Count == 0)
                    {
                        foreach (var row in rowStore)
                        {
                            row.Value.AppendLine("    <td id=\"fail\"></td>");
                        }

                        continue;
                    }

                    // sigs exists, add each to row with isValid
                    foreach (var methodSignature in methodSignatures)
                    {
                        // create the signature row if it has not been hit yet
                        if (!rowStore.TryGetValue(methodSignature.Key, out var sigBuilder))
                        {
                            rowStore[methodSignature.Key] = new StringBuilder();
                            sigBuilder = rowStore[methodSignature.Key];
                            sigBuilder.AppendLine($"  <tr>");
                            sigBuilder.AppendLine($"    <td>{methodSignature.Key}</td>");
                        }

                        // add the isValid to the signature line
                        sigBuilder.Append("    <td id=\"");
                        if (methodSignature.Value)
                        {
                            sigBuilder.AppendLine("pass\"></td>");
                        }
                        else
                        {
                            sigBuilder.AppendLine("fail\"></td>");

                        }
                    }
                }

                // we get list of methods from XML, not assembly, so they stay the same
                foreach (var row in rowStore)
                {
                    row.Value.AppendLine("  </tr>");
                    builder.AppendLine(row.Value.ToString());
                }

                builder.AppendLine("</table>");
                builder.AppendLine(string.Empty);
            }

            AddFooterBoilerplate(builder);
            var output = builder.ToString();
            AppendContent(filePath, output);
            return report.Key;
        }

        private void CreateVersionIndexFile(string agentVersion, DirectoryInfo directoryInfo, List<string> fileList)
        {
            var filePath = CreateFile($"index.html", directoryInfo.FullName);
            var builder = new StringBuilder();
            AddUpperBoilerplate($"Agent Version {agentVersion}", builder);
            builder.AppendLine("[ <a href=\"../index.html\">main page</a> ]");
            builder.AppendLine(string.Empty);
            builder.AppendLine(string.Empty);
            builder.AppendLine("<p><a href=\"reports.yml\">Raw reports.yml file</a></p>");
            builder.AppendLine(string.Empty);
            builder.AppendLine(string.Empty);
            builder.AppendLine("<p><ul>");
            foreach (var file in fileList)
            {
                builder.AppendLine($"  <li><a href=\"{file}.html\">{file}</a></li>");
            }

            builder.AppendLine("</ul></p>");
            AddFooterBoilerplate(builder);
            AppendContent(filePath, builder.ToString());
        }

        private void UpdateMainIndexFile(string outputPath)
        {
            // mvs root under wiki
            var directoryInfo = new DirectoryInfo(outputPath);

            // enumerate the version dirs to get names for homes
            var builder = new StringBuilder();
            AddUpperBoilerplate("Multiverse Testing Report", builder);
            builder.AppendLine("<h2>Welcome to the Multiverse Testing Report</h2>");
            builder.AppendLine(string.Empty);
            builder.AppendLine("<p>Select an agent version from the list below to see the reports.</p>");
            builder.AppendLine(string.Empty);
            builder.AppendLine("<ul>");
            foreach (var dir in directoryInfo.EnumerateDirectories())
            {
                builder.AppendLine($"  <li><a href=\"{dir.Name}/index.html\">{dir.Name}</a></li>");
            }

            builder.AppendLine("</ul>");
            builder.AppendLine(string.Empty);
            AddFooterBoilerplate(builder);
            var filePath = Path.GetFullPath("index.html", outputPath);
            OverwriteContent(filePath, builder.ToString()); ;
        }

        private void AddStyleSheet(string outputPath)
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            File.Copy($@"{exeDir}\styles.css", $@"{outputPath}\styles.css", true);
        }

        private void AddUpperBoilerplate(string title, StringBuilder builder)
        {
            builder.AppendLine("<!doctype html>");
            builder.AppendLine(string.Empty);
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("  <meta charset=\"utf - 8\">");
            builder.AppendLine("  <meta name=\"viewport\" content=\"width = device - width, initial - scale = 1\">");
            builder.AppendLine(string.Empty);
            builder.AppendLine($"  <title>{title}</title>");
            builder.AppendLine($"  <meta name=\"description\" content=\"{title}\">");
            builder.AppendLine("  <meta name=\"author\" content=\"New Relic\">");
            builder.AppendLine(string.Empty);
            builder.AppendLine($"  <meta property=\"og: title\" content=\"{title}\">");
            builder.AppendLine("  <meta property=\"og: type\" content=\"website\">");
            builder.AppendLine($"  <meta property=\"og: description\" content=\"{title}\">");
            builder.AppendLine("  <link rel=\"stylesheet\" href=\"../styles.css\">");
            builder.AppendLine("</head>");
            builder.AppendLine("");
            builder.AppendLine("<body>");
        }

        private void AddFooterBoilerplate(StringBuilder builder)
        {
            builder.AppendLine("</body>");
            builder.AppendLine("</html>");
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
    }
}
