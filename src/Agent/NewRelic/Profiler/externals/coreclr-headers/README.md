# Vendored CoreCLR Headers

This directory contains a frozen copy of CLR profiling-API headers from `dotnet/coreclr`. They are consumed by the native profiler build.

## Provenance

Base set (everything except the per-file overrides listed below):

- Upstream repository: https://github.com/dotnet/coreclr
- Upstream branch: `release/3.1`
- Upstream commit: `1b04187394cd1de5b316c40a3ad9d9189f4cb5b0` (2023-01-20) — the final commit on `release/3.1` before the repository was archived
- License: MIT (see `LICENSE.TXT` in this directory)

### Per-file overrides

`dotnet/coreclr` was archived at .NET Core 3.1, so headers describing
profiling-API surface added after 3.1 must come from `dotnet/runtime`. Files
refreshed from there are listed individually so the rest of the set stays
pinned to the base commit above:

| File | Source repository | Tag | Commit | Blob SHA-1 | Reason |
|------|-------------------|-----|--------|-----------|--------|
| `src/pal/prebuilt/inc/corprof.h` | https://github.com/dotnet/runtime | `v5.0.17` | `6a984143635bde23e728abaaccbde52f5ea8fa3e` | `9a1e4b490a66f5a88a34e814c3d4c01256a937f0` | Adds `ICorProfilerCallback10` and `ICorProfilerInfo12` (EventPipe profiling API), needed for allocation sampling |

Upstream path for that file at that tag is
`src/coreclr/src/pal/prebuilt/inc/corprof.h` (the `src/coreclr/src` → `src/coreclr`
flattening happened after .NET 5, so newer tags use `src/coreclr/pal/prebuilt/inc/`).
The copy is byte-identical to upstream — verify with
`git hash-object src/pal/prebuilt/inc/corprof.h`, which must print the Blob SHA-1
above.

`v5.0.17` is deliberately chosen over a newer tag: .NET 5 is the release that
introduced `ICorProfilerCallback10` / `ICorProfilerInfo12`, and it is the last
release whose `corprof.h` stops there. Pinning to its final servicing tag gives
the required surface as a whole-file (never hand-edited) copy without dragging in
later API surface (`ICorProfilerCallback11`, `ICorProfilerInfo13`+) that the
profiler does not use. Bump this pin only when a newer interface is actually
needed.

## Why this is vendored

The `dotnet/coreclr` repository was archived after .NET Core 3.1 reached EOL in December 2022. Historically the profiler build cloned that repository on every build to resolve CLR profiling-API headers (`corprof.h`, `cor.h`, etc.). Cloning an archived repo adds network dependency, build fragility, and supply-chain risk for no benefit — the CLR profiling API has been stable since .NET Core 3.1 and the newer `dotnet/runtime` repository ships equivalent headers in different locations that our build does not use.

Vendoring this exact set of headers eliminates the clone and pins us to a known, stable surface.

## What is here

The vendored set is the transitive closure of `#include` directives starting from:

- The direct-compile source `src/inc/corhlpr.cpp` (included in the Linux build via `CMakeLists.txt`)
- The profiler-included coreclr headers: `cor.h`, `corhlpr.h`, `corerror.h`, `corprof.h`, `pal.h`, `pal_assert.h`, `palprivate.h`, `palrt.h`, `opcode.def`
- Headers the profiler source includes whose Linux resolution goes through coreclr's PAL tree: `atl.h`, `atlbase.h`, `atlcomcli.h`, `windows.h`, `shellapi.h`, `shlobj.h`, `tlhelp32.h`, and their transitive dependencies

Paths below this directory mirror the upstream layout exactly, so `#include <corprof.h>` continues to resolve without source changes:

```
externals/coreclr-headers/
├── LICENSE.TXT
├── README.md   (this file)
└── src/
    ├── inc/                  (Windows + Linux)
    ├── pal/inc/              (Linux)
    ├── pal/inc/rt/           (Linux)
    └── pal/prebuilt/inc/     (Windows + Linux)
```

## How to refresh

The `dotnet/coreclr` repository is archived and will receive no further commits.
If the profiler requires a header not present in the vendored set (e.g., a newer
`ICorProfilerCallback` interface), the source of truth is now `dotnet/runtime`:

    https://github.com/dotnet/runtime

The equivalent headers live under `src/coreclr/inc/` and
`src/coreclr/pal/inc/` in that repository. A refresh would require:

1. Identifying the minimum set of additional headers needed (walk `#include`
   directives from the new entry point).
2. Copying only the required files into this vendored tree, preserving the
   upstream path layout.
3. Pinning the source to a **tagged release**, never a moving branch, and
   recording it in the **Per-file overrides** table in the **Provenance**
   section above (file, repo, tag, commit, blob SHA-1, reason). Prefer the
   oldest tag that contains the needed surface, so the refresh does not pull in
   unrelated newer API.
4. Updates to `licenses/THIRD_PARTY_NOTICES.txt` if license text or
   attribution changed (`dotnet/runtime` is also MIT).

## Do not hand-edit

Files in this directory are upstream copies. Do not patch them in place. If a patch is needed, carry it in profiler source (e.g., preprocessor overrides in a local header) rather than modifying vendored files — otherwise refreshes become merge conflicts.
