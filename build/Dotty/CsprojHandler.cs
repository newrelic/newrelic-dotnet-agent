using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Build.Construction;
using NuGet.Packaging;
using Serilog;

namespace Dotty
{
    public class CsprojHandler
    {
        public static List<string> UpdatePackageReferences(string csprojPath, List<ProjectPackageInfo> packages, List<NugetVersionData> versionDatas)
        {
            Log.Information("Processing {CSProj}...", csprojPath);
            var updateLog = new List<string>();

            if (packages.Count == 0)
            {
                Log.Warning("No packages found in csproj file " + csprojPath);
                return updateLog;
            }
            var updatedCsProj = false;

            var projectRootElement = ProjectRootElement.Open(csprojPath);
            if (projectRootElement != null)
            {
                foreach (var itemGroup in projectRootElement.ItemGroups)
                {
                    var packageReferences = itemGroup.Items.Where(i => i.ItemType == "PackageReference").ToList();

                    foreach (var versionData in versionDatas)
                    {
                        // check whether packageReferences contains the package
                        var matchingPackages =
                            packageReferences.Where(p => p.Include == versionData.PackageName).ToList();
                        if (matchingPackages.Count == 0)
                        {
                            //Log.Warning($"No matching packages found in csproj file for {versionData.PackageName}");
                            continue;
                        }

                        foreach (var packageReference in matchingPackages)
                        {
                            var version = packageReference.Metadata.FirstOrDefault(m => m.Name == "Version")?.Value;

                            // look for a condition attribute and parse the tfm if found
                            var condition = packageReference.Condition;
                            var tfm = condition?.Split("==").LastOrDefault()?.Trim('\'', ' ', ';');

                            if (version.AsVersion() < versionData.NewVersion.AsVersion() &&
                                !string.IsNullOrEmpty(versionData.IgnoreTfMs) &&
                                versionData.IgnoreTfMs.Split(",").Contains(tfm))
                            {
                                Log.Warning($"Not updating {packageReference.Include} for {tfm}, this TFM is ignored.  Manual verification recommended.");
                                continue;
                            }

                            if (version.AsVersion() < versionData.NewVersion.AsVersion())
                            {
                                Log.Information($"{Path.GetFileName(csprojPath)}: Updating {packageReference.Include}{(!string.IsNullOrEmpty(tfm) ? $" for {tfm}" : "")} from {version} to {versionData.NewVersion}");

                                packageReference.Metadata.FirstOrDefault(m => m.Name == "Version")!.Value = versionData.NewVersion;

                                updatedCsProj = true;

                                updateLog.Add($"- Package [{versionData.PackageName}]({versionData.Url}){(!string.IsNullOrEmpty(tfm) ? $" for {tfm}" : "")} was updated from {version} to {versionData.NewVersion}.");
                            }
                        }
                    }
                }

                if (updatedCsProj)
                {
                    projectRootElement.Save();
                    updateLog.Add("");
                }
            }

            return updateLog;
        }

        private static List<PackageReference> Parse(string csProjPath, string[] csprojLines)
        {
            var packages = new List<PackageReference>();
            try
            {
                var unclosed = false;
                var curLine = "";
                for (int i = 0; i < csprojLines.Length; i++)
                {
                    curLine += csprojLines[i];
                    if (!unclosed && !curLine.Contains("PackageReference"))
                    {
                        curLine = "";
                        continue;
                    }

                    if (unclosed && curLine.Contains("</PackageReference>"))
                    {
                        unclosed = false;
                    }
                    else if (!curLine.EndsWith("/>"))
                    {
                        unclosed = true;
                        curLine += Environment.NewLine;
                        continue;
                    }
                    else
                        unclosed = false;

                    var serializer = new XmlSerializer(typeof(PackageReference));
                    using (var reader = new StringReader(curLine))
                    {
                        var packageReference = (PackageReference)serializer.Deserialize(reader);
                        packageReference.LineNumber = i;
                        packages.Add(packageReference);
                    }

                    if (!unclosed)
                        curLine = "";
                }

                return packages;
            }
            catch (Exception e)
            {
                Log.Error(e, $"XML issue while parsing {csProjPath}");
                return packages;
            }
        }
    }
}
