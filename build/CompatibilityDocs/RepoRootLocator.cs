using System;
using System.IO;

namespace CompatibilityDocs;

public static class RepoRootLocator
{
    public static string Find(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FullAgent.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Could not locate repo root (FullAgent.sln) above '{startDir}'.");
    }
}
