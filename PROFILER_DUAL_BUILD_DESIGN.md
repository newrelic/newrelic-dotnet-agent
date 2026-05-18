# Profiler Dual-Build (glibc + musl) Design

## Status as of 2026-05-18

Investigation pass complete. Two local musl-native build spikes completed:
spike1 (2026-05-15) confirmed the libc++-static-not-PIC blocker; spike2 (2026-05-18)
validated the full libstdc++ migration — binary meets all four properties,
smoke test passes. See [`PROFILER_MUSL_SPIKE_REPORT.md`](PROFILER_MUSL_SPIKE_REPORT.md)
for full raw data. No code changes merged yet — this doc is the input to a
phased plan that will be refined and approved before any execution PR.

---

## Reframing the constraint (2026-05-15 revision)

Today's shipped `libNewRelicProfiler.so` works on Alpine because its build has
**all four** of these properties:

1. `DT_NEEDED` limited to `libm`, `libgcc_s`, `libpthread`, `libc` (and
   `ld-linux-aarch64` on arm64).
2. Max `GLIBC_` symbol version referenced ≤ 2.17.
3. No `libc++` / `libstdc++` in `DT_NEEDED` (statically linked).
4. Lazy binding (`RTLD_LAZY`) enabled.

Documented in `src/Agent/NewRelic/Profiler/linux/README.md`. **All four
properties were load-bearing only for one purpose: making the glibc binary
also load on Alpine via lazy-binding luck.** Once the dual-build ships a
real musl-native binary, Alpine compatibility is provided by that binary —
the glibc binary's job collapses to "support actually-glibc distros."

The constraint that replaces "≤ 2.17" is therefore not a fixed number; it
is **"≤ glibc-of-the-oldest-supported glibc distro."** That is a product
matrix question, addressed in Phase 3 below. The dual-build's real value
is that it **decouples Alpine compatibility from glibc-baseline
conservatism**, freeing the glibc build to modernize.

The four-property invariant is preserved **on the musl binary** for a
different reason — its base image must be Alpine-native and produce a
binary whose only `DT_NEEDED` libc reference is to musl's `libc.musl-*.so.1`,
not glibc symbols at any version. The "no libc++ / libstdc++ runtime dep"
property remains universal across both binaries: it's good hygiene
(static-linking the C++ runtime is what lets a single .so be deployed on
arbitrary distros without a libstdc++ version dance).

---

## (1) How OpenTelemetry .NET instrumentation does it

Four mechanisms, kept independent:

### 1a. Per-target Dockerfiles, one binary per (libc, arch)

`docker/` directory:

| File | Target | Notes |
|---|---|---|
| `alpine.dockerfile` | linux-musl-{x64,arm64} | Native musl build. `mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23` + apk-installed `clang21`, `cmake 4.1.3`, `alpine-sdk`. Sets `IsAlpine=true` in env. |
| `ubuntu1604.dockerfile` | linux-x64 (legacy glibc baseline) | Ubuntu 16.04 + clang-5.0 + g++-9 + cmake 3.20.5 from Kitware GH releases. The "low-glibc-ceiling" build. Sets `IsLegacyUbuntu=true`. |
| `debian.dockerfile`, `debian-arm64.dockerfile`, `centos-stream9.dockerfile` | Test-runtime images | Used by `ci.yml` for the integration-test matrix (download prebuilt artifact, run inside the target distro container). Not used to produce release artifacts. |

CI (`build.yml`) builds release artifacts on the runner host (Ubuntu 22.04 / Ubuntu 22.04 ARM / macOS 14) for "modern" targets, and inside the Alpine container for the two musl targets, plus a separate `ubuntu1604.dockerfile` job that produces the legacy-glibc x64 binary which is **swapped in over the host-built linux-x64 binary** via the `replace-build-artifacts` action. So x64 effectively comes from the legacy-glibc image, x64-musl + arm64-musl come from Alpine, arm64 (glibc) comes from `ubuntu-22.04-arm`, and Windows / macOS come from native runners.

### 1b. Multi-binary tracer-home tree

After the build jobs complete, the per-artifact `bin/tracer-home` trees are produced (each release-artifact-producing job uploads its own copy with the appropriate subtree populated). For the NuGet release, `Build.NuGet.Steps.cs` then assembles them into a single `runtimes/<RID>/native/` layout — see Section 1d. The conceptual merged shape is:

```
bin/tracer-home/
├── net/                         # managed code, libc-independent
│   ├── OpenTelemetry.AutoInstrumentation.dll
│   └── ...
├── linux-x64/
│   └── OpenTelemetry.AutoInstrumentation.Native.so       # built on ubuntu1604
├── linux-arm64/
│   └── OpenTelemetry.AutoInstrumentation.Native.so       # built on ubuntu-22.04-arm
├── linux-musl-x64/
│   └── OpenTelemetry.AutoInstrumentation.Native.so       # built in alpine.dockerfile
├── linux-musl-arm64/
│   └── OpenTelemetry.AutoInstrumentation.Native.so       # built in alpine.dockerfile
├── osx-arm64/
├── win-x64/
└── win-x86/
```

The directory names are the canonical .NET RID (Runtime Identifier) strings
(`linux-x64`, `linux-musl-x64`, etc.). This matters for the NuGet package
(see 1d) — the dotnet SDK auto-resolves `runtimes/<RID>/native/` based on the
target RID.

The mechanism that decides which directory a given build's `.so` lands in is
in `build/Build.Steps.Linux.cs` lines 63–67:

```csharp
string clrProfilerDirectoryName = Environment.GetEnvironmentVariable("OS_TYPE") switch
{
    "linux-musl" => $"linux-musl-{platform}",
    _            => $"linux-{platform}"
};
```

`OS_TYPE=linux-musl` is set on the Alpine matrix entries in `build.yml`; default ("not-set") yields `linux-{x64|arm64}`.

### 1c. Selection at process-start time (`instrument.sh`)

The user does **not** pre-pick a binary at install time. Selection is at
process startup, in `instrument.sh`:

```sh
case "$(uname -s | tr '[:upper:]' '[:lower:]')" in
  linux*)
    if [ "$(ldd /bin/ls | grep -m1 'musl')" ]; then
      OS_TYPE="linux-musl"
    else
      OS_TYPE="linux-glibc"
    fi
    ;;
  ...
esac

case "$OS_TYPE" in
  linux-glibc) DOTNET_RUNTIME_ID="linux-$ARCHITECTURE" ;;
  linux-musl)  DOTNET_RUNTIME_ID="linux-musl-$ARCHITECTURE" ;;
esac

export CORECLR_PROFILER_PATH="$OTEL_DOTNET_AUTO_HOME/$DOTNET_RUNTIME_ID/OpenTelemetry.AutoInstrumentation.Native.so"
```

The libc probe is `ldd /bin/ls | grep -m1 musl` — robust because `/bin/ls`
exists everywhere and `ldd` output mentions `musl-*.so.1` only on musl.

### 1d. Two distribution shapes

**For end users**: per-(libc, arch) ZIPs uploaded to the GitHub Release.
`release.yml` produces:

```
opentelemetry-dotnet-instrumentation-linux-glibc-x64.zip
opentelemetry-dotnet-instrumentation-linux-glibc-arm64.zip
opentelemetry-dotnet-instrumentation-linux-musl-x64.zip
opentelemetry-dotnet-instrumentation-linux-musl-arm64.zip
opentelemetry-dotnet-instrumentation-windows.zip
opentelemetry-dotnet-instrumentation-macos.zip
```

`otel-dotnet-auto-install.sh` probes libc on the install host, derives the
correct ZIP name, downloads it, unzips into `$OTEL_DOTNET_AUTO_HOME`. So each
ZIP is **per-libc**, not a fat archive — but the partitioning happens at the
download step, not at runtime.

**For NuGet consumers**: a single `OpenTelemetry.AutoInstrumentation.Runtime.Native` package using the canonical .NET runtime-pack layout — `runtimes/<RID>/native/`:

```
runtimes/linux-x64/native/OpenTelemetry.AutoInstrumentation.Native.so
runtimes/linux-musl-x64/native/...
runtimes/linux-arm64/native/...
runtimes/linux-musl-arm64/native/...
runtimes/osx-arm64/native/...
runtimes/win-x64/native/...
runtimes/win-x86/native/...
```

This is the layout the dotnet SDK / NuGet RID-resolution understands natively — `dotnet publish` for a given RID picks the right `.so` automatically without any code in our targets file.

---

## (2) Recommended approach for the .NET agent

### Option compare

| | (A) Single multi-binary package | (B) Per-libc packages | (C) Hybrid (single .deb/.rpm/tar.gz, separate NuGet) |
|---|---|---|---|
| `.deb` / `.rpm` count | 2 (one per arch) — same as today | 4 (× libc × arch) | 2 (× arch) |
| `.tar.gz` count | 2 — same as today | 4 | 2 |
| Internal Profiler NuGet | 1 with all RIDs | 2 packages | 1 with all RIDs |
| Customer install matrix | unchanged | doubled | unchanged |
| Selection logic | `setenv.sh` libc probe | install-time package choice | `setenv.sh` probe + RID-resolution for NuGet |
| Disk size impact | one extra .so per arch package (~15 MB measured); package-relative percentage not measured | none per package | minimal |
| Postinst complexity | unchanged | per-libc divergence | unchanged |
| Aligns with OTel | Yes (tracer-home model) | Partially | Yes |
| Aligns with .NET RID convention | Yes | No | Yes |
| Customer flexibility (move app between glibc & musl images without re-installing agent) | Yes | No | Yes |

**Recommendation: (A) Single multi-binary package.** Reasons:

1. Adding a musl variant per arch adds one `.so` per package. Measured size of today's shipped binaries: **x64 = 15.3 MB, arm64 = 14.4 MB** (verified via `ls -l` on artifacts from CI runs 25933232238 / 25934126059, 2026-05-15). Package-relative bloat percentage not measured but is bounded by absolute size; the managed DLLs dominate the package, so the relative impact is small.
2. Single source of truth for the agent's `newrelic.config`, instrumentation XML, managed DLLs — these are libc-independent. Splitting into two packages duplicates that surface.
3. Cuts the deploy / signing / promotion / yum-repo / apt-repo matrix in half versus (B). The repo's deploy pipeline is sensitive to matrix size — see `build/Linux/build/`'s single-package assumption today.
4. Customer benefit: an image base swap from `mcr.microsoft.com/dotnet/aspnet:10.0` to `*-alpine` does not require swapping the agent package. They just re-source `setenv.sh`.
5. Aligns the home-directory layout with the canonical .NET RID convention, which makes the eventual NuGet / runtime-pack switch (item 4 below) mechanical.

### Resulting agent home directory layout

Today (flat):

```
newrelichome_x64_coreclr_linux/
├── libNewRelicProfiler.so
├── NewRelic.Agent.Core.dll
├── newrelic.config
├── extensions/
│   ├── ...
└── ...
```

Proposed (per-RID native subdir, managed code stays at root):

```
newrelichome_x64_coreclr_linux/
├── linux-x64/
│   └── libNewRelicProfiler.so       # glibc, built on modernized image (P3)
├── linux-musl-x64/
│   └── libNewRelicProfiler.so       # musl, built on alpine.dockerfile
├── NewRelic.Agent.Core.dll          # libc-independent
├── newrelic.config
├── extensions/
│   ├── ...
├── setenv.sh                         # updated to do libc probe
└── ...
```

