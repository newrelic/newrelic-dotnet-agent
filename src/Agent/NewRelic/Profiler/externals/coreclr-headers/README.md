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

If a change in the profiler later requires a header that is not in the vendored set, or a bug fix lands in a still-maintained CoreCLR branch that we need, refresh by re-running the audit against a fresh checkout of `dotnet/coreclr`:

```powershell
git clone https://github.com/dotnet/coreclr.git C:\source\repos\coreclr
cd C:\source\repos\coreclr
git checkout release/3.1    # or whatever commit is being tracked

# Re-audit #include closure from the same seed set used originally:
# - profiler-source entry points
# - direct-compile corhlpr.cpp
# - coreclr header seeds (cor.h, corhlpr.h, corerror.h, corprof.h, pal.h, etc.)
# Walk transitive #include directives and copy each resolved header.
```

Any refresh must be accompanied by:

1. An update to the "Upstream commit" line above with the new SHA and date.
2. A binary-parity check confirming the rebuilt profiler binaries are equivalent to the pre-refresh binaries under the verification recipe in `PROFILER_MODERNIZATION_PLAN.md` (Linux: `readelf -d` DT_NEEDED set, `readelf -V` max GLIBC version, `nm -D --defined-only` exported symbols; Windows: `dumpbin /disasm` and `dumpbin /exports`).
3. Updates to `licenses/THIRD_PARTY_NOTICES.txt` if upstream license text or attribution changed.

## Do not hand-edit

Files in this directory are upstream copies. Do not patch them in place. If a patch is needed, carry it in profiler source (e.g., preprocessor overrides in a local header) rather than modifying vendored files — otherwise refreshes become merge conflicts.
