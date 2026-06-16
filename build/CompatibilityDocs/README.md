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

### `minAgentVersion` shape

`minAgentVersion` accepts the same scalar-or-map shape as `minVersion` and
`latestVersion`. A scalar applies to all tabs; a `{core, framework}` map can
be **partial** — a tab with no entry simply renders no minimum-agent version.
(This differs from `minVersion`, which must resolve for every declared tab.)

```yaml
# scalar — same minimum agent version for all tabs
minAgentVersion: "10.29.0"

# per-tab map — may be partial; omitted tabs render no min-agent version
minAgentVersion:
  core: "10.29.0"
  framework: "10.0.0"
```

`minAgentVersion` may also be set on an individual **package**, where it
overrides the library-level value for that package's rows only. Use this when
one package in a family needs a newer agent than its siblings — e.g. the ODBC
NuGet package (`System.Data.Odbc`) requires a later agent than the built-in
`System.Data` assembly, which has no minimum at all:

```yaml
- name: System.Data.ODBC
  packages:
    - id: System.Data           # built-in assembly — no min-agent
      tabs: [framework]
      versionSource: manual
      minVersion: "4.6.2"
      latestVersion: "4.8"
    - id: System.Data.Odbc      # NuGet package — its own min-agent
      tabs: [core, framework]
      minVersion: "8.0.0"
      minAgentVersion: "10.36.0"
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

### Per-note `tabs:` field

By default a note is shared — it renders under every tab the enclosing library or
package appears in. To restrict a note to a subset of those tabs, add a `tabs:`
list. The list must be non-empty and contain only tabs the enclosing scope
actually declares (package-level note → the package's own tabs; library-level note
→ the library's effective tabs).

```yaml
notes:
  - type: freeform
    text: "Supported on all platforms."
    # no tabs: field — renders under every tab

  - type: freeform
    text: "On .NET Framework, supported beginning with agent v9.7.0."
    tabs: [framework]   # renders only under the framework tab
```

### Library shapes

A library declares its content one of four ways, which control how it renders:

- **`packages`** — one table row per package (version range from `minVersion` +
  derived/manual latest).
- **`supportedVersions`** — a curated bullet list outside the table (free-text
  version strings; no NuGet package or derivation).
- **`methods`** — a single row with a collapsible instrumented-methods list.
- **`notes` only** — a single table row with dashes for the package, versions, and
  min-agent columns, carrying just the notes. Use this for libraries the agent
  instruments generically with no NuGet package or version to report (e.g. IBM DB2,
  matched by its `DB2Command` ADO.NET type). A library must define at least one of
  these four.

## Regenerate

```
dotnet run --project build/CompatibilityDocs
```

Output is deterministic (no timestamp); re-running with no data change yields no diff.

## Test

```
dotnet test build/CompatibilityDocs.Tests/CompatibilityDocs.Tests.csproj
```
