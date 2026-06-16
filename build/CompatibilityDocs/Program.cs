using System;
using System.IO;
using System.Linq;
using CompatibilityDocs.Derivation;
using CompatibilityDocs.Rendering;
using CompatibilityDocs.Schema;
using Serilog;

namespace CompatibilityDocs;

public static class Program
{
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        try
        {
            var repoRoot = ArgValue(args, "--repo-root") ?? RepoRootLocator.Find(AppContext.BaseDirectory);
            var schemaPath = ArgValue(args, "--schema")
                ?? Path.Combine(repoRoot, "build", "CompatibilityDocs", "compatibility.yaml");
            var outputPath = ArgValue(args, "--out")
                ?? Path.Combine(repoRoot, "docs", "net-agent-compatibility.md");

            Log.Information("Loading schema {Schema}", schemaPath);
            var model = new SchemaLoader().LoadFromFile(schemaPath);

            Log.Information("Validating schema");
            new SchemaValidator().Validate(model);

            Log.Information("Scanning test projects for versions");
            var loader = new ProjectListLoader();
            var scanner = new PackageReferenceScanner();
            var refs = loader.GetProjectPaths(repoRoot)
                .Where(File.Exists)
                .SelectMany(scanner.Scan)
                .ToList();
            var versions = new VersionResolver().BuildIndex(refs);

            CheckDerivedPackagesResolve(model, versions);

            Log.Information("Rendering markdown");
            var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, md);
            Log.Information("Wrote {Out}", outputPath);
            return 0;
        }
        catch (SchemaValidationException ex)
        {
            Log.Error("Schema validation failed: {Message}", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Generation failed");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void CheckDerivedPackagesResolve(CompatibilityModel model,
        System.Collections.Generic.IReadOnlyDictionary<(string, Platform), string> versions)
    {
        foreach (var cat in model.Categories)
        foreach (var lib in cat.Libraries)
        foreach (var pkg in lib.Packages.Where(p => p.VersionSource == "derived"))
        {
            var id = pkg.Id.ToLowerInvariant();
            var resolvesCore = versions.ContainsKey((id, Platform.Core));
            var resolvesFw = versions.ContainsKey((id, Platform.Framework));
            if (!resolvesCore && !resolvesFw)
                throw new SchemaValidationException(
                    $"Derived package '{pkg.Id}' (library '{lib.Name}') was not found in any scanned test project. " +
                    $"Fix the package id, or set versionSource: manual with explicit versions.");

            foreach (var tab in pkg.Tabs)
            {
                var platform = PlatformParser.Parse(tab);
                if (!versions.ContainsKey((id, platform)))
                    Log.Warning("Package {Pkg} declares tab '{Tab}' but no tested version was found there.", pkg.Id, tab);
            }
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
