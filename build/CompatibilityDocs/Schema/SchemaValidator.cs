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

        var effectiveTabs = lib.Tabs is { Count: > 0 } ? (IEnumerable<string>)lib.Tabs : cat.Tabs;
        ValidateNotes(lib.Notes, where, effectiveTabs);
        ValidateMinAgentVersion(lib.MinAgentVersion, effectiveTabs, where);

        foreach (var pkg in lib.Packages)
        {
            var pw = $"package '{pkg.Id}' in {where}";
            RequireTabs(pkg.Tabs, pw);
            if (!AllowedVersionSources.Contains(pkg.VersionSource))
                throw new SchemaValidationException(
                    $"{pw}: versionSource '{pkg.VersionSource}' invalid. Allowed: {string.Join(", ", AllowedVersionSources)}.");

            RequireResolvableVersion(pkg.MinVersion, pkg, "minVersion", pw);
            if (pkg.VersionSource == "manual")
                RequireResolvableVersion(pkg.LatestVersion, pkg, "latestVersion", pw);

            ValidateMinAgentVersion(pkg.MinAgentVersion, pkg.Tabs, pw);
            ValidateNotes(pkg.Notes, pw, pkg.Tabs);
        }
    }

    // A curated min/latest must resolve to a non-empty value for every tab the package
    // declares, and a map form must not carry tab keys the package doesn't declare.
    private static void RequireResolvableVersion(VersionSpec? spec, Package pkg, string field, string pw)
    {
        if (spec == null)
            throw new SchemaValidationException($"{pw}: {field} is required.");

        foreach (var tab in spec.Tabs)
            if (!pkg.Tabs.Contains(tab))
                throw new SchemaValidationException(
                    $"{pw}: {field} has tab '{tab}' which the package does not declare.");

        foreach (var tab in pkg.Tabs)
            if (string.IsNullOrEmpty(spec.For(tab)))
                throw new SchemaValidationException(
                    $"{pw}: {field} does not resolve a version for declared tab '{tab}'.");
    }

    // minAgentVersion map keys must be a subset of the enclosing scope's tabs (a library's
    // effective tabs, or a package's declared tabs). Partial coverage (a tab with no entry)
    // is allowed — it just renders no suffix.
    private static void ValidateMinAgentVersion(VersionSpec? spec, IEnumerable<string> scopeTabs, string where)
    {
        if (spec == null || !spec.IsMap) return;
        var declared = new HashSet<string>(scopeTabs);
        foreach (var tab in spec.Tabs)
            if (!declared.Contains(tab))
                throw new SchemaValidationException(
                    $"{where}: minAgentVersion has tab '{tab}' which is not in the declared tabs.");
    }

    private static void RequireTabs(List<string> tabs, string where)
    {
        if (tabs.Count == 0)
            throw new SchemaValidationException($"{where}: 'tabs' must list at least one of: {string.Join(", ", AllowedTabs)}.");
        foreach (var t in tabs)
            if (!AllowedTabs.Contains(t))
                throw new SchemaValidationException($"{where}: tab '{t}' invalid. Allowed: {string.Join(", ", AllowedTabs)}.");
    }

    private static void ValidateNotes(List<Note> notes, string where, IEnumerable<string> declaredTabs)
    {
        var declaredSet = new HashSet<string>(declaredTabs);
        foreach (var note in notes)
        {
            if (note.Tabs != null)
            {
                if (note.Tabs.Count == 0)
                    throw new SchemaValidationException($"{where}: note 'tabs' must list at least one tab when present.");
                foreach (var t in note.Tabs)
                    if (!AllowedTabs.Contains(t))
                        throw new SchemaValidationException($"{where}: note tab '{t}' invalid. Allowed: {string.Join(", ", AllowedTabs)}.");
                foreach (var t in note.Tabs)
                    if (!declaredSet.Contains(t))
                        throw new SchemaValidationException($"{where}: note tab '{t}' is not in the declared tabs.");
            }

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
