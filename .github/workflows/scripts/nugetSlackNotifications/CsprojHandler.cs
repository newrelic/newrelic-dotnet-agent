using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Serilog;

namespace nugetSlackNotifications
{
    public class CsprojHandler
    {
        public static async Task<List<string>> UpdatePackageReferences(string csprojPath, List<NugetVersionData> versionDatas)
        {
            var updateLog = new List<string>();
            var csprojLines = await File.ReadAllLinesAsync(csprojPath);

            var packages = Parse(csprojLines);
            if (packages.Count == 0)
            {
                Log.Warning("No packages found in csproj file");
                return updateLog;
            }

            foreach (var versionData in versionDatas)
            {
                var matchingPackages = packages.Where(p => p.Include == versionData.PackageName).ToList();
                if (matchingPackages.Count == 0)
                {
                    Log.Warning($"No matching packages found in csproj file for {versionData.PackageName}");
                    continue;
                }

                foreach (var package in matchingPackages)
                {
                    if(package.VersionAsVersion < versionData.NewVersionAsVersion && package.Pin)
                    {
                        Log.Warning($"Not updating {package.Include} for {package.TargetFramework}, it is pinned to {package.Version}.  Manual verification recommended.");
                        continue;
                    }

                    if (package.VersionAsVersion < versionData.NewVersionAsVersion)
                    {
                        Log.Information($"Updating {package.Include} from {package.Version} to {versionData.NewVersion}");
                        var pattern = @"\d+(\.\d+){2,3}";
                        var result = Regex.Replace(csprojLines[package.LineNumber], pattern, versionData.NewVersion);
                        csprojLines[package.LineNumber] = result;

                        updateLog.Add($"- Package [{versionData.PackageName}]({versionData.Url}) " +
                            $"for {package.TargetFramework} " +
                            $"was updated from {versionData.OldVersion} to {versionData.NewVersion} " +
                            $"on {versionData.PublishDate.ToShortDateString()}.");
                    }
                }
            }

            await File.WriteAllLinesAsync(csprojPath, csprojLines);
            updateLog.Add("");
            return updateLog;
        }

        private static List<PackageReference> Parse(string[] csprojLines)
        {
            var packages = new List<PackageReference>();
            try
            {
                for (int i = 0; i < csprojLines.Length; i++)
                {
                    var line = csprojLines[i];
                    if (!line.Contains("PackageReference"))
                    {
                        continue;
                    }

                    var serializer = new XmlSerializer(typeof(PackageReference));
                    using (var reader = new StringReader(line))
                    {
                        var packageReference = (PackageReference)serializer.Deserialize(reader);
                        packageReference.LineNumber = i;
                        packages.Add(packageReference);
                    }
                }

                return packages;
            }
            catch (Exception e)
            {
                Log.Error(e, "XML issue");
                return packages;
            }
        }
    }
}
