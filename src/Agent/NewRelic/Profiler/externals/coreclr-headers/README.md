# Vendored CoreCLR Headers

This directory contains a frozen copy of CLR profiling-API headers from `dotnet/coreclr`. They are consumed by the native profiler build.

## Provenance

- Upstream repository: https://github.com/dotnet/coreclr
- Upstream branch: `release/3.1`
- Upstream commit: `1b04187394cd1de5b316c40a3ad9d9189f4cb5b0` (2023-01-20) — the final commit on `release/3.1` before the repository was archived
- License: MIT (see `LICENSE.TXT` in this directory)

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
3. Updating the "Upstream repository" and "Upstream commit" lines in the
   **Provenance** section above to reflect the new source and SHA.
4. Updates to `licenses/THIRD_PARTY_NOTICES.txt` if license text or
   attribution changed (`dotnet/runtime` is also MIT).

## Do not hand-edit

Files in this directory are upstream copies. Do not patch them in place. If a patch is needed, carry it in profiler source (e.g., preprocessor overrides in a local header) rather than modifying vendored files — otherwise refreshes become merge conflicts.
