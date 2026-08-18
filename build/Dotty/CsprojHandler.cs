// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Build.Construction;
using Serilog;

namespace Dotty;

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
                        packageReferences.Where(p => string.Equals(p.Include, versionData.PackageName, StringComparison.OrdinalIgnoreCase)).ToList();
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
                        string tfm = null;
                        if (condition?.StartsWith("'$(TargetFramework)'") ?? false)
                            tfm = condition?.Split("==").LastOrDefault()?.Trim('\'', ' ', ';');

                        // resolve per-TFM target if available, otherwise fall back to global latest
                        string targetVersion;
                        if (!string.IsNullOrEmpty(tfm) &&
                            versionData.TfmTargetVersions != null &&
                            versionData.TfmTargetVersions.ContainsKey(tfm))
                        {
                            var cappedValue = versionData.TfmTargetVersions[tfm];
                            if (cappedValue == null)
                            {
                                Log.Warning($"Not updating {packageReference.Include} for {tfm}; no version within the configured major ceiling. Leaving pinned.");
                                continue;
                            }
                            targetVersion = cappedValue;
                        }
                        else
                        {
                            targetVersion = versionData.NewVersion;
                        }

                        if (version.AsVersion() < targetVersion.AsVersion() &&
                            !string.IsNullOrEmpty(versionData.IgnoreTfMs) &&
                            versionData.IgnoreTfMs.Split(",").Contains(tfm))
                        {
                            Log.Warning($"Not updating {packageReference.Include} for {tfm}, this TFM is ignored.  Manual verification recommended.");
                            continue;
                        }

                        if (version.AsVersion() < targetVersion.AsVersion())
                        {
                            Log.Information($"{Path.GetFileName(csprojPath)}: Updating {packageReference.Include}{(!string.IsNullOrEmpty(tfm) ? $" for {tfm}" : "")} from {version} to {targetVersion}");

                            packageReference.Metadata.FirstOrDefault(m => m.Name == "Version")!.Value = targetVersion;

                            updatedCsProj = true;

                            updateLog.Add($"- Package [{versionData.PackageName}]({versionData.Url}){(!string.IsNullOrEmpty(tfm) ? $" for {tfm}" : "")} was updated from {version} to {targetVersion}.");
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
