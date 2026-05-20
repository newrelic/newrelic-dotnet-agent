## Linux profiler build Dockerfiles

### Active build images (used by CI)

**`Dockerfile`** — x64 glibc Linux build container. Ubuntu 14.04, clang-3.9, cmake-3.9.
Used by the `build_profiler.yml` `build-linux-profiler-x64` job via `docker compose`.

**`Arm64Dockerfile`** — arm64 glibc Linux build container. Ubuntu 18.04, clang-3.9,
cmake 3.9 pulled from an NR-owned S3 bucket.
Used by the `build_profiler.yml` `build-linux-profiler-arm64` job.

**`MuslDockerfile`** — x64 + arm64 musl Linux build container. Alpine-based,
clang + libstdc++ toolchain. Used by the `build_profiler.yml`
`build-linux-musl-x64-profiler` and `build-linux-musl-arm64-profiler` jobs.

The agent ships **four** Linux profiler binaries — one per (libc, arch) combination —
into per-RID subdirectories of the agent home: `linux-x64/`, `linux-arm64/`,
`linux-musl-x64/`, `linux-musl-arm64/`. A libc-aware compat symlink at the home root
keeps the legacy flat path (`$NRHOME/libNewRelicProfiler.so`) working for customers
who hardcode `CORECLR_PROFILER_PATH`. The symlink is created by the `.deb`/`.rpm`
postinst, by `setenv.sh` on first source, and pre-baked at tarball build time.

### Alpine compatibility

Alpine support comes from the dedicated **`linux-musl-{x64,arm64}/libNewRelicProfiler.so`**
binary — a true musl-native build linked against musl libc and libstdc++. On an Alpine
host, the postinst (or `setenv.sh`) detects musl via `ldd /bin/ls 2>&1 | grep musl` and
points the home-root compat symlink at the musl variant.

**Historical context (legacy compat path).** Before the dual-build, the single shipped
glibc binary loaded on Alpine via four properties of its build:

| Property | x64 | arm64 |
|---|---|---|
| DT_NEEDED | libm, libgcc_s, libpthread, libc | libm, libgcc_s, libpthread, libc, ld-linux-aarch64 |
| Max GLIBC_ version referenced | 2.14 | 2.17 |
| libc++/libstdc++ runtime dep | None (static-linked) | None |
| Lazy binding (RTLD_LAZY) | Yes | Yes |

Alpine's musl exposes the `libc.so.6` / `libm.so.6` / `libpthread.so.0` sonames,
satisfying the ELF loader, and provides native equivalents of those ancient glibc
symbols. `RaiseException` is a CoreCLR PAL export resolved from `libcoreclr.so`. The
`ldd`-reported lazy-binding holes (`strtoll_l`, `strtoull_l`) were not hit by the
profiler's normal code paths. This was a fragile arrangement that survived through
luck, not design — any build-image change that raised the max GLIBC_ version, added a
libc++/libstdc++ `DT_NEEDED`, or changed the `DT_NEEDED` soname set would have broken it.

**The dedicated musl binary supersedes that mechanism.** The four-property invariant
above no longer governs Alpine compatibility — it governs only the glibc binary's
floor for old glibc distros. Alpine customers transparently move onto the musl-native
binary via the compat symlink; no `gcompat` workaround, no lazy-binding luck, no
`strtoll_l` fragility.

### Other files

**`DebugDockerfile`** — a work-in-progress debugging image (Ubuntu 18.04, clang-7,
dotnet-sos, lldb). Not used by CI. Still references the old `dotnet/coreclr` clone
that was removed from the main build in PR #3576; this file has not been updated.

### Planned modernization (Phase 3)

With Alpine compatibility now provided by the dedicated musl binary, the glibc build
base no longer has to preserve the four-property invariant — it is free to track .NET's
own portable-build glibc baseline (Ubuntu 18.04 / glibc 2.27 if .NET 10 is the agent's
minimum supported runtime). Phase 3 of the dual-build effort moves the glibc base
forward, drops the libc++ static-link, and addresses the Ubuntu 14.04 EOL / apt-key /
S3-cmake reliability risks. Phase 3 is gated on the agent moving its minimum supported
runtime past .NET 8.
