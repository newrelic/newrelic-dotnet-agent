# CompatibilityDocs

Generates `docs/net-agent-compatibility.md` — the customer-facing list of
libraries the .NET agent instruments, with version ranges and notes.

## How it works

- Curated data lives in `compatibility.yaml` (validated by `compatibility.schema.json`).
- Tested version numbers (`versionSource: derived`) are computed at generation
  time from the integration-test `.csproj` files listed in
  `build/Dotty/projectInfo.json` plus `MultiFunctionApplicationHelpers.csproj`.
  Do not hand-edit derived versions.
- Packages with no test-project source use `versionSource: manual` with explicit
  `minVersion`/`latestVersion`.

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
