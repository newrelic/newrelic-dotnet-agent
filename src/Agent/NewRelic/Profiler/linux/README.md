## Linux profiler build Dockerfiles

### Active build images (used by CI)

**`Dockerfile`** — x64 Linux build container. Ubuntu 14.04, clang-3.9, cmake-3.9.
Used by the `build_profiler.yml` `build-linux-profiler-x64` job via `docker compose`.

**`Arm64Dockerfile`** — arm64 Linux build container. Ubuntu 18.04, clang-3.9,
cmake 3.9 pulled from an NR-owned S3 bucket.
Used by the `build_profiler.yml` `build-linux-profiler-arm64` job.

Both images compile with `-stdlib=libc++ -static-libstdc++`, which together with the
profiler's narrow set of external dependencies produces binaries that work on glibc-based
distros **and** musl-based distros (Alpine) — see below.

### Alpine compatibility

The shipped `libNewRelicProfiler.so` loads successfully on Alpine Linux (including the
`mcr.microsoft.com/dotnet/aspnet:*-alpine` images) **without `gcompat`**. This is not
accidental — it is a direct consequence of four binary properties that the current build
images preserve:

| Property | x64 | arm64 |
|---|---|---|
| DT_NEEDED | libm, libgcc_s, libpthread, libc | libm, libgcc_s, libpthread, libc, ld-linux-aarch64 |
| Max GLIBC_ version referenced | 2.14 | 2.17 |
| libc++/libstdc++ runtime dep | None (static-linked) | None |
| Lazy binding (RTLD_LAZY) | Yes | Yes |

Alpine's musl libc exposes the `libc.so.6` / `libm.so.6` / `libpthread.so.0` sonames,
satisfying the ELF loader, and provides native equivalents of all referenced glibc symbols
at those ancient version levels. `RaiseException` is a CoreCLR PAL export resolved from
the already-loaded `libcoreclr.so`. The three `ldd`-reported misses (`strtoll_l`,
`strtoull_l`) are lazy-binding holes that the profiler's normal code paths never reach.

**Do not break these properties.** Any build-image change that raises the max GLIBC_
version above 2.17, adds a libc++/libstdc++ `DT_NEEDED`, or changes the `DT_NEEDED`
soname set will break Alpine. Verify with `readelf -d` and `readelf -V` before merging.

### Other files

**`DebugDockerfile`** — a work-in-progress debugging image (Ubuntu 18.04, clang-7,
dotnet-sos, lldb). Not used by CI. Still references the old `dotnet/coreclr` clone
that was removed from the main build in PR #3576; this file has not been updated.

### Planned modernization

The `Dockerfile` and `Arm64Dockerfile` images are based on old Ubuntu / clang versions
and have known reliability risks (Ubuntu 14.04 EOL, apt-key deprecation, S3-hosted cmake
for arm64). The hard constraint on any modernization effort is preserving the four binary
properties listed above.
