# Profiler Modernization Plan

*Draft for Marty's local reference. Will be copied to repo root post-approval and never committed.*

## Status as of 2026-05-15

**All in-scope items complete or deferred by explicit user direction.** Eight PRs landed or open:

| Item | PR | State |
|---|---|---|
| P1.1 — Eliminate `dotnet/coreclr` clone | #3576 | ✅ merged |
| P1.2 — PlatformToolset parametrization | #3579 | ✅ merged |
| P1.3 — `windows-2025-vs2026` + `v145` cutover | #3586 | ✅ merged |
| P2.2 — Kitware cmake (S3 dependency removed) | #3590 | ✅ merged |
| P2.4 — arm64 CI consolidation onto docker compose | #3593 | ✅ open, byte-identical binary verified |
| P3.1 — `apt-key` → keyring-file migration | #3591 | ✅ merged |
| P3.2 — `ProfiledMethods` → SDK-style csproj | #3585 | ✅ merged |
| P3.3 — Delete `Dockerfile.new` + README refresh | #3587 | ✅ merged |
| P3.4 — `format.lib` audit | #3583 | ✅ merged |
| (extra) Replace `darenm/Setup-VSTest` with vswhere | #3592 | ✅ merged |

**Deferred by explicit user direction:**
- **P2.1 / P2.3** — base-image swap and Linux toolchain modernization. Strategy is incremental hardening of the current Ubuntu 14.04/18.04 setup; deferred until a future dual-build (glibc + musl) initiative.


## Context

The New Relic .NET Profiler is a native C++ CLR profiler shipped with every agent release. It injects IL into customer code, so it is extraordinarily sensitive to changes in string handling, IL generation, and runtime compatibility. Historically, "innocuous" updates to the build pipeline have caused regressions. This plan targets **toolchain modernization only** — no changes to profiler source logic or C++ language standard.

**Drivers:**

