# CompatibilityDocs

Generates `docs/net-agent-compatibility.md` — the customer-facing list of
libraries the .NET agent instruments, with version ranges and notes.

## How it works

- Curated data lives in `compatibility.yaml` (validated by `compatibility.schema.json`).
- Every package requires a curated `minVersion` — the minimum supported version
  sourced from the public docs. This value is **never derived**; it must always be
  set explicitly in the YAML and supplies the lower bound of the "Supported versions"
  column in the generated table.
- `versionSource` governs only the **upper bound** (latest version):
  - `derived` (default): the latest version is computed at generation time by
    scanning the integration-test `.csproj` files listed in
    `build/Dotty/projectInfo.json` plus `MultiFunctionApplicationHelpers.csproj`.
    Do not hand-edit derived versions.
  - `manual`: the latest version comes from `latestVersion` in the YAML.
    `latestVersion` is required when `versionSource: manual`.

### `minVersion` and `latestVersion` shapes

Both fields accept either a scalar string (applies to all tabs the package
declares) or a per-tab map keyed by `core` and/or `framework`. A map must cover
every tab the package declares and must not include tabs it does not declare.

```yaml
# scalar — same value for all tabs
minVersion: "3.17.0"

# per-tab map — when the minimum differs by platform
minVersion:
  core: "3.2.0"
  framework: "2.0.0"
```

## Editing the schema

Open `compatibility.yaml` in an editor with the YAML language server (the VS Code
"YAML" extension, or Rider). The `# yaml-language-server` header wires up
autocomplete and validation from `compatibility.schema.json`. Note `type`s:
`addedInAgent`, `maxSupportedVersion`, `knownIncompatibleVersions`,
`requiresHybridAgent` (optional `aboveVersion`; omit it to mark the whole library
as hybrid-agent-only), `freeform`.

Quote version strings and any value ending in a colon (e.g. `version: "8.15.10"`,
`intro: "...datastores:"`); unquoted dotted versions and trailing colons confuse
the YAML parser. For typed notes (`addedInAgent`/`maxSupportedVersion`/
`knownIncompatibleVersions`) the renderer adds the trailing period — do not end
their `text` with a period (freeform notes are emitted verbatim, so keep theirs).

## Regenerate

```
dotnet run --project build/CompatibilityDocs
```

Output is deterministic (no timestamp); re-running with no data change yields no diff.

## Test

```
dotnet test build/CompatibilityDocs.Tests/CompatibilityDocs.Tests.csproj
```
