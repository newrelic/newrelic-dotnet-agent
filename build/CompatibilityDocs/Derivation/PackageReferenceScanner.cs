using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace CompatibilityDocs.Derivation;

public class PackageReferenceScanner
{
    public virtual IReadOnlyList<PackageRef> Scan(string csprojPath)
    {
        var result = new List<PackageRef>();
        var root = ProjectRootElement.Open(csprojPath);
        if (root == null)
            return result;

        foreach (var itemGroup in root.ItemGroups)
        {
            foreach (var item in itemGroup.Items.Where(i => i.ItemType == "PackageReference"))
            {
                var version = item.Metadata.FirstOrDefault(m => m.Name == "Version")?.Value;
                if (string.IsNullOrEmpty(version))
                    continue; // central-version or version-less reference: nothing to derive

                result.Add(new PackageRef(item.Include, version, ParseTfm(item.Condition)));
            }
        }
        return result;
    }

    private static string? ParseTfm(string? condition)
    {
        if (condition?.StartsWith("'$(TargetFramework)'") ?? false)
            return condition.Split("==").LastOrDefault()?.Trim('\'', ' ', ';');
        return null;
    }
}
