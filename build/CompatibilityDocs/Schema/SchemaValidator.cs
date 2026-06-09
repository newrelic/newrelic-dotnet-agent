using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class SchemaValidator
{
    private static readonly HashSet<string> AllowedNoteTypes =
        new() { "addedInAgent", "maxSupportedVersion", "knownIncompatibleVersions", "requiresHybridAgent", "freeform" };

    private static readonly HashSet<string> AllowedTabs = new() { "core", "framework" };
    private static readonly HashSet<string> AllowedVersionSources = new() { "derived", "manual" };

    public virtual void Validate(CompatibilityModel model)
    {
        foreach (var cat in model.Categories)
        {
            RequireTabs(cat.Tabs, $"category '{cat.Key}'");
            foreach (var lib in cat.Libraries)
                ValidateLibrary(cat, lib);
        }
    }

    private void ValidateLibrary(Category cat, Library lib)
    {
        var where = $"library '{lib.Name}' in '{cat.Key}'";

        var hasPackages = lib.Packages.Count > 0;
        var hasSupportedVersions = lib.SupportedVersions is { Count: > 0 };
        var hasMethods = lib.Methods.Count > 0;
        if (!hasPackages && !hasSupportedVersions && !hasMethods)
            throw new SchemaValidationException(
                $"{where}: must define at least one of 'packages', 'supportedVersions', or 'methods'.");

        if (lib.Tabs != null) RequireTabs(lib.Tabs, where);
        ValidateNotes(lib.Notes, where);

        foreach (var pkg in lib.Packages)
        {
            var pw = $"package '{pkg.Id}' in {where}";
            RequireTabs(pkg.Tabs, pw);
            if (!AllowedVersionSources.Contains(pkg.VersionSource))
                throw new SchemaValidationException(
                    $"{pw}: versionSource '{pkg.VersionSource}' invalid. Allowed: {string.Join(", ", AllowedVersionSources)}.");
            if (pkg.VersionSource == "manual" && (string.IsNullOrEmpty(pkg.MinVersion) || string.IsNullOrEmpty(pkg.LatestVersion)))
                throw new SchemaValidationException(
                    $"{pw}: versionSource 'manual' requires both minVersion and latestVersion.");
            ValidateNotes(pkg.Notes, pw);
        }
    }

    private static void RequireTabs(List<string> tabs, string where)
    {
        if (tabs.Count == 0)
            throw new SchemaValidationException($"{where}: 'tabs' must list at least one of: {string.Join(", ", AllowedTabs)}.");
        foreach (var t in tabs)
            if (!AllowedTabs.Contains(t))
                throw new SchemaValidationException($"{where}: tab '{t}' invalid. Allowed: {string.Join(", ", AllowedTabs)}.");
    }

    private static void ValidateNotes(List<Note> notes, string where)
    {
        foreach (var note in notes)
        {
            if (!AllowedNoteTypes.Contains(note.Type))
                throw new SchemaValidationException(
                    $"{where}: note type '{note.Type}' invalid. Allowed: {string.Join(", ", AllowedNoteTypes)}.");

            switch (note.Type)
            {
                case "addedInAgent" when string.IsNullOrEmpty(note.SinceVersion) || string.IsNullOrEmpty(note.AgentVersion):
                    throw new SchemaValidationException($"{where}: addedInAgent requires sinceVersion and agentVersion.");
                case "maxSupportedVersion" when string.IsNullOrEmpty(note.Version):
                    throw new SchemaValidationException($"{where}: maxSupportedVersion requires version.");
                case "knownIncompatibleVersions" when string.IsNullOrEmpty(note.Text) && (note.Versions is not { Count: > 0 }):
                    throw new SchemaValidationException($"{where}: knownIncompatibleVersions requires text or versions.");
                case "freeform" when string.IsNullOrEmpty(note.Text):
                    throw new SchemaValidationException($"{where}: freeform requires text.");
            }
        }
    }
}