1. **Windows build tooling on GitHub runners:** the exploration branch `ci/fix-profiler-build` (2026‑05‑05) tried `windows-2025-vs2026` and was abandoned (most probable cause: v143 ATL not installable alongside VS2026's primary v145 toolset). CI is pinned to `windows-2022`, which GitHub will retire.
2. **Coreclr clone:** the Profiler solution clones `https://github.com/dotnet/coreclr.git` at `release/3.1` on every build (Windows and Linux). That repo was archived after .NET Core 3.1 EOL (Dec 2022). This is a supply-chain and reliability risk.
3. **Linux build image (Ubuntu 14.04):** EOL ESM; depends on an expired-CA workaround, an `apt-key add` pattern deprecated in current Debian/Ubuntu, and pulls aarch64 cmake from an NR-owned S3 bucket (`virtuoso-testing.s3.us-west-2.amazonaws.com`) with no preservation guarantees.

**Constraints:**
- **Alpine/musl must keep working.** The user does not have a packaging pipeline that can update non-Alpine targets independently. Any modernization that breaks Alpine blocks all profiler updates.
- **Existing unit + integration tests must pass with identical results.** No test edits without explicit approval. New targeted string-handling tests are allowed.
- Moderate scope: toolchain modernization only. No C++ standard bump. No `std::wstring_convert` replacement. Profiler source logic unchanged.

---

## Alpine compatibility: empirical findings

This plan is grounded in a direct inspection of the shipped `libNewRelicProfiler.so` files (both architectures) plus a runtime test inside `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`. Full write-up to be produced separately; key facts:

**Binary shape of today's shipped profiler (inspected via `readelf`/`nm` on artifacts in `src/Agent/newrelichome_{x64,arm64}_coreclr_linux`):**

| Property | x64 | arm64 |
|---|---|---|
| DT_NEEDED | libm.so.6, libgcc_s.so.1, libpthread.so.0, libc.so.6 | libm.so.6, libgcc_s.so.1, libpthread.so.0, libc.so.6, ld-linux-aarch64.so.1 |
| Max GLIBC_ version referenced | **2.14** | **2.17** |
| libc++ / libstdc++ runtime dep | **None** (static-linked libstdc++) | None |
| Undefined dynamic symbols | 155 | similar |

**Runtime behavior on Alpine 3.23 (the `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` base image, no gcompat installed):**
- `ldd` reports three relocation errors (`RaiseException`, `strtoll_l`, `strtoull_l`), **yet the profiler loads and initializes successfully** under `dotnet`. The full init log emits, 30+ instrumentation XML files parsed, 372 instrumentation points identified, event mask set, method matching starts.

**Mechanism (factually determined):**
1. The profiler's DT_NEEDED is narrow and references only very old glibc symbol versions. Alpine's musl libc exposes `libc.so.6` / `libm.so.6` / `libpthread.so.0` sonames that satisfy the ELF loader, and musl provides native equivalents of nearly all referenced glibc symbols at those ancient version levels.
2. `RaiseException` is a CoreCLR PAL export, resolved at runtime from the already-loaded `libcoreclr.so` (dotnet loads the CLR before loading any profiler).
3. `strtoll_l` / `strtoull_l` / other `*_l` locale-aware symbols are unresolved in `ldd`, but the dotnet profiler loader uses `dlopen(RTLD_LAZY)`, so they only fail if/when actually called. The profiler's normal init and instrumentation path does not invoke them on Alpine.

**What this means for modernization:**
- Alpine compatibility today is not gcompat-based — it is a direct function of the binary having a **narrow DT_NEEDED**, **no libc++/libstdc++ runtime dep**, and a **low glibc symbol baseline (≤2.14 on x64, ≤2.17 on arm64)**.
- Any modern build image that produces a binary with the same characteristics will preserve Alpine compat. This is achievable on several currently supported toolchains.
- The three unresolved-symbol leaks (`strtoll_l`, `strtoull_l`) are a latent fragility — any code change that hits a locale-aware parsing path would surface them on Alpine. Not in scope for this plan, but worth tracking.

---

## Factual inventory (verified)

### Profiler solution (`src/Agent/NewRelic/Profiler/NewRelic.Profiler.sln`)

- **Platform toolset:** `v143` pinned in every native vcxproj. `ToolsVersion="15.0"`, manual `Microsoft.Cpp.Default.props` import.
- **WindowsTargetPlatformVersion:** `10.0` (no pinned SDK).
- **Language standard:** no `<LanguageStandard>` set — defaults to MSVC C++14.
- **Preprocessor:** `_ATL_ATTRIBUTES`; uses ATL. `stdafx.h` forced-included.
- **Link deps (Profiler.vcxproj):** `Sicily.lib` + `format.lib` + `version.lib`. `format.lib` provenance **unverified**.
- **Module definition:** `Profiler.def`.
- **Treat warnings as errors:** on.
- **CoreCLR clone target:** `CheckCoreClrPath` wired as `BuildDependsOn` in `Profiler.vcxproj`; clones `https://github.com/dotnet/coreclr.git` at `release/3.1` into `$(CORECLR_PATH)` (default `$(SolutionDir)..\..\..\..\..\coreclr`).
- **Include paths:** `$(CORECLR_PATH)\src\pal\prebuilt\inc` and `$(CORECLR_PATH)\src\inc`.
- **ProfiledMethods.csproj** uses legacy `packages.config`.

### Linux build (`src/Agent/NewRelic/Profiler/linux/`)

- **Dockerfile** (primary, used by CI): Ubuntu 14.04 (SHA-pinned), clang-3.9, cmake-3.9.0-rc3. Includes expired-CA workaround (removes `DST_Root_CA_X3`). Uses deprecated `apt-key add`. Clones coreclr `release/3.1` via a separate 22.04 stage.
- **Dockerfile.new:** Ubuntu 18.04 + clang-7. Has a comment: `# WARNING this will not build a profiler that works on Alpine Linux.` Not currently used.
- **Arm64Dockerfile:** Ubuntu 18.04 (SHA-pinned) + clang-3.9 + cmake 3.9.0-rc3-aarch64 from `virtuoso-testing.s3.us-west-2.amazonaws.com`. Also clones coreclr `release/3.1`.
- **CMakeLists.txt:** `cmake_minimum_required(VERSION 3.8)`; Linux flags `-std=c++11 -stdlib=libc++ -fno-strict-aliasing -fms-extensions -fdeclspec -fPIC` + `-static-libstdc++` on shared-linker. Compiles `$(CORECLR_PATH)/src/inc/corhlpr.cpp` directly.

### CI (`.github/workflows/build_profiler.yml`)

- Windows: `windows-2022`, `microsoft/setup-msbuild@v3`, VS 2022 Enterprise.
- Linux x64: `ubuntu-22.04`, `docker compose build build && docker compose run build`.
- Linux arm64: `uraimo/run-on-arch-action@v3.1.0` with `distro: ubuntu18.04`; pulls cmake from `virtuoso-testing` S3.

### Windows runner image contents (verified against actions/runner-images README)

- **windows-2022:** `VC.ATL`, `VC.ATLMFC`, `VC.Tools.x86.x64` (v143 primary), `ComponentGroup.VC.Tools.142.x86.x64`, SDKs 10-19041, 11-22621, 11-26100.
- **windows-2025-vs2026 (20260428.85.1):** `VC.ATL`, `VC.ATLMFC` (bound to VS2026's **v145** primary), `VC.Tools.x86.x64` (v145), **sidecar** `VC.14.44.17.14.x86.x64` (v143 compilers only — no paired ATL), `ComponentGroup.VC.Tools.142.x86.x64`. SDK `10.1.26100.7705`. **Missing:** Windows 10 SDK 19041, Windows 11 SDK 22621.

### Packaging (single-binary-per-arch model today)

- `.deb` / `.rpm` / `.tar.gz` / NuGet profiler package all ship **one `libNewRelicProfiler.so` per arch**, consumed from `newrelichome_{x64,arm64}_coreclr_linux`. No per-libc selection logic. `linux/README.md` claims Alpine compat for the current build (verified above).

---

## Prioritized work list

**P1 — Blockers / imminent supply-chain risk**
- ~~P1.1 Eliminate the `dotnet/coreclr` clone.~~ ✅ DONE — PR #3576
- ~~P1.2 Prepare Windows build for eventual migration off `windows-2022`.~~ ✅ DONE — PR #3579
- ~~P1.3 Cut over Windows CI to `windows-2025-vs2026` + `v145` toolset; update `Directory.Build.props` default.~~ ✅ MERGED — PR #3586.

**Strategic direction (2026-05-14):** user direction is **incremental stabilization** of the existing Ubuntu 14.04 / Ubuntu 18.04 setup, **not** a base-image swap. Goal is to harden the current build and position for a future dual-build (glibc + musl) architecture. Base-image modernization (manylinux2014 / manylinux_2_28 etc.) is deferred — see "Manylinux investigation" section below.

**P2 — Reliability / next 12 months (revised scope)**
- P2.1 ~~Replace Ubuntu 14.04 as the primary Linux x64 build image~~ **DEFERRED.** Stay on the current image; address fragility incrementally via P2.2 / P3.1 / arm64 consolidation.
- ~~P2.2 Replace `virtuoso-testing.s3.us-west-2.amazonaws.com` dependency for aarch64 cmake.~~ ✅ DONE — PR #3590. Kitware GitHub releases, cmake 3.20.5 pinned (last release built on glibc ≤2.17 baseline, validated against OTel .NET instrumentation's ubuntu1604.dockerfile). SHA-256 verified before extraction. CI green on chore/profiler-arm64-cmake-from-kitware.
- P2.3 ~~Modernize Linux toolchain (cmake, clang) in step with P2.1~~ **DEFERRED with P2.1.** Toolchain bumps would change binary output and require re-verifying all four binary properties.
- ~~**P2.4 (new) Consolidate arm64 CI onto `docker compose run build_arm64`.**~~ ✅ DONE — PR #3593. `docker/setup-qemu-action` + `docker compose run build_arm64`; deleted the duplicate inline install in `build_profiler.yml`. Binary-shape parity gate passed: produced arm64 `libNewRelicProfiler.so` is **byte-identical** to the prior `run-on-arch-action` output (same SHA-256, DT_NEEDED, GLIBC ceiling, no libc++/libstdc++, exported symbols).

**P3 — Hygiene**
- ~~P3.1 Replace deprecated `apt-key add` with keyring files.~~ ✅ DONE — PR #3591. Modern `signed-by=/etc/apt/keyrings/llvm.gpg` pattern in `Dockerfile`, `Arm64Dockerfile`, and the workflow inline install. Validated locally via Docker Desktop + buildx (linux/arm64 emulated) on both Dockerfiles; CI green on chore/profiler-replace-apt-key-with-keyring.
- ~~P3.2 Migrate `ProfiledMethods/packages.config` → PackageReference.~~ ✅ MERGED — PR #3585. SDK-style csproj conversion; IL-identical output verified; 313/313 native unit tests pass.
- ~~P3.3 Delete `Dockerfile.new` (unused, confusing); refresh `linux/README.md` with the verified Alpine mechanism.~~ ✅ MERGED — PR #3587
- ~~P3.4 Document or remove `format.lib`.~~ ✅ DONE — PR #3583 (compiled corhlpr.cpp in-tree, removed NETFXSDK dependency)

Out of scope by user direction: C++ standard bump, deprecated-API replacement (`std::wstring_convert`), resolving the `strtoll_l`/`strtoull_l` unresolved-symbol leak.

---

## Recommended approach

### P1.1 — CoreCLR clone elimination: **vendor the headers in-tree**

**Options considered:**
| | Pros | Cons |
|---|---|---|
| (a) Vendor needed headers in-tree | Zero network dep; fully reproducible; pinned forever. Aligns with the fact that the profiling API has been frozen since .NET Core 3.1. | One-time header audit; diverges from CoreCLR upstream. |
| (b) Clone `dotnet/runtime` at a pinned SHA | Still gets upstream fixes; active repo. | Header locations changed (`src/coreclr/pal/...` vs `src/pal/...`); still a network dep; still a clone-fragility risk. |
| (c) Source via Microsoft NuGet | Cleanest dependency model *if* available. | **Unverified** — no NuGet ships exactly the PAL/inc headers consumed. `Microsoft.NETCore.App.Runtime.*` does not. |

**Recommendation: (a) Vendor.** The profiling API is frozen; the clone adds zero ongoing value; it adds non-trivial ongoing risk.

**Execution:**
1. **Header audit (first action of P1.1).** Build once with the current clone, run preprocessor-only pass (`/P` on MSVC, `-E -M` on clang) against every source file, collect the exact set of headers consumed from `$(CORECLR_PATH)/src/`. Copy those and their transitive includes into `src/Agent/NewRelic/Profiler/externals/coreclr-headers/`. Also vendor `src/inc/corhlpr.cpp` (compiled directly into the profiler by CMakeLists.txt).
2. Add `src/Agent/NewRelic/Profiler/externals/coreclr-headers/README.md` documenting provenance (upstream commit SHA) and refresh script.
3. `Profiler/Profiler.vcxproj`: remove the `CheckCoreClrPath` target, `GitCoreClr` property, `CORECLR_PATH` defaults; point `<IncludePath>` / `<ExternalIncludePath>` to the vendored path.
4. `CMakeLists.txt`: replace `CORECLR_PATH` lookups with vendored path.
5. `linux/Dockerfile`, `Arm64Dockerfile`, `build_profiler.sh`, `.github/workflows/build_profiler.yml` (arm64 inline): remove coreclr clone commands.

**Verification (P1.1 gate):**
- Before change: capture `dumpbin /disasm` output for Windows x64 + x86 Release; capture `objdump -d` + `readelf -d` + `nm -D --defined-only` for Linux x64 + arm64.
- Apply only P1.1 (nothing else).
- Re-capture. **Both dumps must be byte-identical** — this is a pure sourcing change; if the diff is non-empty, something about which headers are in scope shifted.

### ~~P1.2~~ ✅ — Prepare Windows build for eventual migration (PR #3579)

Parametrized `PlatformToolset` across all 16 profiler vcxprojs via a shared `Directory.Build.props`. Default remained v143; the toolset became overridable via `-p:NativeToolset=vNNN` without editing any project file.

### ~~P1.3~~ ✅ — v143→v145 cutover + `windows-2025-vs2026` runner (PR #3586, pending integration gate)

**Root cause of prior failure confirmed:** v143 ATL is absent on `windows-2025-vs2026`; only v145 ATL ships with that runner. Building with v143 fails at link time.

**What was done:**
- `build_profiler.yml`: `windows-2022` → `windows-2025-vs2026` on both Windows jobs.
- `Directory.Build.props`: default `NativeToolset` updated from `v143` → `v145`.

**Verification completed:**
- 0 errors, 0 warnings on x64 + x86 Release with v145 (local and CI).
- Identical export surface (11 named exports, same ordinals) and DLL dependencies.
- 313/313 native unit tests pass on CI `windows-2025-vs2026`.

**Merge gate remaining:** full IntegrationTests, UnboundedIntegrationTests, and ContainerIntegrationTests (including AlpineX64) must pass. Binary output differs from v143 build by design — byte-identical is not the correct gate for a toolset upgrade.

**Verification (P1.2 gate):**
- Default build (no `NativeToolset` override) must produce byte-identical `NewRelic.Profiler.dll` to the pre-change build.

### P1.3 — v143→v145 cutover + `windows-2025-vs2026` runner

**Investigation findings (2026-05-14, local with VS 2025 / MSVC 14.50.x):**

| Check | Result |
|---|---|
| Build (x64 + x86 Release, `/p:NativeToolset=v145`) | ✅ 0 errors, 0 warnings |
| Native unit tests x64 (313 tests) | ✅ 313/313 pass |
| Native unit tests x86 | ✅ (test binary rebuilt with v145 on same run) |
| Exports (11 named exports) | ✅ Identical set, identical ordinals |
| DLL dependencies | ✅ Identical: KERNEL32, USER32, ADVAPI32, SHELL32, VERSION, SHLWAPI |
| Binary hash | ✅ Different (expected — new codegen) |

**Key finding:** ATL is present for v145 locally and produces a clean build. The `windows-2025-vs2026` CI image has v145 ATL as its primary — and has no v143 ATL sidecar — so passing `/p:NativeToolset=v145` is **required** on that runner (v143 would fail at link time due to missing ATL).

**New v145 behavior to be aware of:** compiler emits `"Structured output is enabled..."` info message on stderr. This is VS 2025's new structured diagnostic format, not a warning. CI log parsers that treat any stderr output as an error may need updating (none found in this repo).

**Pre-existing quirk (not a v145 regression):** the x86 `DllCanUnloadNow` export is COMDAT-folded with `std::codecvt_utf8::do_encoding` — both return 0; LTCG picks one body. Same behavior in v143 and v145; runtime-correct since the export is at the right address.

**Branch:** `ci/profiler-build-windows-2025-v145` — changes `build_profiler.yml`:
- `build-windows-profiler` and `package-and-deploy` jobs: `windows-2022` → `windows-2025-vs2026`
- Both MSBuild compile steps: add `-p:NativeToolset=v145`

**Gate:** CI must pass fully (build + unit tests + code coverage). Binary diff against the current `windows-2022` / v143 artifacts is expected to differ (codegen change) but must pass the same 313-test suite and produce the same export/import surface.

### P2.4 — arm64 CI consolidation (audit, 2026-05-14)

**Audit findings:**

`docker-compose.yml` defines three services:

| Service | Dockerfile | Used by CI? |
|---|---|---|
| `build` | `Dockerfile` | **Yes** — x64 job runs `docker compose build build && docker compose run build` |
| `build_debug` | `DebugDockerfile` | No — local debugging only |
| `build_arm64` | `Arm64Dockerfile` (`platform: linux/arm64/v8`) | **No — orphaned** |

The CI arm64 job (`build_profiler.yml` lines 222-246) uses `uraimo/run-on-arch-action@v3.1.0` with an inline `install:` block — **completely bypasses `Arm64Dockerfile` and the `build_arm64` service**. The inline install replicates the `Arm64Dockerfile` package list, LLVM repo + apt-key, S3 cmake download, and `/usr/bin/cc` symlinks verbatim. Two near-identical configurations kept in sync only by manual diligence.

**Why this matters for dual-build readiness:**

If we want to add a musl variant later, the natural model is "write `MuslDockerfile`, add a service in `docker-compose.yml`, point CI at it." That works for x64 today. It does **not** work for arm64 — arm64 CI doesn't use docker compose, so adding any new architecture/variant requires another inline-install block in workflow YAML.

**Proposed consolidation:**

In `build_profiler.yml` arm64 job, replace `uraimo/run-on-arch-action` with:
- `docker/setup-qemu-action` (registers binfmt for aarch64 emulation)
- `docker compose build build_arm64`
- `docker compose run build_arm64`

`Arm64Dockerfile` becomes the single source of truth for the arm64 build environment. Drift impossible.

**Risk:** non-trivial CI change. First run validates whether QEMU + docker compose on `ubuntu-22.04` runners produces a binary with the same four properties (DT_NEEDED set, GLIBC ≤2.17, no libc++/libstdc++ runtime dep, exported-symbol set) as today's `run-on-arch-action` build. If it diverges, reconcile before merging.

**Alternative (smaller, less valuable):** delete `Arm64Dockerfile` and acknowledge the inline-install model as authoritative. Removes orphan code; leaves the dual-build inconvenience in place.

**Smaller hardening items to land independently of P2.4:**

- ~~**P3.1 — keyring-file pattern.**~~ ✅ DONE — PR #3591 (2026-05-15).
- ~~**P2.2 — replace virtuoso-testing S3 cmake.**~~ ✅ DONE — PR #3590 (2026-05-15).

Each PR independently must pass binary-shape parity (DT_NEEDED, GLIBC ceiling, exported-symbol set) since the goal is hardening, not changing output.

### Cross-reference: OpenTelemetry .NET instrumentation as a dual-build template

The OTel project (https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation) already operates the dual-build model the user wants to position for. Their `docker/` directory contains separate per-target Dockerfiles — `alpine.dockerfile` (musl), `debian-arm64.dockerfile`, `debian.dockerfile`, `centos-stream9.dockerfile`, `ubuntu1604.dockerfile` (their oldest-glibc-baseline build) — each producing a binary tuned to its target. They build natively for each, then ship a multi-binary distribution.

Two specific findings from inspecting OTel's setup during the P2.2 work:

1. **cmake glibc cutover:** OTel's `ubuntu1604.dockerfile` uses cmake 3.20.5 with the comment "Kitware Xenial apt repo no longer serves cmake" — confirming the post-3.20 Kitware Linux binaries require glibc 2.28+. Their cmake 3.20.5 runs on Ubuntu 16.04 (glibc 2.23) in production CI; this is why our P2.2 pins to 3.20.5.
2. **Dual-build organization:** their separate per-target Dockerfiles + per-target build jobs in CI. When P2.4 lands and our arm64 build moves to docker compose + Arm64Dockerfile, adding a future `MuslDockerfile` would follow the OTel template mechanically.

### Manylinux investigation (2026-05-14, deferred)

Original P2.1-P2.3 recommendation was `quay.io/pypa/manylinux2014_x86_64` + `manylinux2014_aarch64`. That recommendation **stands as the right long-term answer** but was deferred by user direction in favor of incremental hardening.

**Status of the manylinux candidates as of 2026-05-14:**

| Image | Base | glibc | GCC | Alpine ceiling (≤2.17) |
|---|---|---|---|---|
| `manylinux2014_x86_64` | CentOS 7 (EOL'd 2024-06-30) | **2.17** ✅ | 10 | OK |
| `manylinux_2_28_x86_64` | AlmaLinux 8 | 2.28 ❌ | 14 | breaks Alpine |
| `manylinux_2_34_x86_64` | AlmaLinux 9 | 2.34 ❌ | 14 | breaks Alpine |

`manylinux2014` is the only PyPA image that holds the 2.17 ceiling. CentOS 7 base is EOL — yum repos are in vault status, no new system CVE patches. PyPA still maintains the toolchain layer (GCC, cmake, etc.) on top, but the underlying glibc/openssl/libcurl in the image are frozen. For an ephemeral CI build container that doesn't link against the host's openssl/libcurl, this is mostly cosmetic but will trigger compliance scans. PyPA still lists `manylinux2014` as supported with no announced sunset date.

**Re-evaluation trigger:** revisit when one of these happens:
- PyPA announces sunset of `manylinux2014`.
- A critical CVE is found in the `manylinux2014` toolchain layer that PyPA can't patch.
- The repo migrates to a dual-build (glibc + musl) packaging model — at which point the glibc-targeting image needs to come from somewhere stable, and `manylinux_2_28` becomes the natural choice (Alpine compat handled separately by the musl build).

### P2.1–P2.3 — Linux toolchain modernization (Alpine-preserving) — original recommendation, superseded above

Alpine compatibility is a hard constraint. The empirical finding above says this is achievable on any modern toolchain that produces a binary with:
- DT_NEEDED limited to `libm`, `libgcc_s`, `libpthread`, `libc`
- Max GLIBC symbol version ≤2.17
- No libc++ or libstdc++ runtime dep (static-link libstdc++, drop `-stdlib=libc++`)
- Lazy binding enabled

**Options considered:**

| Option | Description | Pros | Cons |
|---|---|---|---|
| (i) Keep Ubuntu 14.04, harden only | Pin SHA, drop expired-CA workaround if no longer needed, replace `apt-key`. Keep clang-3.9, cmake-3.9. | Zero risk of behavior change. | Ubuntu 14.04 is EOL ESM; upstream apt mirrors can disappear. Cost of kicking this can again: ~1 year max. |
| (ii) Manylinux-style base (CentOS 7 / glibc 2.17) | Use `quay.io/pypa/manylinux2014_x86_64` + `manylinux2014_aarch64` — PyPA's actively-maintained "widest-compatibility" build image. glibc 2.17, modern devtoolset clang, modern cmake. | Matches current arm64 baseline exactly. Supported, secure, widely used. Zero-cost Alpine compat preservation (same symbol ceiling as today). | Image switch; some header/flags tuning likely. |
| (iii) Dual build — glibc + musl side-by-side | Build glibc .so on modernized image + musl .so natively on Alpine. Ship both. Agent selects at install time. | Robust Alpine support (no lazy-binding luck); clean separation. Aligns with dotnet's own linux-x64 / linux-musl-x64 model. | Packaging changes: `.deb`/`.rpm`/`.tar.gz`/NuGet layouts must include both .so files + a selector shim. Non-trivial re-verification across the Linux test matrix. |
| (iv) Modern Ubuntu + symbol-capping | Ubuntu 24.04 + clang-18 + explicit `--sysroot` or `.symver` pragmas to cap glibc baseline. | Most modern tooling. | Fragile, easy to get wrong, poorly documented. Not recommended. |

**Recommendation: (ii) manylinux2014-style base images.** This is the lowest-risk way to modernize tooling while preserving the exact properties that make today's binary work on Alpine. It matches the approach Microsoft itself uses for `linux-x64` .NET runtime builds and the approach the Python ecosystem standardized for universally-compatible binaries.

Option (iii) is the "architecturally correct" long-term answer but is out of scope for a toolchain modernization plan — it's a packaging-pipeline overhaul. Recommend capturing it as a follow-up.

**Execution for (ii):**
1. **x64 Dockerfile rewrite** (`src/Agent/NewRelic/Profiler/linux/Dockerfile`):
   - Base: `quay.io/pypa/manylinux2014_x86_64` (pinned SHA).
   - Install cmake, clang from the image's devtoolset / PyPA-curated toolchain.
   - Remove expired-CA workaround (manylinux images have current CAs).
   - Remove `apt-key add` (CentOS 7 uses yum, not apt).
   - Remove coreclr clone (per P1.1).
2. **arm64 Dockerfile rewrite** (`Arm64Dockerfile`): use `quay.io/pypa/manylinux2014_aarch64`. Remove `virtuoso-testing` S3 cmake download.
3. **CMakeLists.txt:**
   - Raise `cmake_minimum_required` to match the new cmake.
   - Keep `-std=c++11` (user direction: no standard bump).
   - **Drop `-stdlib=libc++`** — today's build uses libc++ for compile but produces a binary with no libc++ runtime dep (static-linked libstdc++ + no used libc++ symbols). On the new image, libc++ is unlikely to be the default; use libstdc++ throughout. **This needs binary-diff verification** to confirm no observable change.
   - Keep `-static-libstdc++`.
   - Keep `-fPIC`, `-fms-extensions`, `-fdeclspec`, `-fno-strict-aliasing`.
   - Keep `file(GLOB SOURCES ...)` list exactly the same.
   - Update include paths per P1.1 (vendored headers).
4. **build_profiler.sh:** unchanged apart from removing `-DCORECLR_PATH=...`.
5. **docker-compose.yml:** update service build contexts if needed.
6. **CI arm64 job:** replace the `uraimo/run-on-arch-action` inline install with `docker compose` against the new `Arm64Dockerfile` (this simplifies the workflow and removes duplicate config).
7. **`Dockerfile.new`:** delete.
8. **`DebugDockerfile`:** align with (ii) or delete if unused.
9. **`linux/README.md`:** rewrite to document the actual Alpine-compat mechanism (narrow DT_NEEDED + low GLIBC baseline + no libc++ runtime) and the "don't break this property" rule.

**Verification (P2 gate — the critical one):**
1. **Binary shape parity** (must match today's binary on these properties):
   - DT_NEEDED list: identical set of `.so` names.
   - Max GLIBC_ version referenced (`readelf -V`): ≤ today's ceiling (x64 ≤2.14, arm64 ≤2.17). **If the new build raises the ceiling, the modernization fails this step and must be revised.**
   - No `libc++.so.*` or `libstdc++.so.*` in DT_NEEDED.
   - Exported symbol set (`nm -D --defined-only`): identical set of exported names (the .def-like interface the CLR looks up). Any add or drop = ABI change = stop.
2. **Alpine smoke test**: run the new binary under `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` with the exact test harness I used for the investigation. Profiler must log `Profiler initialized` and parse the instrumentation XML files.
3. **Full ContainerIntegrationTests.sln** matrix: `UbuntuX64`, `UbuntuArm64`, `CentosX64`, `CentosArm64`, `AmazonX64`, **`AlpineX64`**, `FedoraX64`. All must pass identically.
4. **Full IntegrationTests.sln + UnboundedIntegrationTests.sln**.
5. **Native unit tests** (CommonTest, ConfigurationTest, LoggingTest, MethodRewriterTest, SignatureParserTest, SicilyTest) — same pass set as before.

---

## Mandatory gate (applies to every step)

The user's rule: **existing unit + integration tests must pass with identical results. No test edits without explicit approval. New string-handling tests allowed.**

Every merge in this plan passes:

1. **Native profiler unit tests** (ProfilerTests suite): Debug + Release, x86 + x64. Before-and-after pass set must be identical.
2. **Full IntegrationTests.sln** on Windows. No new failures.
3. **Full UnboundedIntegrationTests.sln** with services container running. No new failures.
4. **Full ContainerIntegrationTests.sln** across all fixtures **including AlpineX64**. No new failures.

Plus the step-specific gate (binary byte-diff / DT_NEEDED parity / GLIBC ceiling / exported-symbol parity) described above.

### Allowed new tests (no existing edits)

Add to `CommonTest`/`MethodRewriterTest`/`SignatureParserTest`:
- UTF-16 surrogate-pair method names.
- Non-BMP characters in type names and namespace paths.
- Method signatures with deeply nested generics (e.g., `List<Dictionary<string, List<string>>>`).
- Round-trip: raw method signature bytes → parsed → re-emitted → byte-identical.

### Local run cheat-sheet (copy into PR descriptions)

```powershell
# From repo root. Requires FullAgent.sln to be built first.
$sln = "$((Resolve-Path .).Path)\"
$nr = "src\Agent\NewRelic"

# Windows native tests:
vstest.console.exe /Platform:x64 `
  "$nr\Profiler\CommonTest\bin\x64\Release\CommonTest.dll" `
  "$nr\Profiler\ConfigurationTest\bin\x64\Release\ConfigurationTest.dll" `
  "$nr\Profiler\LoggingTest\bin\x64\Release\LoggingTest.dll" `
  "$nr\Profiler\MethodRewriterTest\bin\x64\Release\MethodRewriterTest.dll" `
  "$nr\Profiler\SignatureParserTest\bin\x64\Release\SignatureParserTest.dll" `
  "$nr\Profiler\Sicily\SicilyTest\bin\x64\Release\SicilyTest.dll"
# Repeat with /Platform:x86.

# Windows binary parity:
dumpbin /disasm src\Agent\_profilerBuild\x64-Release\NewRelic.Profiler.dll > NewRelic.Profiler.x64.after.txt

# Linux binary parity (inside a container):
docker run --rm -v "/path/to/newrelichome_x64_coreclr_linux:/t:ro" ubuntu:22.04 bash -c "
  apt-get update -qq >/dev/null && apt-get install -y -qq binutils >/dev/null
  readelf -d /t/libNewRelicProfiler.so | grep -E 'NEEDED|SONAME'
  readelf -V /t/libNewRelicProfiler.so | sed -n '/Version needs/,/Version definitions/p'
  nm -D --defined-only /t/libNewRelicProfiler.so | sort
"

# Alpine smoke:
docker run --rm -v "/path/to/newrelichome_x64_coreclr_linux:/agent:ro" \
  mcr.microsoft.com/dotnet/sdk:10.0-alpine sh -c "
  export CORECLR_ENABLE_PROFILING=1
  export CORECLR_PROFILER='{36032161-FFC0-4B61-B559-F6C5D41BAE5A}'
  export CORECLR_PROFILER_PATH=/agent/libNewRelicProfiler.so
  export CORECLR_NEW_RELIC_HOME=/agent
  export NEW_RELIC_LOG_DIRECTORY=/tmp
  export NEW_RELIC_LICENSE_KEY=abc
  dotnet --version
  grep 'Profiler initialized' /tmp/*.log
"
```

---

## Execution order (proposed PR sequence)

Each PR independent, each passing the full mandatory gate plus its step-specific gate:

1. **PR-1 (P1.1, header vendoring).** Binary parity must be byte-identical (no other changes).
2. **PR-2 (P1.2, PlatformToolset parametrization, default unchanged).** Binary parity must be byte-identical. Optional: run an exploratory CI job on `windows-2025-vs2026` with `/p:NativeToolset=v145` to capture the actual MSBuild error for the future cutover plan.
3. **PR-3 (P3.4, `format.lib` audit).** May be no-op. If removable, binary parity must be byte-identical.
4. **PR-4 (P3.2, ProfiledMethods to PackageReference).** Native output unchanged (ProfiledMethods is a test app).
5. **PR-5 (P2.1–P2.3 + P3.1 + P3.3, Linux toolchain modernization).** Binary parity enforced on: DT_NEEDED set, max GLIBC, no libc++/libstdc++ dep, exported-symbol set. Alpine container smoke and full integration suite required.

The v143→v145 cutover is **not** part of this plan. Schedule it as a separate follow-up once PR-2 has landed and the exploratory CI job has told us what specifically fails on VS2026.

---

## Critical files

**Profiler native sources / project files:**
- `src/Agent/NewRelic/Profiler/NewRelic.Profiler.sln`
- `src/Agent/NewRelic/Profiler/Profiler/Profiler.vcxproj` — hosts the coreclr clone target, toolset, ATL defines
- `src/Agent/NewRelic/Profiler/Common/Common.vcxproj` + `Common/Strings.h` + `Common/xplat.h` — **do not modify** (string-handling surface)
- `src/Agent/NewRelic/Profiler/CMakeLists.txt`

**Build infrastructure:**
- `src/Agent/NewRelic/Profiler/build/build.ps1`
- `src/Agent/NewRelic/Profiler/build/scripts/build_linux.ps1`
- `src/Agent/NewRelic/Profiler/linux/Dockerfile` (primary — rewrite)
- `src/Agent/NewRelic/Profiler/linux/Arm64Dockerfile` (rewrite)
- `src/Agent/NewRelic/Profiler/linux/Dockerfile.new` (delete)
- `src/Agent/NewRelic/Profiler/linux/DebugDockerfile`
- `src/Agent/NewRelic/Profiler/linux/build_profiler.sh`
- `src/Agent/NewRelic/Profiler/docker-compose.yml`

**Packaging (confirm single-binary model is preserved):**
- `build/Linux/build/deb/build.sh`, `control`, `postinst`
- `build/Linux/build/rpm/build.sh`, `newrelic-dotnet-agent.spec`
- `build/Packaging/NugetProfiler/NewRelic.Profiler.nuspec`

**CI:**
- `.github/workflows/build_profiler.yml`

**Tests — do not edit without explicit approval:**
- `src/Agent/NewRelic/Profiler/CommonTest/`, `ConfigurationTest/`, `LoggingTest/`, `MethodRewriterTest/`, `SignatureParserTest/`, `Sicily/SicilyTest/`
- `tests/Agent/IntegrationTests/IntegrationTests.sln`
- `tests/Agent/IntegrationTests/UnboundedIntegrationTests.sln`
- `tests/Agent/IntegrationTests/ContainerIntegrationTests.sln`
- `tests/Agent/IntegrationTests/ContainerIntegrationTests/Fixtures/ContainerTestFixtures.cs` (AlpineX64 fixture)

---

## Open items / unverified claims flagged

Items to resolve **as the first action of execution**, not claim now:

1. **`format.lib` provenance** — audit during P3.4.
2. ~~**Exact root cause of `windows-2025-vs2026` build failure**~~ — ✅ Resolved (2026-05-14). The ATL-not-paired-with-v143 theory was correct. Confirmed by local build: v145 builds cleanly with 0 errors/warnings and all 313 unit tests pass. Branch `ci/profiler-build-windows-2025-v145` ready for CI validation. On `windows-2025-vs2026`, v143 would still fail (no v143 ATL); v145 must be used.
3. **Full coreclr header list actually consumed** — produce via preprocessor audit as the first execution step of P1.1. Do not vendor by guess.
4. **manylinux2014 image pin** — select and pin an exact image digest for both x64 and aarch64 variants; document renewal policy.
5. **libc++ → libstdc++ switch in CMakeLists.txt** — the empirical evidence is that today's binary has **no libc++ runtime dep**, so removing `-stdlib=libc++` *should* be a no-op on binary shape. Must be confirmed by binary-shape parity tests before merge.
6. **Resolution of `strtoll_l` / `strtoull_l` unresolved symbols on Alpine** — latent fragility in today's binary. Out of scope for this plan; capture as a follow-up ticket.

---

## Follow-ups captured (not in this plan)

- Dual-build architecture (glibc + musl separate .so files) — the "architecturally correct" long-term Alpine answer. Requires packaging-pipeline overhaul.
- `strtoll_l`/`strtoull_l` elimination in profiler source — small source change, removes latent Alpine fragility. Requires its own binary-diff analysis.
- v143 → v145 MSVC toolset cutover — scheduled once PR-2 is merged and CI has characterized the VS2026 failure.
- C++ standard bump (C++17/20) — explicitly deferred by user.
- `std::wstring_convert` replacement (removed in C++26) — deferred.