Same shape on the arm64 home directory. The arm64-glibc and arm64-musl `.so` files live in `linux-arm64/` and `linux-musl-arm64/` respectively.

### Selection mechanism: updated `setenv.sh`

Replace the current `build/Linux/build/common/setenv.sh` body:

```sh
export CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so
```

with a libc probe (mirrors OTel's `instrument.sh`):

```sh
ARCH=$(uname -m)
case "$ARCH" in
  x86_64)        RID_ARCH="x64"   ;;
  aarch64|arm64) RID_ARCH="arm64" ;;
  *) echo "Unsupported architecture: $ARCH" >&2; return 1 ;;
esac

if ldd /bin/ls 2>/dev/null | grep -q musl; then
  RID="linux-musl-${RID_ARCH}"
else
  RID="linux-${RID_ARCH}"
fi

PROFILER="${NRHOME}/${RID}/libNewRelicProfiler.so"
if [ ! -f "$PROFILER" ]; then
  echo "Profiler not found at $PROFILER" >&2
  return 1
fi

export CORECLR_PROFILER_PATH="$PROFILER"
```

Backwards-compat: **the vast majority of `.deb`/`.rpm`/tarball customers hardcode `CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so`** in their environment (Dockerfile `ENV`, systemd unit, k8s manifest) rather than sourcing `setenv.sh`. This is the dominant install pattern — confirmed by team experience and reinforced by the New Relic-published Dockerfile examples and install docs (see "What customers actually do" below). Removing the flat path without a compat shim would break the majority of Linux installs.

**The libc-aware compat symlink is therefore load-bearing, not optional.** Postinst (`.deb`/`.rpm`) probes the install host's libc and creates `$NRHOME/libNewRelicProfiler.so` → `linux-{musl-,}{x64,arm64}/libNewRelicProfiler.so` accordingly; `setenv.sh` refreshes the symlink on each source to handle tarball-into-different-libc-container cases. Full detail in the Customer impact assessment section below.

A static, always-glibc symlink would work today (Alpine still loads the glibc binary via lazy-binding luck) but would silently break at Phase 3 — when the glibc binary stops loading on Alpine, the symlink would point at the wrong binary and hardcoded-path customers would break. The libc-aware version is what keeps the change non-breaking through all three phases.

---

## (3) Packaging-pipeline impact

Current packaging touchpoints that consume `libNewRelicProfiler.so` from a flat home dir:

| File | What it does | Change |
|---|---|---|
| `build/ArtifactBuilder/CoreAgentComponents.cs:155-159` | Sets `LinuxProfiler = "{home}\libNewRelicProfiler.so"` | Add a second member, `LinuxMuslProfiler`. Update home-root resolution. |
| `build/ArtifactBuilder/Artifacts/LinuxPackage.cs:172` | Validates the .so is in the package | Validate both .so files at their new RID-prefixed paths. |
| `build/ArtifactBuilder/Artifacts/NugetAgent.cs:56-57, 170, 183-185` | Copies the .so into NuGet contentFiles | Switch to `runtimes/<RID>/native/` layout, or extend the existing nested-folder approach. |
| `build/Linux/build/deb/build.sh:36` | `cp -R ${AGENT_HOMEDIR}/* ${INSTALL_LOCATION}` | No-op as long as the home dir is the new layout — the recursive copy already preserves subdirs. |
| `build/Linux/build/rpm/newrelic-dotnet-agent.spec:44` | `%{_install}/*` — packs everything from the root | No-op for same reason. |
| `build/Linux/build/common/setenv.sh` | Sets `CORECLR_PROFILER_PATH` | Rewrite per the section above. |
| `build/Linux/build/common/run.sh` | Customer-facing launcher | Likely needs a similar libc probe; audit when implementing. |
| `build/Packaging/NugetProfiler/NewRelic.Profiler.nuspec:19-20` | Maps `linux-arm64-release/**` → `content/profiler/linux_arm64`, `linux-x64-release/**` → `content/profiler/linux_x64` | Add the two musl variants. Or, switch to RID-canonical layout. |
| `build/Packaging/AzureSiteExtension/Content/install.ps1` | Azure site extension installer | Out of scope (Windows-only). |

The `.deb` and `.rpm` build scripts do `cp -R ${AGENT_HOMEDIR}/*` of the prebuilt home tree, so once `build/build_home.ps1` produces the new layout, the .deb / .rpm pick up the new shape with no change to the packaging scripts themselves.

The NuGet-consumer Profiler package (`NewRelic.Agent.Internal.Profiler`) needs both new variants added to the nuspec. The natural endpoint is RID-canonical:

```xml
<file src="linux-x64-release/**"        target="runtimes/linux-x64/native" />
<file src="linux-arm64-release/**"      target="runtimes/linux-arm64/native" />
<file src="linux-musl-x64-release/**"   target="runtimes/linux-musl-x64/native" />
<file src="linux-musl-arm64-release/**" target="runtimes/linux-musl-arm64/native" />
```

Only consumers that re-pack the .so at a custom path would need a corresponding .targets update. The current `NewRelic.Agent.Internal.Profiler.targets` is trivial (Content Include from `content/profiler/`); audit the one consumer (`Home/Home.csproj`) to determine whether it relies on the path layout or just consumes the file generically.

---

## (4) Phased execution plan with binary-shape parity gates

Each phase is an independently mergeable PR. Each one preserves a working build at every state.

### Phase 0 — Prerequisites

- **P2.4 (#3593, merged 2026-05-16 as `d8b3ce811`):** arm64 CI consolidated onto `docker compose run build_arm64`. Required because the dual-build adds two more compose services and we need a uniform model. ✅ Done.
- **P2.5 (follow-up, merged):** arm64 profiler build switched to native `ubuntu-24.04-arm` runner. Improves arm64 build performance and removes the QEMU dependency. ✅ Done.
- The previously proposed prereq of pre-splitting `Dockerfile` into per-libc files is **rolled into Phase 1** — Phase 1 adds `MuslDockerfile` directly (alongside the existing `Dockerfile` and `Arm64Dockerfile`) and adds the new compose services in the same PR. No standalone prerequisite PR for this.

**Gate:** all Phase 0 prerequisites are merged on `main`. Today's glibc binary is unchanged from before P2.4 (verified via byte-identity check on the linux-x64 and linux-arm64 artifacts produced after P2.4 / P2.5 vs before).

### Phase 1 — Add musl build target (no packaging changes)

**Scope:**
1. New `src/Agent/NewRelic/Profiler/linux/MuslDockerfile` based on `alpine:3.23`
   (plain Alpine is sufficient; dotnet is not needed for the C++ build). Toolchain:
   `clang`, `cmake`, `make`, `musl-dev`, `g++` (for libstdc++ headers / `-static-libstdc++`),
   `linux-headers`, `bash`, `dos2unix`. **Do not install `libc++-dev` / `libc++-static`** —
   the spike confirmed Alpine 3.23's `libc++-static` is not PIC-compiled and cannot be
   statically linked into a shared object; the musl path uses libstdc++ instead.
   Pin SHA before merging Phase 1.
2. New compose services in `src/Agent/NewRelic/Profiler/docker-compose.yml`:
   `build_musl_x64` (platform `linux/amd64`) and `build_musl_arm64` (platform
   `linux/arm64/v8`). Both bind-mount `.:/profiler`, command identical to
   today's `build` service.
3. `CMakeLists.txt`: keep glibc compile path byte-identical; add an `NR_MUSL_BUILD`-env-driven branch for the musl build. **Spike2-confirmed path:** switch the musl branch to **libstdc++** (not libc++), mirroring OTel's `alpine.dockerfile` (`clang` + `alpine-sdk` + `-static-libstdc++ -static-libgcc`). Use `-std=c++14` on the musl path (libstdc++ provides `std::make_unique` only at C++14 or later; the glibc path stays at `-std=c++11`). All six required source changes are listed below; spike2 confirmed this combination compiles cleanly, meets all four binary properties, and passes the smoke test.
   - **3a.** Delete the `#if defined(__llvm__)` block at `Common/xplat.h:32–59` (the `std::make_unique` shim). It is dead weight on any C++14+ toolchain and conflicts with libstdc++'s own `std::make_unique` when clang is the compiler (because `__llvm__` fires regardless of `-stdlib` choice).
   - **3b.** Fix or workaround `atl.h:367` in the vendored coreclr headers. The clean fix is patching `this->pElements` → `pBeginningElement` in the `CallConstructors` helper (real bug in vendored code, never executed). The spike2 workaround (`-fdelayed-template-parsing`) is confirmed functional and is acceptable for Phase 1 if patching is deferred, with a follow-up issue to clean it up.
   - **3c.** Add `-Wl,-z,lazy` explicitly to the musl linker flags. Clang/lld on Alpine defaults to `-z now` (`BIND_NOW`), which resolves all symbols at dlopen time — including CLR-provided symbols like `RaiseException` that only exist after the CLR loads. **Spike2 confirmed** `-Wl,-z,lazy` works: `readelf -d` on the spike2 binary shows no `BIND_NOW` flag.
   - **3d.** Add `#include <cstdint>` to `Sicily/ast/GenericParamType.h` and `Sicily/ast/TypeList.h`. libstdc++'s `<memory>` does not transitively include `<cstdint>` (libc++'s did). `uint32_t` and `uint16_t` are used directly in those headers.
   - **3e.** Add `#include <condition_variable>` to `ThreadProfiler/ThreadProfiler.h`. libstdc++ requires an explicit include; it is not pulled transitively from `<mutex>` or `<atomic>`.
   - **3f.** In `Logging/Logger.h`: (1) wrap all stdlib includes in `#pragma push_macro("__valid") / #undef __valid / ... / #pragma pop_macro("__valid")` — vendored `sal.h:2444` defines `#define __valid` empty, which breaks GCC 15 libstdc++'s `bits/parse_numbers.h` (uses `__valid` as a template type alias) when `sal.h` is processed first in the TU; (2) replace the `std::copy` + `ostream_iterator<wchar_t, wchar_t>` loop in `operator<<(wofstream, xstring_t)` with an explicit `for (auto c : str) { _Ostr.put(static_cast<wchar_t>(c)); }` — libstdc++ is strict about `char16_t`→`wchar_t` type compatibility where libc++ was lenient.
   - Confirm the resulting .so: `DT_NEEDED` is **only** `libc.musl-{x86_64|aarch64}.so.1` + `libgcc_s.so.1`, **no** `libstdc++.so.6` / `libc++.so.1`. Verify with `readelf -d`. (Spike2 result: `DT_NEEDED` is `libc.musl-x86_64.so.1` only; `libgcc_s.so.1` also absent because `-static-libgcc` succeeded.)
4. New CI jobs: `build-linux-musl-x64-profiler`, `build-linux-musl-arm64-profiler` in `build_profiler.yml`. Use `docker/setup-qemu-action` for arm64 (mirrors P2.4). Upload as `profiler-musl-amd64` / `profiler-musl-arm64`.
5. Do **not** integrate the musl artifacts into the home dir or any package yet. Phase 1 just proves we can build them.

**Verification gates (Phase 1):**

- **Glibc binary unchanged:** today's `linux-x64-release/libNewRelicProfiler.so` and `linux-arm64-release/libNewRelicProfiler.so` artifacts are byte-identical (or, if codegen drift is unavoidable, identical on the four hard-constraint properties).
- **Musl binary inspection:** for each new artifact, capture and document:
  - `readelf -d` → `DT_NEEDED` has `libc.musl-{arch}.so.1` (or whatever musl SONAME the chosen Alpine version exposes), `libgcc_s.so.1`. **No `libstdc++.so.6`**, **no `libc++.so.1`**.
  - `nm -D --defined-only` → exported-symbol set is **identical** to the glibc binary. Any drift means the build is exporting different code.
  - `ldd` reports zero unresolved symbols on Alpine 3.18, 3.20, 3.23.
- **Native unit test parity:** the existing native test projects (`CommonTest`, `ConfigurationTest`, etc.) only build on Windows; no new native test runs on Linux today. Phase 1 does not change that. Smoke test: load the musl `.so` under `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, attach to a trivial dotnet console app, confirm `Profiler initialized` log line emits and instrumentation XML files parse (mirror the test from `PROFILER_MODERNIZATION_PLAN.md`). **Spike2 result:** `Profiler initialized` emitted; `Logger initialized`, `ICorProfilerInfo10 available`, 372 instrumentation XML points parsed; profiler shut down cleanly. Exported symbol count: 6989 (vs 4551 glibc x64 baseline) — the delta is expected; libstdc++ internals are statically linked into the musl `.so` rather than in a dynamic dependency, so they appear in its exported symbol table. Critical COM entry points (`DllGetClassObject`, `DllCanUnloadNow`) are present in both binaries.
- **Full IntegrationTests + UnboundedIntegrationTests + ContainerIntegrationTests** — unchanged from today (no packaging change). AlpineX64 fixture continues to work against the **glibc** binary (today's mechanism).

### Phase 2 — Restructure home directory + integrate musl into packages

**Scope:**
1. `build/build_home.ps1`: emit per-RID subdirs into the home dir:
   - `newrelichome_x64_coreclr_linux/linux-x64/libNewRelicProfiler.so`
   - `newrelichome_x64_coreclr_linux/linux-musl-x64/libNewRelicProfiler.so`
   - and arm64 equivalents in `newrelichome_arm64_coreclr_linux/`.
2. `build/ArtifactBuilder/CoreAgentComponents.cs`: add `LinuxMuslProfiler` member; update `Artifacts/LinuxPackage.cs` and `Artifacts/NugetAgent.cs` validators.
3. `build/Linux/build/common/setenv.sh`: rewrite per Section (2) above.
4. `build/Linux/build/common/run.sh`: audit and update if it currently references the flat path.
5. `build/Packaging/NugetProfiler/NewRelic.Profiler.nuspec`: add `linux-musl-x64-release/**` and `linux-musl-arm64-release/**`. Decide between extending the nested-folder layout or switching to canonical `runtimes/<RID>/native/` (recommend the latter — bigger one-time change but lines up with .NET conventions).
6. CI deploy step in `build_profiler.yml`: download the two new musl artifacts, stage into `_workingDir` so `nuget pack` picks them up.
7. `src/Agent/NewRelic/Profiler/linux/README.md`: rewrite the "Alpine compatibility" section. The four-property mechanism is no longer how Alpine works — Alpine compat now comes from the dedicated musl binary. Mention the legacy glibc binary's lazy-binding luck only as historical context.
8. Docs: tarball install instructions need `setenv.sh` re-source emphasised.

**Verification gates (Phase 2):**

- **Glibc binary** still has the four properties from the constraint section (DT_NEEDED, GLIBC ceiling, no libc++/libstdc++, lazy binding). The glibc image hasn't changed in this phase, so byte-identity should hold relative to Phase 1.
- **`.deb` / `.rpm` install on glibc** (Ubuntu 20.04, RHEL 8/9, Amazon Linux 2):
  - `setenv.sh` resolves `CORECLR_PROFILER_PATH` to the `linux-{x64,arm64}/libNewRelicProfiler.so` path.
  - Profiler attaches and initializes.
  - Full ContainerIntegrationTests `UbuntuX64`, `UbuntuArm64`, `CentosX64`, `CentosArm64`, `AmazonX64`, `FedoraX64` pass.
- **`.deb` / `.tar.gz` install on Alpine** (Alpine 3.18, 3.20, 3.23 — extend ContainerIntegrationTests coverage if not already there):
  - `setenv.sh` selects the `linux-musl-{x64,arm64}/libNewRelicProfiler.so` path.
  - Profiler attaches and initializes — **using the new musl-native binary**, not the glibc-loaded-via-luck binary.
  - `AlpineX64` fixture passes. Add `AlpineArm64` fixture if missing.
- **NuGet consumer test:** `dotnet publish` of an app that references `NewRelic.Agent.Internal.Profiler` for `linux-x64`, `linux-musl-x64`, `linux-arm64`, `linux-musl-arm64` RIDs each yield a publish output containing only the matching `.so`. Validate that `Home/Home.csproj` and any other internal consumer still finds the file at the expected post-RID-resolution path.
- **Backwards compat:** customer with `CORECLR_PROFILER_PATH` already exported (any value) — `setenv.sh` does not overwrite (the existing `[ -z ]` guard pattern stays). Document the behavior change for customers who hardcoded the flat-layout path; recommend re-source.
- **Full IntegrationTests + UnboundedIntegrationTests + ContainerIntegrationTests pass on every fixture, including AlpineX64 / AlpineArm64.**

### Phase 3 — Modernize the glibc build base (track .NET's portable build base)

**Rationale (added 2026-05-18 per team review).** The current `Dockerfile` choice (Ubuntu, ex-Debian) is not arbitrary — it tracks **the .NET team's own portable Linux build base for the minimum supported .NET runtime version**. Microsoft publishes this baseline explicitly in the per-version `dotnet/core` `release-notes/<ver>/supported-os.md#linux-compatibility` table. The migration from Debian to Ubuntu happened when .NET 8 became the agent's minimum supported runtime, because .NET 8's portable glibc build is sourced from Ubuntu 16.04 (glibc 2.23). Phase 3 must follow the same rule: **the glibc build base tracks the .NET team's portable build base for whichever .NET version is the agent's minimum supported runtime at the time Phase 3 ships.**

The current Microsoft portable-build baselines (verified 2026-05-18 from `dotnet/core` `main`):

| .NET version | Lifecycle | glibc baseline | Source distro | musl baseline | Source distro |
|---|---|---|---|---|---|
| .NET 8 (LTS) | Active | 2.23 | Ubuntu 16.04 | 1.2.2 | Alpine 3.13 |
| .NET 9 (STS) | Active | 2.23 | Ubuntu 16.04 | 1.2.2 | Alpine 3.13 |
| **.NET 10 (LTS)** | **Active** | **2.27** | **Ubuntu 18.04** | **1.2.3** | **Alpine 3.17** |

**Target depends on the agent's minimum supported runtime when Phase 3 ships.** If .NET 10 is the floor by then, the natural target is **Ubuntu 18.04 base** (or an equivalent glibc-2.27 base such as a manylinux variant — but the citation source is dotnet's own table, not PyPA). If the agent still supports .NET 8 as the floor, Phase 3 doesn't ship — there's nothing to modernize that wouldn't break .NET 8 alignment.

The earlier draft of this doc proposed `manylinux_2_28` (AlmaLinux 8, glibc 2.28) as a generic "modernize to a current AlmaLinux 8 base" choice. That target is *close* to .NET 10's baseline (glibc 2.28 vs 2.27) but does not officially track dotnet's choice. The reframing below uses the dotnet baseline; the manylinux variants remain a viable implementation if pinning a specific dotnet-team-blessed image is preferred over PyPA's. The substantive distro-coverage delta between glibc 2.27 and 2.28 is small — Ubuntu 18.04 covers everything 2.28 does plus Ubuntu 18.04 itself (which Microsoft includes for .NET 10 by virtue of basing their portable build on it).

**Scope:** with Alpine compat now provided by the dedicated musl binary, the glibc build can adopt a current toolchain. **Recommended target (tracking .NET 10 LTS): Ubuntu 18.04 base, glibc 2.27.** Pin a digest at adoption time. Verify the toolchain (GCC / clang / cmake) versions shipped in the chosen base image at the time of selection — `apt install` recent versions if necessary, mirroring how OTel's `ubuntu1604.dockerfile` installs `clang-5.0` and `cmake 3.20.5` from Kitware. Document refresh policy: re-pin whenever the agent's minimum supported runtime advances and the dotnet team's baseline changes.

**Why 2.27 (Ubuntu 18.04, .NET 10 baseline) — comparison with alternatives:**

| Floor | Build base option | Distros covered | Distros dropped vs today | Tracks dotnet baseline? |
|---|---|---|---|---|
| 2.17 | manylinux2014 (CentOS 7, EOL) | Everything | None | No (lower than .NET 10) |
| **2.27** | **Ubuntu 18.04 (.NET 10 LTS portable build base)** | **Ubuntu 18.04+, RHEL 8/9, Debian 10+, Amazon Linux 2023, SUSE 15+** | **Amazon Linux 2 (2.26), RHEL 7 (2.17)** | **Yes (.NET 10 LTS)** |
| 2.28 | manylinux_2_28 (AlmaLinux 8, PyPA) | RHEL 8/9, Ubuntu 20.04+, Debian 11+, Amazon Linux 2023, SUSE 15 SP4+ | adds Ubuntu 18.04 to the dropped list | No (one minor version above .NET 10) |
| 2.34 | manylinux_2_34 (AlmaLinux 9) | RHEL 9, Ubuntu 22.04+, Debian 12, AL2023 | adds Ubuntu 20.04, Debian 11, SUSE 15 SP4 to the dropped list | No (.NET 11+ territory) |

`manylinux2014` keeps every distro but its CentOS 7 base is itself EOL since 2024-06-30 — it trades EOL Ubuntu 14.04 for EOL CentOS 7 and fails the modernization goal. `manylinux_2_34` drops too much of the still-current Ubuntu / Debian LTS surface. `manylinux_2_28` (the prior draft's pick) is one minor glibc version stricter than dotnet's actual .NET 10 portable baseline, and would refuse to load on the very Ubuntu 18.04 systems Microsoft uses to *build* dotnet 10. **Ubuntu 18.04 (glibc 2.27) is the right target when .NET 10 is the agent's minimum supported runtime — it tracks dotnet's published baseline exactly.** The dropped set vs today:

- **Amazon Linux 2** (glibc 2.26): AWS moved AL2 to maintenance-only in 2024; **end-of-life June 2026**. Migration target is AL2023, which is glibc 2.34.
- **RHEL 7** (glibc 2.17): EOL'd 2024-06-30 upstream; only available via paid extended-support.

Both are end-of-life Linux distributions. Customers on them are on borrowed time independent of the .NET agent. The agent's role in keeping them on a glibc 2.17 baseline is no longer doing them a favor — and dotnet 10 itself doesn't support them as runtime targets. **Ubuntu 18.04 stays in (it is the floor, not above it)** — this is a strict improvement over the prior draft's manylinux_2_28 proposal, which would have dropped it.

**Companion cleanup unlocked by the modernization:**

- Drop `-stdlib=libc++` from `CMakeLists.txt` on the **glibc path** (the musl path will already have switched to libstdc++ in Phase 1). On a modern toolchain, use libstdc++ throughout with `-static-libstdc++ -static-libgcc`. Simpler, fewer moving parts, no observable binary-shape change. The musl path already validates this approach.
- If not already done in Phase 1: patch `externals/coreclr-headers/src/pal/inc/rt/atl.h:367` — change `this->pElements` to `pBeginningElement` in the never-called `CallConstructors` helper. This is a real bug in the vendored header that modern clang (≥ 14) catches in two-phase name lookup. Without the patch, `-fdelayed-template-parsing` is required on any modern-clang path (both glibc and musl).
- If not already done in Phase 1: remove the `#if defined(__llvm__)` block at `Common/xplat.h:32–59`. The shim is dead weight on any C++14+ libstdc++ and fires for any clang build (not just libc++ builds) due to `__llvm__` being unconditionally defined by clang.
- Drop the `-fms-extensions -fdeclspec` clang-3.9-era workarounds if no longer needed for the source.
- Address the `strtoll_l` / `strtoull_l` latent fragility (it's a libc++/libc interaction that goes away under modern libstdc++).
- Cmake from system repos; no Kitware S3 substitute, no aarch64 cmake-from-GitHub-releases workaround.
- Modern keyring-based apt config (already done in P3.1, but the new base may not even use apt).

**Scope of changes:**

1. New `Dockerfile` (replaces today's Ubuntu 16.04 file): `FROM ubuntu:18.04@sha256:<digest>`, with `apt install` for clang/g++/cmake (mirroring how today's Dockerfile installs newer toolchain on top of an older base — and how OTel's `ubuntu1604.dockerfile` installs `clang-5.0` and `cmake 3.20.5` on top of Ubuntu 16.04). Pin a digest at adoption time. Document refresh policy: re-pin whenever the agent's minimum supported runtime advances and the dotnet team's portable baseline changes.
2. New `Arm64Dockerfile`: `FROM ubuntu:18.04@sha256:<digest>`. Drops the clang-3.9 + S3 cmake + LLVM apt-key paths entirely (Ubuntu 18.04 ships these from `apt`).
3. `CMakeLists.txt`: drop `-stdlib=libc++` from the glibc path, raise `cmake_minimum_required` to match the new base.
4. `linux/README.md`: rewrite. The four-property invariant becomes a two-property invariant on the **musl** binary only (no libstdc++ DT_NEEDED, narrow DT_NEEDED set). The glibc binary stops being a special case; its compatibility floor is whatever the chosen Ubuntu base provides (matching dotnet's portable baseline).

**Verification gates (Phase 3):**

- **Distro coverage matrix (build runs successfully + agent attaches successfully)** on every RID we claim to support:
  - Ubuntu 20.04 / 22.04 / 24.04, x64 + arm64
  - RHEL 8 / 9, x64 + arm64
  - Debian 11 / 12, x64 + arm64
  - Amazon Linux 2023, x64 + arm64
  - SUSE 15 SP5+, x64
  - All ContainerIntegrationTests fixtures pass with the new glibc binary.
- **Glibc binary's `readelf -V` reports max GLIBC_ symbol ≤ the dotnet portable baseline for the agent's minimum supported runtime** (≤ 2.27 if .NET 10 is the floor; check `dotnet/core` `release-notes/<min-supported>/supported-os.md#libc` at adoption time). If higher, something in the source is using a post-baseline glibc API and we need to either guard it or back it out.
- **No `libstdc++.so.6` or `libc++.so.1` in `DT_NEEDED`** (universal invariant).
- **Musl binary unchanged from Phase 2** — Alpine fixture passes against the same musl artifact, byte-identical SHA-256.
- **AlpineX64 / AlpineArm64 fixtures pass** for the *correct* reason: `setenv.sh` resolved `CORECLR_PROFILER_PATH` to the musl binary, not the glibc one. Add an explicit assertion in the fixture: log the resolved profiler path at startup and fail the test if it points at `linux-x64/` while running on Alpine.
- **Negative test (new):** explicitly point the glibc binary at an Alpine container and confirm `dlopen` fails with a useful error. Today, this would silently load via lazy-binding luck and partially work; after Phase 3, it must fail loudly. Guards against future regressions onto the old mechanism.

**Customer impact (the genuine breaking-change moment):**

- Customers on Amazon Linux 2 or RHEL 7 see a load-time failure with a clear glibc-version error from the dynamic linker. Same set of distros that .NET 10 itself doesn't support — the failure is loud, unambiguous, and consistent with dotnet's own behavior. Ubuntu 18.04 customers are NOT affected (it's the build floor).
- This is the only phase with an unavoidable breaking change. **The communication plan must front-load it:** at least one minor-version-cycle of advance notice, support-team runbook updated, release notes call it out at the top.
- Recommendation: ship Phase 2 + the AL2/RHEL7 deprecation notice in release N, ship Phase 3 (with the glibc bump) in release N+1 or later. This gives customers concrete time to migrate. Pre-announce alongside (or after) the agent's drop of .NET 8 minimum-supported-runtime support, since Phase 3's rationale is tied directly to that change.

### Phase 4 (optional) — Canonical NuGet runtime-pack layout

Move the internal Profiler NuGet to `runtimes/<RID>/native/` if not already done in Phase 2. Update `NewRelic.Agent.Internal.Profiler.targets` if downstream consumers need explicit copy logic (most won't — RID resolution is automatic for `dotnet publish`).

**Verification:** consumer `dotnet publish` for each RID picks the right `.so`; legacy `Home/Home.csproj` still resolves.

---

## Testing matrix expansion (Phases 1-3)

The dual-build doubles the binary surface (one .so → two .so per arch), and Phase 3's glibc-baseline lift means the modernized glibc binary diverges materially from today's binary. The current single-fixture-per-distro test surface is no longer sufficient.

**Required new fixtures and assertions:**

1. **AlpineArm64ContainerTestFixture.** Today's `ContainerTestFixtures.cs` has `AlpineX64` but no `AlpineArm64`. Add it; required for Phase 1 (musl arm64 build) and Phase 2 (RID-aware selection across both Alpine arches).

2. **Distro-version coverage on glibc.** Today's fixtures pin one tag per distro family (`UbuntuX64` is `noble` = 24.04). Phase 3 changes the GLIBC ceiling, so we need explicit coverage of the glibc floor (Ubuntu 18.04 if the floor is glibc 2.27 / .NET 10, RHEL 8, Debian 10/11) and the modern end (Ubuntu 24.04, RHEL 9, Debian 12, AL2023). Recommend adding versioned fixtures: `UbuntuX64_1804`, `UbuntuX64_2004`, `UbuntuX64_2204`, `UbuntuX64_2404` and the same shape for `Rhel8X64`, `Rhel9X64`, `Debian10X64`, `Debian11X64`, `Debian12X64`, `AmazonLinux2023X64`, plus arm64 versions of the same. ~10–12 new fixtures.

3. **Per-fixture assertion that the *correct* binary loaded.** The single biggest new bug class introduced by the dual-build is "fixture passes but loaded the wrong binary" — e.g., AlpineX64 silently loading the glibc binary via lazy-binding luck instead of the musl-native binary. Add an assertion at the end of each fixture: read the resolved `CORECLR_PROFILER_PATH` from agent log output and assert it matches the expected RID for that fixture.

4. **Phase-3 negative test.** Explicitly point the glibc binary at an Alpine container and assert `dlopen` fails with a clear glibc-version error. Today this would silently load via lazy-binding luck. After Phase 3 it must fail loudly. The negative test guards against future regressions onto the old mechanism.

5. **NuGet RID-resolution test.** A small ContainerIntegrationTests fixture that consumes the `NewRelic.Agent` NuGet package, runs `dotnet publish -r linux-musl-x64`, and asserts the published output contains only the musl x64 .so (or, with the compat-copy mitigation, contains both musl x64 and the flat-path symlink/copy). Same for `linux-x64`, `linux-arm64`, `linux-musl-arm64`. Validates the RID-canonical layout works end-to-end.

6. **Compat-symlink lifecycle tests.** Three scenarios: `.deb` install on glibc Ubuntu (postinst symlink correct), `.deb` install on Alpine (postinst symlink correct), tarball-extracted-to-Alpine + first source of `setenv.sh` (symlink created and correct). Each asserts the legacy flat path resolves to the right binary.

**Total new fixture count: roughly +10 container fixtures and +4 negative/integration tests vs today's ~7.** This is a real cost. The mitigation is that all of these can be authored once and run on the existing `ContainerIntegrationTests` infrastructure; they don't require new test infrastructure.

**Where the test cost is unavoidable:** the verification gate for Phase 3 is "the modernized glibc binary works on every supported glibc distro and fails loudly on every dropped distro." That's a matrix; you can't verify it with a single fixture. Skipping the matrix means shipping Phase 3 without empirical evidence that we haven't broken a customer.

---

## Open questions (to resolve before Phase 1 PR)

1. **Alpine base image choice for `MuslDockerfile`.** ~~Unresolved.~~ **Spike answer:** `alpine:3.23` with apk-installed `clang cmake make musl-dev g++ linux-headers bash dos2unix` builds cleanly. The `mcr.microsoft.com/dotnet/sdk:10.0-alpine3.23` image is not needed — dotnet is not required for the C++ build and adds unnecessary weight. Pin a digest before merging.
2. **`-stdlib=libc++` on the musl path: confirmed not viable as-is; switch to libstdc++.** The spike proved Alpine 3.23's `libc++-static` is not built with `-fPIC` and cannot be statically linked into a shared object. There is no workaround short of building libc++ from source inside the Dockerfile (expensive; not what OTel does). **Recommended resolution:** the musl build path switches to libstdc++ (`-static-libstdc++ -static-libgcc`), exactly matching OTel's `alpine.dockerfile`. This requires deleting the `__llvm__`-gated shim in `xplat.h:32–59` (see Phase 1 item 3a above). For the **glibc build**, `-stdlib=libc++` remains unchanged until Phase 3 (where it too can be dropped in favour of libstdc++). Empirically verify both binaries: zero libc++/libstdc++ entries in `DT_NEEDED`, correct exported-symbol set against the glibc baseline.
3. **Symbol-export parity between glibc and musl builds.** Both binaries need to export the same set of `DllGetClassObject` / etc. so the CLR's lookup works identically. CMakeLists.txt' `list(APPEND SOURCES ...)` should be identical for both; verify `nm -D --defined-only` matches.
4. **`build_profiler.sh` accepting an "OS_TYPE" / "RID" env var.** Mirror OTel — single shell script, env-driven output directory, called four times by CI (x64-glibc, x64-musl, arm64-glibc, arm64-musl).
5. **Decide: `runtimes/<RID>/native/` vs. nested `linux-x64/` directories at the home-dir root.** They can be the same string. Recommend literally using `linux-x64`, `linux-musl-x64`, etc. so the `runtimes/` switch in NuGet (Phase 4) is a path prefix swap, not a rename.
6. **Customer-facing breaking change communication.** Phase 2 changes the on-disk layout from flat to per-RID. The compat symlinks (postinst, tarball-baked, NuGet compat-copy) preserve the flat path for the dominant hardcoded-`CORECLR_PROFILER_PATH` install pattern. Release notes still need to cover the edge cases — primarily the tarball-into-Alpine-without-setenv-source scenario described in the Customer impact section. Identify the docs surface for the call-out (release notes + linux-attach docs); also update `newrelic/newrelic-dotnet-examples` and `newrelic/docs-website` Dockerfile examples to reference the per-RID path as the forward-looking pattern (with the flat path as the still-supported compat option).
7. **`run.sh`** — audit during Phase 2. If it sets `CORECLR_PROFILER_PATH` directly, mirror the libc probe.
8. **`atl.h:367` vendored bug.** `Common/xplat.h` aside, the spike exposed a second source-level blocker for any modern-clang build: `this->pElements` in the never-called `CallConstructors` helper at `externals/coreclr-headers/src/pal/inc/rt/atl.h:367` references a non-existent member. Clang ≥ 14 catches it in two-phase name lookup. The spike workaround is `-fdelayed-template-parsing` (restores the old MSVC/clang-3.9 behaviour of skipping unreached template bodies — no effect on emitted code). The clean fix is patching the vendored file one line. **Spike2 resolution:** `-fdelayed-template-parsing` as an `NR_MUSL_BUILD`-gated flag is confirmed functional for Phase 1. The vendored-header source patch can be deferred to a follow-up. **Phase 3 note:** the modernized glibc build (Ubuntu 18.04-based or whichever base tracks dotnet's portable baseline at adoption time) will hit the same issue if it uses a modern clang; the workaround or source patch should be evaluated at that time.
9. **`BIND_NOW` / `-Wl,-z,lazy`.** The spike1 binary had `FLAGS: BIND_NOW`; today's glibc binary uses `RTLD_LAZY` (per the modernization plan). **Spike2 resolution:** `-Wl,-z,lazy` was added to the musl linker flags in CMakeLists.txt. `readelf -d` on the spike2 binary confirms no `BIND_NOW` flag — lazy binding is correctly enabled. This question is fully resolved for Phase 1.

---

## Critical files (cross-reference)

**Build & home dir:**
- `build/build_home.ps1`
- `build/ArtifactBuilder/CoreAgentComponents.cs` (lines 152-159)
- `build/ArtifactBuilder/Artifacts/LinuxPackage.cs` (line 172)
- `build/ArtifactBuilder/Artifacts/NugetAgent.cs` (lines 56-57, 170, 183-185)

**Profiler build:**
- `src/Agent/NewRelic/Profiler/docker-compose.yml`
- `src/Agent/NewRelic/Profiler/linux/Dockerfile` (current glibc x64)
- `src/Agent/NewRelic/Profiler/linux/Arm64Dockerfile` (current glibc arm64)
- `src/Agent/NewRelic/Profiler/linux/MuslDockerfile` *(new)*
- `src/Agent/NewRelic/Profiler/CMakeLists.txt`
- `src/Agent/NewRelic/Profiler/linux/build_profiler.sh`
- `.github/workflows/build_profiler.yml`

**Packaging:**
- `build/Linux/build/deb/build.sh`
- `build/Linux/build/deb/postinst`
- `build/Linux/build/rpm/newrelic-dotnet-agent.spec`
- `build/Linux/build/common/setenv.sh` *(rewrite)*
- `build/Linux/build/common/run.sh` *(audit)*
- `build/Packaging/NugetProfiler/NewRelic.Profiler.nuspec`
- `build/Packaging/NugetProfiler/build/NewRelic.Agent.Internal.Profiler.targets`

**Docs:**
- `src/Agent/NewRelic/Profiler/linux/README.md` *(rewrite Alpine-compat section)*
- `build/Linux/linux_packaging.md`
- Customer-facing Linux attach docs *(release note)*

---

## Customer impact assessment

This is the highest-priority section of the design. The dual-build only makes sense if the customer-facing breaking-change surface is small or zero. The investigation below maps every install / attach path the agent supports and identifies exactly what changes for each.

### TL;DR

| Customer scenario | Today | After Phase 2 (naive) | After Phase 2 (with mitigation) |
|---|---|---|---|
| Windows .NET FW / .NET Core (MSI, Azure SiteExtension, NuGet) | Works | **Unchanged** | **Unchanged** |
| Linux `.deb` / `.rpm` install + sources `setenv.sh` | Works | Works | Works |
| Linux `.deb` / `.rpm` install + hardcoded `CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so` | Works (incl. Alpine via gcompat-free luck) | **Breaks** | **Works** via postinst-installed compat symlink |
| Linux tarball + sources `setenv.sh` | Works | Works | Works |
| Linux tarball + hardcoded flat-path env vars | Works | **Breaks** | **Works** via setenv.sh creating a compat symlink on first source |
| `NewRelic.Agent` NuGet (consumer-facing), customer Dockerfile copies `newrelic/libNewRelicProfiler.so` | Works on x64 glibc; uses linux-arm64 subdir for arm64; works on Alpine x64 by glibc-via-luck | **Breaks** on x64 + Alpine (path moves under linux-x64/) | **Works** if we keep `newrelic/libNewRelicProfiler.so` as a postinstall-resolved compat copy/symlink at NuGet install time |
| `NewRelic.Agent` NuGet, customer relies on `dotnet publish -r linux-musl-x64` RID resolution | Doesn't work today (no musl binary shipped) | Works (new capability) | Works |
| Linux `setenv.sh` user, glibc → musl base image swap (e.g., `aspnet:10.0` → `aspnet:10.0-alpine`) | Works (by luck — no Alpine-native binary) | Works | Works |
| Same scenario, after Phase 3 (glibc baseline tracks dotnet's portable baseline — 2.27 if .NET 10 is the floor) | n/a | Glibc binary stops loading on Alpine if customer pinned a flat-path env var | Compat symlink resolves to musl binary; Alpine still works |
| **Phase 3 — customer on Amazon Linux 2 / RHEL 7** | Works | n/a | **Fails loudly** with clear glibc-version error. Same distros dotnet 10 itself doesn't support. Customer must upgrade distro or pin pre-Phase-3 agent. Distros are EOL upstream regardless. |
| Customer reads docs.newrelic.com Linux install instructions verbatim | Works | **Breaks** until docs are republished | Works (compat symlink); docs should still be updated to point at the new RID-aware path |
| K8s auto-attach (operator + init container), glibc pod | Works | Works (compat symlink resolves to glibc binary) | Works |
| K8s auto-attach, Alpine pod, no operator annotation | Works (lazy-binding luck) | Works (still lazy-binding luck — compat symlink → glibc binary, loads by luck) | Works (same mechanism); breaks at Phase 3 |
| K8s auto-attach, Alpine pod, `dotnet-runtime: "linux-musl-x64"` annotation | n/a (annotation doesn't exist) | n/a (operator change required) | **Works on the proper musl-native binary**; survives Phase 3 |

The naive Phase 2 introduces a **major breaking change** for **the dominant Linux install pattern**: hardcoded `CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so`. Per team experience working with Linux customer installs, the vast majority of `.deb`/`.rpm`/tarball-installed agents hardcode this env var rather than sourcing `setenv.sh` — and that's exactly what our own published examples and install docs lead them to do. **The compat-symlink mitigations below are therefore load-bearing, not optional belt-and-suspenders.** They are the mechanism by which Phase 2 ships non-breaking for the majority of Linux customers; without them, Phase 2 is a major breaking release.

### What customers actually do (verified)

The customer-pattern claims here are grounded in five concrete pieces of evidence, with citations:

1. **Our own integration test Dockerfiles hardcode the flat path.** Every variant in `tests/Agent/IntegrationTests/ContainerApplications/SmokeTestApp/Dockerfile{,.amazon,.centos,.fedora}` contains:

   ```dockerfile
   ENV CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so
   ```

   The `AlpineX64ContainerTestFixture` reuses the same `SmokeTestApp/Dockerfile`. We test Alpine today by running the glibc-targeted binary against this hardcoded flat path.

2. **`run.sh` in `build/Linux/build/common/run.sh`** ships the same flat-path assumption as a one-liner customer launcher:

   ```sh
   CORECLR_PROFILER_PATH="$CORECLR_NEWRELIC_HOME/libNewRelicProfiler.so" "$@"
   ```

3. **`setenv.sh` exports the flat path** today:

   ```sh
   export CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so
   ```

4. **The `NewRelic.Agent` NuGet package** stages the x64 glibc .so at `contentFiles/any/netstandard2.0/newrelic/libNewRelicProfiler.so`, so `dotnet publish` lands it at `<publish>/newrelic/libNewRelicProfiler.so`. The arm64 .so goes to a `linux-arm64/` subdir.

5. **The official New Relic public install docs and example repos use the flat path.** Verified 2026-05-15 by reading the source `.mdx` files in `newrelic/docs-website` and the Dockerfiles in `newrelic/newrelic-dotnet-examples`:

   - **`newrelic/newrelic-dotnet-examples/docker-agent-nuget/{alpine,ubuntu,centos}/Dockerfile`** — all three published example Dockerfiles set `CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so`. The README of that repo describes them as the recommended Docker NuGet attach pattern, and the NugetAgent README in this repo explicitly links there.
   - **`newrelic/docs-website` install pages using the flat path** (English; equivalent translations exist):
     - `src/install/dotnet/installation/linuxInstall3-enable-agent.mdx` — canonical apt/yum/tarball attach page; offers `setenv.sh` and the flat-path env vars as **two equally-presented tabs**
     - `src/install/dotnet/installation/dockerLinux.mdx` — Docker on Linux install page (flat path twice)
     - `src/install/dotnet/installation/nuget2.mdx` — NuGet install page (`CORECLR_PROFILER_PATH=APP_DEPLOYMENT_DIRECTORY/newrelic/libNewRelicProfiler.so`); also contains a callout instructing arm64 customers to override the path to `linux-arm64/libNewRelicProfiler.so` — confirming arm64 NuGet customers already manage path overrides today
     - `src/install/dotnet/installation/azure-linux-container.mdx` — Azure Linux container (flat path twice)
     - `src/install/dotnet/installation/aws-elastic.mdx` — AWS Elastic Beanstalk (flat path)
     - `src/install/dotnet/installation/azure-nuget-core-install.mdx` — Azure NuGet on Linux (`/home/site/wwwroot/newrelic/libNewRelicProfiler.so`)
     - `src/content/docs/apm/agents/net-agent/other-installation/understanding-net-agent-environment-variables.mdx` — env-variable reference page
   - At least 4 additional translated copies of the same English pages (es/jp/kr/fr/pt) exist and would also need to be updated.

The flat path is **a** documented attach path on every Linux install surface today, side-by-side with the `setenv.sh` option on the canonical install page. Removing the flat path without a compatibility shim would invalidate the published examples and require all of these doc pages to be updated in lockstep — including our own AlpineX64 integration test fixture, which models the customer pattern exactly.

What the team's experience confirms:
- The vast majority of Linux `.deb`/`.rpm`/tarball customers hardcode the flat path. Sourcing `setenv.sh` is the minority case. This is corroborated by every published example and install-doc page above leading customers to the flat-path pattern. A precise customer-survey/telemetry number is not required to act on this; the design must treat hardcoded `CORECLR_PROFILER_PATH` as the dominant install pattern.

### Per-platform impact

#### Windows (.NET Framework + .NET Core, all hosts: IIS, Azure App Service, console, etc.)

**Impact: zero.** The Profiler.dll continues to ship as a single Windows binary per arch (x64, x86). Windows install paths (MSI, Azure SiteExtension, NuGet `NewRelic.Agent` for Windows targets, Chocolatey) are unchanged. No customer-visible change.

#### Linux glibc (Ubuntu, Debian, RHEL, CentOS, Amazon Linux, SUSE) — `.deb` / `.rpm` install

**Today:**
- `apt install newrelic-dotnet-agent` → files at `/usr/local/newrelic-dotnet-agent/`
- postinst writes `/etc/profile.d/newrelic-dotnet-agent-path.sh` exporting `CORECLR_NEWRELIC_HOME`
- Customer either sources `setenv.sh` (path-agnostic) or hardcodes env vars in their systemd unit / Docker / k8s manifest with the flat path.

**After Phase 2 (naive):** flat path no longer exists; profiler attach fails with `Failed to load libNewRelicProfiler.so` in Event Viewer / dotnet stderr. Sourcing `setenv.sh` continues to work because the script is updated.

**With mitigation (recommended):**

In **postinst** for `.deb` / `.rpm`, after the recursive copy completes, do a libc probe and create a symlink at the legacy path pointing to the matching variant:

```sh
if ldd /bin/ls 2>/dev/null | grep -q musl; then
    ln -sf "linux-musl-$(uname -m | sed 's/x86_64/x64/;s/aarch64/arm64/')/libNewRelicProfiler.so" \
           "$NEWRELIC_HOME/libNewRelicProfiler.so"
else
    ln -sf "linux-$(uname -m | sed 's/x86_64/x64/;s/aarch64/arm64/')/libNewRelicProfiler.so" \
           "$NEWRELIC_HOME/libNewRelicProfiler.so"
fi
```

Result: customer's hardcoded `CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so` continues to resolve to a working binary. No breakage.

**Caveat:** the symlink is correct only for the install host's libc. If a `.deb` is built on glibc Ubuntu and copied (without re-installing) into an Alpine container, the symlink points at the glibc binary. This is fine because Alpine containers normally `apk add` the package or extract a tarball — they don't `dpkg-deb -x`. But it's worth calling out.

#### Linux tarball install (`.tar.gz`)

**Today:** customer extracts the tarball into a known location, sets env vars manually or sources `setenv.sh`.

**After Phase 2 (naive):** flat path missing. Same break.

**With mitigation:** `setenv.sh` creates the compat symlink on first source (idempotent):

```sh
COMPAT_LINK="$NRHOME/libNewRelicProfiler.so"
if [ ! -e "$COMPAT_LINK" ]; then
    ln -sf "$RID/libNewRelicProfiler.so" "$COMPAT_LINK" 2>/dev/null || true
fi
```

(Falls back silently on read-only filesystems; the `setenv.sh`-exported `CORECLR_PROFILER_PATH` already points at the per-RID path so readonly is not actually a problem.)

This mitigates the case where the customer extracts the tarball and sources `setenv.sh` at least once — but `setenv.sh` is the minority install path, so we cannot rely on it to fix the symlink. **The tarball MUST ship a pre-baked default symlink at build time**, pointing to `linux-x64/libNewRelicProfiler.so` (since the tarball is built on a glibc host, that's the only sensible default for a static pre-bake). This handles the glibc-on-glibc case (the majority of tarball installs) transparently and without requiring the customer to source `setenv.sh`.

The remaining edge case is a customer who extracts the tarball **into an Alpine container** without sourcing `setenv.sh` — the pre-baked symlink would point at the glibc binary, which today loads via lazy-binding luck and after Phase 3 would fail. Two layered mitigations cover this:
1. **`setenv.sh`** detects when the symlink target's libc doesn't match the host libc and refreshes it. Customers who source `setenv.sh` once after the layered base swap get a corrected symlink automatically.
2. **Release notes** call out this specific scenario for the small remaining subgroup who extract a tarball into Alpine and never source `setenv.sh` and never re-postinst.

Importantly, this edge case is the only sub-pattern of tarball installs that requires explicit customer awareness — and it's the *minority* edge case of the *minority* tarball-without-setenv path. The mainline tarball-install scenario (extract on glibc, hardcode env vars, deploy to glibc image) stays non-breaking with the pre-baked symlink alone.

#### `NewRelic.Agent` NuGet package (consumer-facing)

**Today:** `<publish>/newrelic/libNewRelicProfiler.so` (x64 glibc), `<publish>/newrelic/linux-arm64/libNewRelicProfiler.so` (arm64). The documented NuGet attach path on `nuget2.mdx` is `CORECLR_PROFILER_PATH=APP_DEPLOYMENT_DIRECTORY/newrelic/libNewRelicProfiler.so`, with an explicit callout instructing arm64 customers to override to `APP_DEPLOYMENT_DIRECTORY/newrelic/linux-arm64/libNewRelicProfiler.so`. So arm64 NuGet customers already manage path overrides — that's an existing documented pattern, not an inferred pain point.

**After Phase 2 (naive):** flat path moves under `linux-x64/`. All x64 and Alpine consumers break. Arm64 users were already on a workaround.

**With mitigation (recommended):** ship a per-RID layout in the NuGet `contentFiles`:

```
contentFiles/any/netstandard2.0/newrelic/
├── linux-x64/libNewRelicProfiler.so
├── linux-musl-x64/libNewRelicProfiler.so
├── linux-arm64/libNewRelicProfiler.so
└── linux-musl-arm64/libNewRelicProfiler.so
```

…**and** keep a compat copy at `newrelic/libNewRelicProfiler.so` in the package, defaulted to `linux-x64`. (Yes, this means shipping the same x64 .so twice in the package — measured size of one copy ~15 MB.) Alternatively, generate the compat copy via a small build target (`NewRelic.Agent.targets`) at the consumer's `dotnet publish` time:

```xml
<Target Name="NewRelicCopyDefaultProfiler" AfterTargets="Publish">
  <Copy SourceFiles="$(PublishDir)newrelic/linux-x64/libNewRelicProfiler.so"
        DestinationFiles="$(PublishDir)newrelic/libNewRelicProfiler.so"
        Condition="'$(RuntimeIdentifier)' == '' Or $(RuntimeIdentifier.StartsWith('linux-x64'))" />
  <!-- analogous for musl-x64 / arm64 / musl-arm64 -->
</Target>
```

The .targets-driven approach has a downside: it triggers at the consumer's publish, so a bug there is invisible until the customer hits it. The "ship a duplicate copy" approach is simpler and visible at NuGet build time.

**New capability:** customers who run `dotnet publish -r linux-musl-x64` (today doesn't yield a working setup because we ship a glibc-only binary that lazily-binds on Alpine) get a proper musl-targeted binary in their publish output. This is a net positive that today's customers don't have.

#### Linux containers / Docker

The four documented patterns (each grounded in either New Relic-published examples or install docs):

1. **`apt-get install newrelic-dotnet-agent` inside Dockerfile, source `setenv.sh`.** No change.
2. **`apt-get install` inside Dockerfile, hardcode flat-path env vars** (our SmokeTestApp pattern). **With mitigation**, no change — postinst symlink resolves.
3. **`COPY` from a build stage that downloaded the tarball, hardcode flat-path env vars.** **With mitigation**, no change provided either (a) tarball ships compat symlink, or (b) `setenv.sh` runs at least once.
4. **NuGet-based: customer's app references `NewRelic.Agent` and copies the published `newrelic/` dir into the runtime image.** **With mitigation**, no change provided NuGet keeps a default `newrelic/libNewRelicProfiler.so` compat copy.

**Net Phase 2 customer impact with all mitigations applied: zero for the four documented patterns above. The visible differences are upside — a customer who switches from a glibc base to an Alpine base no longer relies on lazy-binding luck; they get a properly Alpine-native binary.** Edge cases not covered by the four patterns (e.g., a customer using a non-documented attach mechanism we don't know about) require telemetry to size — flagged as an open item.

#### Kubernetes auto-attach (k8s-agents-operator + newrelic-agent-init-container)

The .NET agent's K8s auto-attach is a separate two-repo system that customers consume independently of the agent home directory and tarball:

- **`newrelic-agent-init-container`** ships a per-arch (`linux/amd64`, `linux/arm64`) image whose `dotnet-agent-download.sh` fetches the agent tarball from `https://download.newrelic.com/dot_net_agent/...` and unpacks it to `/instrumentation` inside the image. The image runtime is `busybox:stable`. The init container is later `cp -r`'d into the target pod's volume by the operator.
- **`k8s-agents-operator`** mutates pod specs at admission. `internal/apm/dotnet.go:59` (verified 2026-05-18 against `main` of the operator repo) constructs the profiler path as a flat reference:
  ```go
  coreClrProfilerPath := mountPath + "/libNewRelicProfiler.so"
  ```
  This becomes the value of `CORECLR_PROFILER_PATH` injected into the customer's app container. There is no annotation today for selecting libc / RID.

**Today, K8s customers running on Alpine-based pods work via the same glibc-on-Alpine lazy-binding luck as the package-install case** — the operator hardcodes the flat path; the init container ships only the glibc binary (the tarball doesn't have musl variants); the pod's app container loads the glibc `.so` and the loader's lazy binding masks the missing musl-symbol references at startup. Same mechanism, different attach surface.

**Phase 2 has a coordinated three-repo dependency for non-breaking K8s support:**

1. **Agent (this design / dual-build PR series):** ship a tarball whose extracted layout has per-RID subdirectories AND a libc-aware compat symlink at the home root (per the "Mitigation summary" below). The init container's `cp -r /instrumentation/.` copy preserves the symlink, and the operator's `mountPath + "/libNewRelicProfiler.so"` resolves to whichever variant the symlink points at.

2. **Init container (`newrelic-agent-init-container`):** no functional change strictly required if (1) is done correctly — the existing `cp -r` copies whatever's in the tarball, so the per-RID subdirs and the symlink come along for free. Optional improvement: the init container could probe the *target* pod's libc at copy time and rewrite the symlink, but that requires the init container to introspect the target volume's intended runtime, which is awkward. Keeping the symlink at glibc-by-default and relying on a runtime annotation override (item 3) is simpler.

3. **Operator (`k8s-agents-operator`):** add an annotation analogous to OTel's `otel-dotnet-auto-runtime` to let customers explicitly opt into the musl variant. Recommend `newrelic.com/instrumentation-dotnet-runtime` (the operator's existing annotation namespace pattern); accepted values mirror .NET RIDs. When the annotation is set, the operator constructs `coreClrProfilerPath` from the matching per-RID subdir:

   ```yaml
   metadata:
     annotations:
       instrumentation.newrelic.com/inject-dotnet: "true"
       instrumentation.newrelic.com/dotnet-runtime: "linux-musl-x64"  # opt-in for musl-based pods
       # Default (annotation absent): "linux-x64"
       # Other valid values: "linux-arm64", "linux-musl-arm64"
   ```

   With the annotation set, `coreClrProfilerPath` becomes `mountPath + "/" + rid + "/libNewRelicProfiler.so"`; without it, the existing `mountPath + "/libNewRelicProfiler.so"` flat path resolves to the libc-aware compat symlink (which on a glibc-built tarball points at `linux-x64/libNewRelicProfiler.so`).

   This is **opt-in for musl** — same pattern OTel uses, and for the same reason: at pod-spec mutation time the operator cannot detect the target container's libc, so an explicit annotation is the only signal available before the container is running. Customers running .NET on Alpine pods will need to add the annotation in Phase 2 if they want the proper musl-native binary; without the annotation, they continue to get the glibc binary loaded via lazy-binding luck (same as today). Phase 3 closes that fallback — Alpine customers must have the annotation set before Phase 3 ships.

**K8s customer impact (per-phase summary):**

| Phase | Alpine pod, no annotation | Alpine pod, `dotnet-runtime: "linux-musl-x64"` | glibc pod (any) |
|---|---|---|---|
| Today | Works via lazy-binding luck | n/a (annotation doesn't exist) | Works |
| Phase 1 (this PR) | Unchanged — operator/init container untouched | n/a | Unchanged |
| Phase 2 | Continues to work via lazy-binding luck (compat symlink → glibc binary on Alpine pod, loads via luck) | Works on the proper musl-native binary; no luck involved | Works (compat symlink → glibc binary) |
| Phase 3 | **Breaks** — glibc binary stops loading on Alpine. Customer must have added the annotation. | Works — explicit musl path | Works |

**Communication and timing:**
- Phase 2 release notes for the operator must call out the new annotation as the migration path for Alpine pods. Encourage customers to add the annotation before Phase 3 ships.
- The operator change is technically optional for Phase 2 functionally (today's behavior is preserved by the compat symlink), but is **required** before Phase 3 — otherwise Alpine K8s customers break with no way to recover short of editing `CORECLR_PROFILER_PATH` manually.
- The operator change must coordinate with this dual-build PR series. Track as a dependent change-set, not a follow-up.

#### Linux containers — Alpine specifically

**Today:** the shipped glibc binary loads on Alpine via the four-property luck (DT_NEEDED narrow, GLIBC ≤ 2.17, no libc++, lazy-binding). `strtoll_l` / `strtoull_l` are latent fragility — any code path that hits locale-aware parsing on Alpine throws.

**After Phase 2:**
- If the install creates the postinst/setenv.sh compat symlink: the Alpine container resolves the legacy `…/libNewRelicProfiler.so` path to the **musl-native** binary. Customer transparently moves off the lazy-binding mechanism. No more `strtoll_l` fragility.
- If the install does **not** create the compat symlink (some non-postinst install paths): customer must update `CORECLR_PROFILER_PATH` to the per-RID path. **Phase 2 must therefore ensure the compat symlink is created on every supported install path** (postinst for `.deb`/`.rpm`, build-time pre-bake for tarball, NuGet compat-copy for the NuGet package). Without that universal coverage the change is breaking for the majority of installs, since the dominant install pattern is to hardcode `CORECLR_PROFILER_PATH`.

**After Phase 3 (glibc baseline tracks dotnet's portable baseline — 2.27 if .NET 10 is the floor):** two distinct breaking-change populations:

**(a) Customers on EOL distros are dropped — same set dotnet itself drops.** A glibc 2.27 floor exactly matches Microsoft's portable .NET 10 build base (Ubuntu 18.04). The dropped set:
- **Amazon Linux 2** (glibc 2.26) — AWS EOL 2026-06. Not a .NET 10-supported distro per Microsoft's own table.
- **RHEL 7** (glibc 2.17) — Red Hat EOL 2024-06. Not a .NET 10-supported distro.
- **Ubuntu 18.04 stays in** (it is the build floor, not above it).

These customers see `dlopen` fail at agent attach with a clear error from `ld-linux.so` ("`/lib/x86_64-linux-gnu/libc.so.6: version 'GLIBC_2.27' not found`"). They are **not** silently un-monitored — the failure is loud, traceable, and consistent with dotnet 10's own behavior on these systems. Migration: move to AL2023, RHEL 8/9, Ubuntu 18.04+ (any LTS), or pin the .NET agent to the pre-Phase-3 release line.

**(b) Customers on Alpine who somehow resolve to the glibc binary.** Possible if: a stale postinst symlink wasn't refreshed during a layered base-image swap; a NuGet customer with `RuntimeIdentifier=linux-x64` deploys to Alpine by mistake; a customer hand-set `CORECLR_PROFILER_PATH` to the `linux-x64/` path on an Alpine host. Today this case "works" via lazy-binding luck. After Phase 3 it fails loudly. The fix is always the same: source `setenv.sh` (does the libc probe correctly) or set `CORECLR_PROFILER_PATH` to `linux-musl-{arch}/libNewRelicProfiler.so`.

**Communication plan for Phase 3:**
- Pre-announce the AL2/RHEL7/U18 drops at least one minor-version cycle ahead. Recommend: announce in the release that lands Phase 2; ship Phase 3 in a subsequent release.
- Update support runbooks: "agent stopped loading after upgrade and customer is on AL2/RHEL7/U18" → expected; recommend distro upgrade or pin the previous agent line.
- Release notes call out distro drops at the top, not buried in a changelog.
- The Phase 3 release is a candidate for a major version bump given the supported-distro change.

#### Customer-defined CORECLR_PROFILER_PATH (escape hatch)

The agent already respects an explicit `CORECLR_PROFILER_PATH` set by the customer (the `setenv.sh` body checks `[ -z "$..." ]` before exporting). After Phase 2, this hatch still works — a customer can hardcode `CORECLR_PROFILER_PATH=$NRHOME/linux-musl-x64/libNewRelicProfiler.so` if they want explicit control, e.g., to force a specific binary in a multi-arch image.

#### Custom instrumentation, `newrelic.config`, license keys

**Zero impact.** The newrelic.config file, `extensions/*.xml`, log directory, NEW_RELIC_LICENSE_KEY, and all other configuration surface are libc-independent and stay at the home root in the new layout. Custom instrumentation files (which the docs tell customers to drop into `extensions/`) are unaffected.

#### `CORECLR_NEWRELIC_HOME` and other env vars

**Zero impact.** `CORECLR_NEWRELIC_HOME`, `CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `NEW_RELIC_LICENSE_KEY`, `NEW_RELIC_LOG_DIRECTORY`, `NEW_RELIC_HOST`, etc. — all unchanged. Only `CORECLR_PROFILER_PATH` is potentially affected, and only for customers who hardcode it.

#### macOS, BSD, other non-Linux Unix

**Zero impact** — not supported today, not added by this design.

#### Customers on existing agent versions (no upgrade)

**Zero impact.** Existing agent installs continue to ship the existing flat-path binary. The dual-build only affects new releases. Customers are in control of when to upgrade.

### Mitigation summary (must-haves for "non-breaking" claim)

The dominant Linux install pattern is hardcoded `CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so`. Every item below is **load-bearing** for shipping Phase 2 non-breaking — none are optional belt-and-suspenders. All must land in the same release:

1. **`.deb` / `.rpm` postinst** creates the legacy flat-path as a libc-aware symlink to the matching `linux-{musl-,}{x64,arm64}/libNewRelicProfiler.so`. **Required** — covers the dominant package-install pattern.
2. **`setenv.sh`** creates/refreshes the symlink idempotently on each source. **Required** — handles tarball-into-different-libc-container migrations and serves as a fallback recovery path. The detection should re-point the symlink if its current target's libc doesn't match the host libc.
3. **Tarball build** ships a default `libNewRelicProfiler.so` symlink at the home root, pointing at `linux-x64/libNewRelicProfiler.so` — chosen because the tarball is built on a glibc host and the linux-x64 binary is the only sensible single default for a pre-baked symlink. **Required** — covers the dominant tarball-install pattern (extract on glibc, hardcode env vars, deploy to glibc image) without requiring the customer to source `setenv.sh`. The Alpine-tarball edge case is covered by item 2 if the customer ever sources `setenv.sh`; release notes cover the remaining sliver who do neither.
4. **`NewRelic.Agent` NuGet package** ships a compat copy `newrelic/libNewRelicProfiler.so` (= linux-x64 glibc) **plus** the per-RID layout for `dotnet publish` RID resolution. **Required** — the documented NuGet attach path on `nuget2.mdx` is the flat path; without the compat copy, every NuGet customer breaks. The arm64 customer who already had to override gets no worse; the x64 customer with a hardcoded `CORECLR_PROFILER_PATH` keeps working.
5. **Update our own `tests/Agent/IntegrationTests/ContainerApplications/SmokeTestApp/Dockerfile*`** to use the new RID-aware path **and** to source `setenv.sh`. Tests-only change but **required**: today our tests model the customer pattern that's about to break. Tests must explicitly verify both code paths (legacy compat symlink works; new RID-aware path works) so regressions in the compat symlink would fail CI rather than reach customers.
6. **`run.sh`** rewritten to do the libc probe + RID resolution.
7. **`k8s-agents-operator` updated** to support a `instrumentation.newrelic.com/dotnet-runtime` annotation (opt-in pattern, mirroring OTel's `otel-dotnet-auto-runtime`). When the annotation is set, the operator constructs `coreClrProfilerPath` from the matching per-RID subdir (`mountPath + "/" + rid + "/libNewRelicProfiler.so"`); when absent, the operator continues to set the flat path (`mountPath + "/libNewRelicProfiler.so"`) which resolves via the libc-aware compat symlink. **Required for Phase 2 if we want Alpine K8s customers to have an explicit upgrade path before Phase 3**; functionally optional for Phase 2 in isolation (today's lazy-binding-luck behavior is preserved by the symlink) but **required before Phase 3** ships.
8. **`newrelic-agent-init-container` validated** against the new tarball layout. No code changes strictly required — the existing `cp -r /instrumentation/.` copy preserves the per-RID subdirs and the symlink — but a tarball-shape integration test should be added to the init container repo's test matrix to guard against the layout regression.
9. **Release notes** explicitly document:
   - The new layout (per-RID subdirs) — for forward-looking customers who want to migrate to the explicit RID path.
   - The compat symlink — call out that the flat path **continues to work** for the majority of installs unchanged, so customers don't need to update unless they want to.
   - The single edge case requiring action: extracting a tarball into an Alpine container while never sourcing `setenv.sh` and never running postinst. This subset must source `setenv.sh` once (which fixes the symlink) or set `CORECLR_PROFILER_PATH` explicitly.
   - Phase 3 forewarning: the upcoming release will raise the glibc floor to track dotnet's portable baseline for the agent's minimum supported runtime (glibc 2.27 / Ubuntu 18.04 if .NET 10 is the floor). Drops Amazon Linux 2 and RHEL 7 — both EOL upstream and unsupported by dotnet 10 itself. Alpine customers should also validate that their `CORECLR_PROFILER_PATH` resolves to the musl binary before Phase 3 ships, since the glibc-on-Alpine luck mechanism is gone after Phase 3.

### Open items requiring product / docs decisions

These cross outside the build/CI scope and need product owner input before the migration can ship:

1. **docs.newrelic.com refresh.** All Linux install pages in `newrelic/docs-website` currently show the flat path. They need updating in lockstep with Phase 2. Verified count: at least 7 English-language `.mdx` files (`linuxInstall3-enable-agent`, `dockerLinux`, `nuget2`, `azure-linux-container`, `aws-elastic`, `azure-nuget-core-install`, `understanding-net-agent-environment-variables`) plus translated copies in es/jp/kr/fr/pt — total ≥ several dozen pages once translations are counted. Plus the Dockerfile examples in `newrelic/newrelic-dotnet-examples/docker-agent-nuget/{alpine,ubuntu,centos}/`.
2. **AzureSiteExtension and Windows-side install pages** are unaffected but should be sanity-checked for Linux cross-references.
3. **Phase 3 release timing and distro-drop pre-announcement.** Phase 3 raises the glibc floor to track dotnet's portable baseline for the agent's minimum supported runtime (glibc 2.27 / Ubuntu 18.04 if .NET 10 is the floor). Drops Amazon Linux 2 and RHEL 7 — both EOL upstream and unsupported by dotnet 10 itself. Pre-announce in the release that ships Phase 2; ship Phase 3 in a subsequent release. Major-version bump candidate. Decisions needed: which release ships Phase 2 vs Phase 3, when the agent's minimum supported runtime moves off .NET 8 (Phase 3 is contingent on this), and whether the Phase 3 release coincides with the .NET agent's next major version.
4. ~~**Customer-survey or telemetry data** on how many active installs hardcode `CORECLR_PROFILER_PATH=…flat`.~~ **Resolved by team experience: the vast majority of `.deb`/`.rpm`/tarball installs hardcode the flat path.** The compat-symlink mitigations are load-bearing for non-breaking shipping; this is no longer an open item.
5. **Release-notes review with support team** so the support runbook for "agent stopped loading after upgrade" is updated to immediately ask "did you set `CORECLR_PROFILER_PATH` manually?".
6. **K8s auto-attach coordination across three repos.** The dual-build's full customer-facing rollout requires changes in `newrelic/k8s-agents-operator` (add `instrumentation.newrelic.com/dotnet-runtime` annotation, branch `coreClrProfilerPath` construction on it) and validation in `newrelic/newrelic-dotnet-agent-init-container` (confirm the new tarball layout copies cleanly via `cp -r`). These are tracked in separate repos and need an explicit dependency contract: the operator change should land in the same release as the agent's Phase 2, OR ahead of it — never lag, because Alpine K8s customers need the annotation as their migration path before Phase 3 ships.

---

## What this design does **not** include

- **Windows side**: unchanged. Profiler.dll continues to ship as today.
- **macOS support**: out of scope. The .NET agent has never supported macOS profiling; this design does not introduce it.
- **C++ standard bump or `std::wstring_convert` replacement**: still deferred per the modernization plan.
- **Resolving the `strtoll_l` / `strtoull_l` unresolved-symbol leak**: Phase 1 likely closes it on the musl binary (clean musl link), but the glibc binary still carries it until source-level changes land. Capture as follow-up.
- **A "fat" cross-libc binary**: not pursued. Some projects ship a single binary with `dlopen` shims; the cost (additional indirection on every CLR-callback hot path) is not worth the disk savings of one extra `.so`.

---

## Provenance of claims in this document

This doc was reviewed for fabricated or unsupported claims on 2026-05-15. Every fact below is in one of three categories. **Read this section before acting on any specific claim.**

### Category A — Independently verified during this investigation

- **OTel mechanism (Section 1)**: every OTel claim is grounded in a specific file in `https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation` HEAD as of 2026-05-15. Specific files cited inline (`docker/alpine.dockerfile`, `docker/ubuntu1604.dockerfile`, `.github/workflows/build.yml`, `.github/workflows/_build.yml`, `.github/workflows/build-nuget-packages.yml`, `.github/workflows/release.yml`, `.github/workflows/build-ubuntu1604-native-container.yml`, `build/Build.Steps.Linux.cs`, `build/Build.NuGet.Steps.cs`, `instrument.sh`, `script-templates/otel-dotnet-auto-install.sh.template`).
- **All file/line references in Section 3 (packaging touchpoints)** spot-checked against the working tree on 2026-05-15.
- **Both glibc binaries' four-property compliance**: independently re-verified via `readelf -d`, `readelf -V`, `nm -D --defined-only` on the actual artifacts downloaded from CI runs 25933232238 (x64, main) and 25934126059 / 25937065283 (arm64, branch). Results: x64 max GLIBC = 2.14, arm64 max GLIBC = 2.17, both with narrow DT_NEEDED, no libc++/libstdc++ in DT_NEEDED. Exported-symbol counts: x64 = 4551, arm64 = 4183 (counts differ across arches; this is expected — the user should not assume the exported set is identical between arches).
- **Customer install patterns from public New Relic-published sources**: the `newrelic/newrelic-dotnet-examples` repo Dockerfiles (`alpine/`, `ubuntu/`, `centos/`) all hardcode `CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so`. The `newrelic/docs-website` repo includes the flat path in at least 7 install pages: `src/install/dotnet/installation/{linuxInstall3-enable-agent,dockerLinux,nuget2,azure-linux-container,aws-elastic,azure-nuget-core-install}.mdx` plus `src/content/docs/apm/agents/net-agent/other-installation/understanding-net-agent-environment-variables.mdx`. Customer-pattern claims in Section ("What customers actually do") are grounded in this evidence.

### Category B — Stated in PROFILER_MODERNIZATION_PLAN.md, not independently re-verified here

- The arm64 binary's three lazy-binding holes (`strtoll_l`, `strtoull_l`, `RaiseException`) and the lazy-binding mechanism by which today's binary loads on Alpine.
- "Lazy binding (RTLD_LAZY) enabled" — this is a runtime-loader behavior, not a binary-shape property; the modernization plan asserts it. **Spike note:** the musl spike binary has `BIND_NOW` set (see Category A update below). Needs confirmation vs. today's glibc binary.
- Today's CMakeLists.txt flags (`-stdlib=libc++ -static-libstdc++ -fno-strict-aliasing -fms-extensions -fdeclspec -fPIC`) and the `-static-libstdc++` static-link assumption. **Spike note:** the flag set has been independently verified via the spike — see Category A below.

### Category A additions from 2026-05-15 musl spike

These facts were **directly measured** during the local spike (see `PROFILER_MUSL_SPIKE_REPORT.md`):

- **Alpine 3.23's `libc++-static` package is not built with `-fPIC`.** Attempting to pass `-static-libstdc++` with Alpine's stock `libc++-static` errors with `R_X86_64_PC32 ... can not be used when making a shared object`. Workaround attempted in spike: dynamic libc++ link. This causes the musl binary to fail the "no libc++/libstdc++ in DT_NEEDED" property.
- **The `__llvm__` macro fires for any clang build regardless of `-stdlib` choice.** `Common/xplat.h:32-59` adds a `std::make_unique` shim gated on `#if defined(__llvm__)`. Clang defines `__llvm__` even when using libstdc++. The shim conflicts with libstdc++'s own `std::make_unique`. Switching the musl path to libstdc++ therefore requires removing/guarding that shim.
- **`atl.h:367` (vendored coreclr header) has a real bug.** `this->pElements` in a never-called `CallConstructors` helper references a non-existent member. Clang ≥ 14's stricter two-phase name lookup catches this at parse time; clang-3.9 did not. Workaround in spike: `-fdelayed-template-parsing`. Clean fix: patch the vendored header.
- **The musl spike binary has `BIND_NOW` set** (and `FLAGS_1: NOW`) — not `RTLD_LAZY`. On Alpine, `BIND_NOW` causes dlopen to resolve all symbols immediately, including `RaiseException` (a CLR-provided symbol expected at runtime). The spike smoke test failure is partially attributable to this. Phase 1 must explicitly pass `-Wl,-z,lazy`.
- **Zero GLIBC_ references in the spike binary.** `readelf -V` shows no version-needs section — the binary has no glibc versioned symbol references at all. This is the primary positive finding.
- **The `RaiseException` ldd error in the spike is a pre-existing condition**, not a spike regression — the same behavior appears in the modernization plan for the arm64 binary.

### Category A additions from 2026-05-18 musl spike2 (libstdc++ migration validation)

These facts were **directly measured** during spike2 — the libstdc++ migration validation spike (see `PROFILER_MUSL_SPIKE_REPORT.md` spike2 addendum):

- **`sal.h` `#define __valid` conflicts with GCC 15 libstdc++ `bits/parse_numbers.h`.** The vendored coreclr header `externals/coreclr-headers/src/pal/inc/rt/sal.h:2444` defines `#define __valid` as an empty macro (SAL annotation keyword). GCC 15's `libstdc++` header `bits/parse_numbers.h` (included transitively via `<mutex>` → `<chrono>`) uses `__valid` as a template type-alias name. When `sal.h` is processed before `<mutex>` in any translation unit, the macro is already defined and `bits/parse_numbers.h` fails with `error: expected unqualified-id`. Fix: `#pragma push_macro("__valid") / #undef __valid` around the stdlib includes in `Logging/Logger.h`, then `#pragma pop_macro("__valid")` after. This protects the stdlib headers from the macro without touching the vendored header.
- **libstdc++ `<memory>` does not transitively include `<cstdint>`.** libc++'s `<memory>` did; libstdc++ does not. `Sicily/ast/GenericParamType.h` (`uint32_t`) and `Sicily/ast/TypeList.h` (`uint16_t`) both need an explicit `#include <cstdint>`.
- **libstdc++ `<mutex>` does not transitively include `<condition_variable>`.** `ThreadProfiler/ThreadProfiler.h` must add `#include <condition_variable>` explicitly.
- **libstdc++ is strict about `char16_t`→`wchar_t` copy in ostream contexts.** `Logger.h`'s `operator<<(wofstream, xstring_t)` previously used `std::copy` with `ostream_iterator<wchar_t, wchar_t>` — libc++ was lenient; libstdc++ rejects the char16_t source type. Fix: replace with an explicit `for (auto c : str) { _Ostr.put(static_cast<wchar_t>(c)); }` loop.
- **`-std=c++14` is required on the musl path.** libstdc++ only provides `std::make_unique` when `__cplusplus >= 201402L` (i.e., `-std=c++14` or later). The glibc path can remain at `-std=c++11` for now; the musl path must use `-std=c++14`.
- **Spike2 binary meets all four design-doc properties.** `DT_NEEDED`: `libc.musl-x86_64.so.1` only (no `libstdc++.so.6`, no `libc++.so.1`). GLIBC_ version refs: zero. Lazy binding: confirmed (`-Wl,-z,lazy` worked; no `BIND_NOW` flag). Smoke test: `Profiler initialized` emitted; 372 instrumentation XML points parsed; `DllGetClassObject` and `DllCanUnloadNow` present in exported set.
- **Exported symbol count 6989 vs glibc baseline 4551.** Higher count is expected: libstdc++ internals are now *statically linked* into the musl `.so` and surface in its dynamic symbol table, rather than residing in a separate `libstdc++.so.6` dynamic dependency. This does not affect correctness; critical COM entry points are present in both.

### Category C — General-knowledge claims I did not directly re-source during this investigation

These are commonly available facts but I did not pull primary citations for them. Treat with appropriate caution before publishing externally:

- **Distro → glibc version mappings** in the Phase 3 table (Ubuntu/Debian/RHEL/Amazon Linux/SUSE). All values are widely-published but were not freshly cross-checked.
- **Distro EOL dates**: RHEL 7 Maintenance Support 2 ended 2024-06-30; Amazon Linux 2 EOL extended to 2026-06-30 per AWS announcement; Ubuntu 18.04 standard support ended 2023-05-31 (the doc previously said "2023-04" — corrected to "2023-05-31").
- **manylinux variant base images** (manylinux2014 = CentOS 7; manylinux_2_28 = AlmaLinux 8; manylinux_2_34 = AlmaLinux 9). Standard PyPA conventions.
- **AlmaLinux 8 EOL 2029-05-31**.
- **GitHub-hosted arm64 runners GA + free for public repos** (the user's own PR #3593 follow-up confirmed they work; the "GA / free for public repos" framing is general-knowledge).
- **"PyPA's current default"** for manylinux — I softened this phrasing because PyPA supports multiple manylinux variants concurrently, no single variant is officially "the default."

### Category D — Inferences explicitly removed from the doc during this scrub

The following claims appeared in earlier drafts but lacked substantiation:

- "The shipped .so is ~3 MB per arch" — **wrong.** Actual measured size: x64 = 15.3 MB, arm64 = 14.4 MB.
- "Current .deb is ~25 MB" — not measured; removed.
- "<15% bloat from adding a musl variant" — derived from the wrong number above; replaced with qualitative statement.
- "Alpine users are usually savvier and source setenv.sh" — pure speculation; removed.
- "Almost everyone sources setenv.sh; the docs lead with it" — partially wrong. The canonical install page (`linuxInstall3-enable-agent.mdx`) offers setenv.sh as one of *two equally-presented* tabs, the other showing the flat-path env vars. Replaced with the precise framing.
