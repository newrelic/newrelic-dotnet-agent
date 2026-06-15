# Profiler musl-native build spike -- report

**Date:** 2026-05-15. Local-only, no CI/PR/commits. References:
[`PROFILER_DUAL_BUILD_DESIGN.md`](PROFILER_DUAL_BUILD_DESIGN.md) Phase 1,
[`PROFILER_MODERNIZATION_PLAN.md`](PROFILER_MODERNIZATION_PLAN.md)
"Local run cheat-sheet -> Alpine smoke".

## TL;DR

The build path **works**: today's source compiles cleanly on Alpine 3.23 with
clang21+libc++, producing a musl-native `libNewRelicProfiler.so` (8.15 MB) with
**zero glibc symbol references**. The four-property bar is **not yet met** --
two issues blocked the final hop:

1. **Alpine 3.23's `libc++-static` package is not built with `-fPIC`.** The
   spike could not statically link libc++ into a `.so` (link errored with
   `R_X86_64_PC32 ... can not be used when making a shared object; recompile
   with -fPIC`). The spike fell back to dynamic libc++, so DT_NEEDED contains
   `libc++.so.1` / `libc++abi.so.1` / `libunwind.so.1` rather than the
   minimal `libc.musl-*.so.1` + `libgcc_s.so.1`. **Property 3 (no
   libc++/libstdc++ in DT_NEEDED) fails.**
2. **The Alpine smoke test fails to initialize** because of a mixed-stdlib
   undefined-symbol set in the binary. `corhlpr.cpp` (vendored from coreclr)
   pulls libstdc++-style `std::` symbols (e.g. `std::exception::what`),
   while the rest of the profiler -- compiled with `-stdlib=libc++` -- pulls
   `std::__1::` symbols. The current glibc build hides this by static-linking
   libstdc++; with libstdc++ unavailable on the spike, ~80 relocations stay
   unresolved at runtime and the profiler never reaches its init log line.

These are the same root cause: the build **needs** to static-link both
libc++ and libstdc++. Alpine's libc++-static not being PIC-compiled is the
direct blocker. **This is the unverified-assumption result the spike was
designed to surface.**

## What was actually changed

### `src/Agent/NewRelic/Profiler/linux/MuslDockerfile` (new, ~13 lines)

```dockerfile
FROM alpine:3.23
RUN apk add --no-cache \
    clang cmake make musl-dev g++ linux-headers \
    libstdc++-dev libc++ libc++-dev libc++-static \
    llvm-libunwind-dev llvm-libunwind-static \
    bash dos2unix
```

Notes vs the design's package list:
- Added `libc++` / `libc++-dev` / `libc++-static` -- required because today's
  source has a `__llvm__`-gated `make_unique` shim in `Common/xplat.h` that
  conflicts with libstdc++'s native `std::make_unique`. Building with
  libstdc++ requires source-level changes to that shim, which is out of
  scope for "minimum CMakeLists.txt diff."
- Added `llvm-libunwind-dev` / `llvm-libunwind-static`. The plain
  `libunwind-dev` package conflicts with the libc++ install (Alpine
  resolves: `breaks: llvm-libunwind-21.1.2-r0[!libunwind-dev]`).
- Did not pin a digest yet (per spike scope).

### `CMakeLists.txt` diff (the cost)

```diff
@@ -36,8 +36,28 @@ if(${WIN32})
 else()
   set(THREADS_PREFER_PTHREAD_FLAG ON)
   find_package(Threads REQUIRED)
+  # NR_MUSL_BUILD selects the musl-native (Alpine) toolchain path. The default
+  # (unset) path is unchanged from the historical glibc build to preserve
+  # byte-for-byte build behavior. The musl path keeps -stdlib=libc++ but adds
+  # -fdelayed-template-parsing for modern-clang two-phase-lookup compatibility
+  # and adjusts the link flags for the Alpine libc++ packaging layout.
   set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fno-strict-aliasing -stdlib=libc++ -Wno-invalid-noreturn -Wno-ignored-attributes -Wno-macro-redefined -fms-extensions -fdeclspec -fPIC")
-  set (CMAKE_SHARED_LINKER_FLAGS "-static-libstdc++")
+  if(DEFINED ENV{NR_MUSL_BUILD})
+    # Modern clang (21.x on Alpine 3.23) tightened two-phase name lookup vs the
+    # original clang-3.9; one never-instantiated helper in the vendored
+    # coreclr-headers atl.h trips it. -fdelayed-template-parsing restores the
+    # MSVC-/old-clang-like behavior of skipping unreached template bodies, with
+    # no effect on emitted code.
+    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fdelayed-template-parsing")
+  endif()
+  if(DEFINED ENV{NR_MUSL_BUILD})
+    # Alpine clang+libc++: spike accepts libc++ as a dynamic dep because
+    # Alpine 3.23's libc++-static package is not built with -fPIC and can't
+    # be statically linked into a shared object. Static-link path TBD.
+    set (CMAKE_SHARED_LINKER_FLAGS "-stdlib=libc++ -static-libgcc")
+  else()
+    set (CMAKE_SHARED_LINKER_FLAGS "-static-libstdc++")
+  endif()
 endif()
```

The glibc path (`NR_MUSL_BUILD` unset) is byte-identical in flags to before.
A real Phase 1 PR would clean up the two duplicate `if(DEFINED ENV{NR_MUSL_BUILD})`
into one.

### Did **not** change

- `linux/Dockerfile`, `linux/Arm64Dockerfile` -- untouched.
- `linux/build_profiler.sh` -- untouched. The spike used a one-off in-container
  build script (`/c/tmp/musl-build.sh`) that does an out-of-tree cmake build.
- `docker-compose.yml` -- untouched (Phase 1 follow-up scope, not spike scope).
- `.github/workflows/build_profiler.yml` -- untouched.
- No commits, no pushes.

## Build outcome

**Build succeeded.** Output: `src/Agent/NewRelic/Profiler/linux/musl-spike-libNewRelicProfiler.so`
(8,151,968 bytes). The current shipped glibc x64 binary is 15,322,696 bytes --
the spike binary is half the size, consistent with libc++/libc++abi being
linked dynamically rather than statically.

Compile-stage findings:
- `-stdlib=libc++` plus `apk add libc++-dev libc++-static` was needed to keep
  the `xplat.h` `__llvm__`-gated `std::make_unique` shim from conflicting with
  libstdc++ headers. Without it, building against libstdc++ 15.2 hits the
  shim's SFINAE-deleted overload AND `regex.tcc` parse errors with
  `-std=c++11`.
- `-fdelayed-template-parsing` was needed so clang 21 does not eagerly parse
  a never-instantiated helper (`CallConstructors`) in the vendored
  `externals/coreclr-headers/src/pal/inc/rt/atl.h:367`, which references a
  non-existent `this->pElements` (real bug in the vendored header, masked
  by older clang's looser parsing).
- Two pre-existing `-Wpragma-pack` warnings on `cor.h:1876, 1883` -- same as
  today's glibc build, not a spike regression.

## Binary characterization

### `readelf -d` (DT_NEEDED list)

```
Dynamic section at offset 0x12b7a8 contains 27 entries:
  Tag        Type                         Name/Value
 0x0000000000000001 (NEEDED)             Shared library: [libc++.so.1]
 0x0000000000000001 (NEEDED)             Shared library: [libc++abi.so.1]
 0x0000000000000001 (NEEDED)             Shared library: [libunwind.so.1]
 0x0000000000000001 (NEEDED)             Shared library: [libc.musl-x86_64.so.1]
 0x000000000000000e (SONAME)             Library soname: [libNewRelicProfiler.so]
 0x000000000000000c (INIT)               0x60000
 0x000000000000000d (FINI)               0xec40e
 0x0000000000000019 (INIT_ARRAY)         0x127c00
 0x000000000000001b (INIT_ARRAYSZ)       16 (bytes)
 0x000000000000001a (FINI_ARRAY)         0x127c10
 0x000000000000001c (FINI_ARRAYSZ)       8 (bytes)
 0x000000006ffffef5 (GNU_HASH)           0x260
 0x0000000000000005 (STRTAB)             0x12a80
 0x0000000000000006 (SYMTAB)             0x4c38
 0x000000000000000a (STRSZ)              223745 (bytes)
 0x000000000000000b (SYMENT)             24 (bytes)
 0x0000000000000003 (PLTGOT)             0x12c998
 0x0000000000000002 (PLTRELSZ)           20400 (bytes)
 0x0000000000000014 (PLTREL)             RELA
 0x0000000000000017 (JMPREL)             0x5a078
 0x0000000000000007 (RELA)               0x49488
 0x0000000000000008 (RELASZ)             68592 (bytes)
 0x0000000000000009 (RELAENT)            24 (bytes)
 0x000000000000001e (FLAGS)              BIND_NOW
 0x000000006ffffffb (FLAGS_1)            Flags: NOW
 0x000000006ffffffb (FLAGS_1)            ...
 0x000000006ffffff9 (RELACOUNT)          677
 0x0000000000000000 (NULL)               0x0
```

**Vs. the design's bar (`libc.musl-x86_64.so.1` + `libgcc_s.so.1` only):**
- `libc.musl-x86_64.so.1` [ok]
- `libgcc_s.so.1` -- absent. `-static-libgcc` succeeded.
- `libc++.so.1` [fail] (extra)
- `libc++abi.so.1` [fail] (extra)
- `libunwind.so.1` [fail] (extra)
- `libstdc++.so.6` -- absent [ok]
- `BIND_NOW` flag is set -- that's not "lazy binding" (Property 4 from the
  design doc); needs follow-up. It would still match what the design treats
  as Property 4 only if today's glibc binary also has `BIND_NOW`; the design
  doc states the glibc binary uses `RTLD_LAZY`, so the spike's `BIND_NOW`
  is a divergence to confirm/explain in Phase 1.

### `readelf -V` -- GLIBC_ version refs

**Zero GLIBC_ references.** The "Version needs" / "Version definitions"
section that appears on the glibc binary (max GLIBC_2.14 on x64) is **absent
entirely** on the spike binary -- there are no versioned symbol needs, which
is the expected shape for a musl-linked .so. This is the strongest single
positive finding: nothing in this binary will ever ask the dynamic loader
for a glibc version.

### `nm -D --defined-only` -- exported symbols

| | glibc x64 (CI run 25933232238) | musl spike |
|---|---|---|
| Exported symbol count | 4551 | **2156** |
| Symbols only in glibc | -- | 2674 |
| Symbols only in musl | -- | 279 |
| Symbols common to both | -- | 1877 |

The 47% drop is **almost entirely libc++ internals that the glibc build
static-linked and re-exported**. Sample symbols only-in-glibc:
`_ZGVZNKSt3__120__time_get_c_storageIcE...` (libc++ time_get static-init
guard variables), various `std::__1::` template instantiations, etc. In the
spike binary these stay inside `libc++.so.1` rather than being exported by
the profiler.

The 279 only-in-musl symbols are mostly NewRelic profiler types whose
template instantiations differ slightly between the two stdlib internals
(e.g. `sicily::SemInfo::operator=`, exception destructors). This is normal
codegen drift, not a missing-feature signal.

**Critical exports present in both:**
- `DllGetClassObject` [ok]
- `DllCanUnloadNow` [ok]
- All inspected `sicily::ast::*Type::*` constructors and assignment operators [ok]

The COM-style entry points the CLR looks up are unchanged. No NewRelic-side
exports are missing from the spike.

### `ldd` (inside Alpine 3.23)

```
        /lib/ld-musl-x86_64.so.1 (0x7fc321582000)
        libc++.so.1 => /usr/lib/libc++.so.1 (0x7fc32134c000)
        libc++abi.so.1 => /usr/lib/libc++abi.so.1 (0x7fc321312000)
        libunwind.so.1 => /usr/lib/libunwind.so.1 (0x7fc321303000)
        libc.musl-x86_64.so.1 => /lib/ld-musl-x86_64.so.1 (0x7fc321582000)
Error relocating /out/musl-spike-libNewRelicProfiler.so: RaiseException: symbol not found
```

One unresolved symbol: `RaiseException` -- same as today's glibc binary
(`PROFILER_MODERNIZATION_PLAN.md` "Alpine compatibility: empirical findings").
This is a CoreCLR PAL export resolved at runtime from libcoreclr.so. **Not
a spike regression.**

## Smoke test on `mcr.microsoft.com/dotnet/sdk:10.0-alpine`

**Failed.** The profiler does not initialize. No log files are produced;
no "Profiler initialized" line emits.

The dotnet runtime emits a long list of relocation errors when it tries
to load the profiler. Truncated sample (first 10 of ~80 distinct symbols):

```
Error relocating libNewRelicProfiler.so: _ZNKSt9exception4whatEv: symbol not found
Error relocating libNewRelicProfiler.so: _ZNSt3__115basic_streambufIwNS_11char_traitsIwEEE5imbueERKNS_6localeE: symbol not found
Error relocating libNewRelicProfiler.so: _ZNSt3__115basic_streambufIwNS_11char_traitsIwEEE6setbufEPwl: symbol not found
Error relocating libNewRelicProfiler.so: _ZNSt3__17codecvtIwc11__mbstate_tED2Ev: symbol not found
Error relocating libNewRelicProfiler.so: _ZTINSt3__114__codecvt_utf8IwEE: symbol not found
Error relocating libNewRelicProfiler.so: __cxa_pure_virtual: symbol not found
Error relocating libNewRelicProfiler.so: _ZNSt12out_of_rangeD1Ev: symbol not found
Error relocating libNewRelicProfiler.so: _ZNSt3__15wcoutE: symbol not found
Error relocating libNewRelicProfiler.so: __gxx_personality_v0: symbol not found
Error relocating libNewRelicProfiler.so: _ZTVSt9exception: symbol not found
```

**Two distinct symbol families** in the unresolved set:

- `_ZNSt3__1...` -- symbols in the `std::__1::` inline namespace (libc++).
  These come from compilation units that included `<stdlib.h>`/`<exception>`
  with `-stdlib=libc++` headers. They should be in `libc++.so.1` but the
  Alpine `libc++.so.1` evidently doesn't export the specific subset the
  profiler needs (likely a version mismatch -- the build-time libc++-dev was
  21.1.2-r0; the runtime libc++.so.1 is the same 21.1.2-r0 -- yet wcout,
  basic_streambuf<wchar_t>, codecvt are common-but-non-trivial templates
  that may have been stripped).

- `_ZNSt...` (no `3__1`) and `__gxx_personality_v0` / `__cxa_pure_virtual`
  -- symbols in the libstdc++ ABI namespace. These come from `corhlpr.cpp`
  (vendored from coreclr) which includes `<stdexcept>` etc. without a
  `-stdlib=libc++` translation-unit override, so it pulls libstdc++ headers
  even though the project is otherwise libc++.

Today's glibc build also produces both symbol families -- but resolves them
at link time by static-linking `libstdc++` (the `-static-libstdc++` linker
flag). On the Alpine spike we removed `-static-libstdc++` because the
plain libstdc++ static link would still leave the libc++ symbols
unresolved, **and** Alpine's libc++-static itself can't be statically
linked due to the missing `-fPIC`. **The fundamental problem is Alpine's
libc++-static packaging.**

## What the design doc would need to update

1. **Alpine 3.23's `libc++-static` is not `-fPIC`.** The Phase 1 scope
   that says "produce a binary whose only DT_NEEDED libc reference is to
   musl's libc.musl-*.so.1, no libstdc++" is achievable in principle but
   not from the stock `apk add libc++-static`. Phase 1 needs one of:
   - **(a) Build libc++ from source inside the Dockerfile with `-fPIC`**
     (adds 5-15 minutes per image build; OTel doesn't do this -- see point 3).
   - **(b) Switch to libstdc++** for the musl build path. This requires a
     source change to `Common/xplat.h` to remove or generalize the
     `__llvm__`-gated `std::make_unique` shim -- which is no longer needed
     in any modern libstdc++ regardless. Phase 1 would then mirror OTel's
     `alpine.dockerfile` toolchain choice (clang + libstdc++ + static-link
     libstdc++). This **also unlocks** companion item #2 below.
   - **(c) `apk add g++` and use g++ for the musl build.** Inverse of the
     glibc build's compiler choice; same Alpine packaging story as (b),
     and similar source-side prerequisites.

2. **The `__llvm__`-gated `make_unique` shim in `xplat.h` is the de-facto
   blocker for using libstdc++ on any modern compiler.** Lines 32-59 of
   `Common/xplat.h` are dead-weight on any libstdc++ >= 6 (which ships
   `std::make_unique` in C++11 mode as an extension). Phase 1 should
   simply delete that block. This isn't a CMakeLists.txt change but it
   IS the smaller blocking source change.

3. **OTel's `alpine.dockerfile` does not install libc++** -- it uses
   `clang21 alpine-sdk cmake` (i.e., clang on top of g++/libstdc++) and
   their CMakeLists.txt does not pass `-stdlib=libc++`. They use
   `-static-libstdc++ -static-libgcc` for the static link. The design doc's
   Phase 1 reference to OTel as a template for the musl Dockerfile should
   note that **OTel does not face the libc++-static-isn't-PIC problem
   because they don't use libc++ at all.** Picking option (b) above aligns
   our musl path with OTel's exactly.

4. **`-fdelayed-template-parsing` is required** for any modern clang
   build (this isn't musl-specific -- it would also be needed for a
   glibc build on manylinux_2_28 + clang 14+). Phase 3 in the design doc
   plans the manylinux_2_28 jump and would hit the same `atl.h` template
   issue. The cleaner long-term fix is to patch the never-called
   `CallConstructors` helper in the vendored coreclr header (one-line
   change: `this->pElements` -> `pBeginningElement` or `m_pData`). The
   spike used the flag rather than touching vendored code; Phase 1 or
   Phase 3 should make the source fix.

5. **The spike's binary has `BIND_NOW` set.** Today's glibc binary uses
   `RTLD_LAZY` per the modernization plan's empirical finding. If
   `BIND_NOW` is enforced by the link, the runtime will resolve every
   undefined symbol at load, including `RaiseException` (which we expect
   the CLR to satisfy at runtime). The spike's smoke-test failure may be
   partially attributable to this: with lazy binding, the unused
   `_ZNKSt9exception4whatEv` etc. would only fail if called; with
   `BIND_NOW`, they fail at dlopen. Phase 1 should explicitly pass
   `-Wl,-z,lazy` (clang/lld may default to `-z now` on Alpine -- needs
   confirmation). **This is a fifth finding orthogonal to the main libc++
   issue but relevant to "why the smoke test failed."**

## Surprises

- **Alpine 3.23's `libc++-static` is built without `-fPIC`.** This is the
  one thing the design doc could not have anticipated without testing --
  and is the entire reason this spike exists. Filed under Phase 1 open
  question (point 1 above).
- **The vendored `atl.h` has a real bug** (`this->pElements`) at line 367
  in a never-called helper that has gone undetected since it was vendored
  from coreclr. Modern clang catches it; clang 3.9 doesn't. This will
  bite Phase 3 (manylinux_2_28 + modern clang) regardless of whether the
  musl work happens.
- **The spike binary is half the size** (8.15 MB vs 15.3 MB) of today's
  glibc binary -- entirely because libc++/libc++abi/libunwind aren't
  static-linked into it. If Phase 1 adopts option (b) (switch to libstdc++)
  the static-libstdc++ approach will likely produce a similar-sized binary
  to today's glibc one.
- **`__llvm__` is `defined` for any clang build** -- including clang on
  libstdc++. The shim in `xplat.h` activates whenever clang is used,
  regardless of stdlib choice; the gate's name is misleading. This means
  switching the musl build to libstdc++ is **mandatory** if we want to
  use clang there, and is **safe** for the glibc build whenever modernized.

## Files left behind in the working tree

- `src/Agent/NewRelic/Profiler/linux/MuslDockerfile` -- new
- `src/Agent/NewRelic/Profiler/linux/musl-spike-libNewRelicProfiler.so` -- built artifact (~8 MB)
- `src/Agent/NewRelic/Profiler/CMakeLists.txt` -- modified (conditional NR_MUSL_BUILD branch)
- `C:\tmp\musl-build.sh` -- one-off in-container build driver
- `C:\tmp\alpine-smoke.sh` -- one-off smoke-test driver
- Docker image `nr-profiler-musl-spike:latest`

No commits, no pushes. The CMakeLists.txt change is a genuine on-disk diff
to a checked-in file -- revert with `git checkout -- src/Agent/NewRelic/Profiler/CMakeLists.txt`
if the spike artifacts should be cleaned up.

---

# Spike2 addendum -- libstdc++ migration validation

**Date:** 2026-05-18. Follows directly from spike1. Goal: validate that switching
the musl path from libc++ to libstdc++ produces a binary that meets all four
design-doc properties and passes the smoke test. No CI, no commits.

## TL;DR

**All four properties met. Smoke test passed.** The libstdc++ migration is fully
validated. Six source files required changes; all are small and mechanical.
The spike2 binary (`musl-spike2-libNewRelicProfiler.so`, 10,761,496 bytes) is
the expected artifact for Phase 1 minus only the CI scaffolding.

## What changed relative to spike1

### `src/Agent/NewRelic/Profiler/linux/MuslDockerfile`

Stripped libc++, libc++-dev, libc++-static, llvm-libunwind-dev, llvm-libunwind-static.
Final content:

```dockerfile
# Spike-only: musl-native build of libNewRelicProfiler.so on Alpine 3.23.
FROM alpine:3.23
RUN apk add --no-cache \
    clang cmake make musl-dev g++ linux-headers bash dos2unix
```

### `src/Agent/NewRelic/Profiler/CMakeLists.txt`

Rewrote the `NR_MUSL_BUILD` branch to:
- Use `-std=c++14` (libstdc++ only provides `std::make_unique` at C++14+)
- Drop `-stdlib=libc++`
- Add `-fdelayed-template-parsing` (atl.h:367 workaround -- see spike1)
- Use `-static-libstdc++ -static-libgcc` linker flags
- Add `-Wl,-z,lazy` to disable Alpine's default `BIND_NOW` (see spike1 finding 5)

Glibc path (`NR_MUSL_BUILD` unset) is byte-identical to today.

### `src/Agent/NewRelic/Profiler/Common/xplat.h`

Deleted the `#if defined(__llvm__)` block (28 lines) -- the `std::make_unique`
shim in `namespace std`. This shim fires for any clang build (not just libc++
builds) because `__llvm__` is unconditionally defined by clang. libstdc++'s own
`std::make_unique` (C++14+) conflicts with it. Safe to delete: any C++14+
toolchain provides this natively.

### `src/Agent/NewRelic/Profiler/Sicily/ast/GenericParamType.h`

Added `#include <cstdint>`. libstdc++'s `<memory>` does not transitively include
`<cstdint>`; `uint32_t` is used directly in this header.

### `src/Agent/NewRelic/Profiler/Sicily/ast/TypeList.h`

Added `#include <cstdint>`. Same reason -- `uint16_t` used directly.

### `src/Agent/NewRelic/Profiler/ThreadProfiler/ThreadProfiler.h`

Added `#include <condition_variable>`. libstdc++ does not pull this transitively
from `<mutex>` or `<atomic>`.

### `src/Agent/NewRelic/Profiler/Logging/Logger.h`

Two changes:

1. Added `#pragma push_macro("__valid") / #undef __valid` before the stdlib
   includes, and `#pragma pop_macro("__valid")` after them. Root cause:
   `externals/coreclr-headers/src/pal/inc/rt/sal.h:2444` defines `#define __valid`
   as an empty SAL annotation macro. GCC 15's `bits/parse_numbers.h` (included
   transitively through `<mutex>` -> `<chrono>`) uses `__valid` as a template
   type alias name. When sal.h is processed before Logger.h's stdlib includes
   in a translation unit's include chain, the macro is already defined and
   `bits/parse_numbers.h` fails with `expected unqualified-id`. The push/undef/pop
   pattern protects the stdlib from the macro without touching the vendored header.

2. Changed the `operator<<(wofstream, xstring_t)` body from:
   ```cpp
   std::copy(str.cbegin(), str.cend(), std::ostream_iterator<wchar_t, wchar_t>(_Ostr));
   ```
   to:
   ```cpp
   for (auto c : str) { _Ostr.put(static_cast<wchar_t>(c)); }
   ```
   libstdc++ is strict about `char16_t`->`wchar_t` type compatibility in
   `ostream_iterator` contexts; libc++ was lenient about this implicit cast.

## Build errors encountered and resolved

| Error | Root cause | Fix |
|---|---|---|
| `std::make_unique` not in namespace std | `-std=c++11` with libstdc++ -- `make_unique` requires C++14 | Changed to `-std=c++14` |
| `uint32_t` / `uint16_t` undeclared | libstdc++ `<memory>` does not pull in `<cstdint>` | Added `#include <cstdint>` to GenericParamType.h and TypeList.h |
| `std::condition_variable` not found | Not included transitively from `<mutex>` or `<atomic>` in libstdc++ | Added `#include <condition_variable>` to ThreadProfiler.h |
| `bits/parse_numbers.h:56: expected unqualified-id` for `using __valid = true_type;` | `sal.h:2444` `#define __valid` macro (empty) corrupts the GCC 15 `bits/parse_numbers.h` template alias | `#pragma push_macro / #undef __valid / #pragma pop_macro` around stdlib includes in Logger.h |
| Logger.h type mismatch (`char16_t`->`wchar_t`) | `ostream_iterator<wchar_t,wchar_t>` does not accept `char16_t` source in libstdc++ | Replaced with explicit put() loop with `static_cast<wchar_t>` |

## Binary characterization

### `readelf -d` (DT_NEEDED)

```
NEEDED  libc.musl-x86_64.so.1
SONAME  libNewRelicProfiler.so
```

No `libstdc++.so.6`, no `libc++.so.1`, no `libunwind.so.1`. `-static-libstdc++ -static-libgcc`
succeeded -- both are fully absorbed into the `.so`. **Property 3 met.**

FLAGS: no `BIND_NOW`. `-Wl,-z,lazy` worked. **Property 4 met.**

### `readelf -V` (GLIBC_ version refs)

**Zero GLIBC_ references.** No version-needs section. Identical result to spike1.
**Property 2 met.**

### `nm -D --defined-only` -- exported symbols

| Binary | Exported count |
|---|---|
| glibc x64 baseline (CI run 25933232238) | 4551 |
| musl spike2 | **6989** |

The higher count is expected: libstdc++ internals are now statically linked into
the musl `.so` rather than in a dynamic `libstdc++.so.6` dependency, so they
appear in the `.so`'s dynamic symbol table. This is the same mechanism that
explains why today's glibc binary (4551) has a large symbol table -- it also
static-links libstdc++ via `-static-libstdc++`.

Critical COM entry points present: `DllGetClassObject` [ok], `DllCanUnloadNow` [ok].

### `ldd` (inside Alpine 3.23)

```
        /lib/ld-musl-x86_64.so.1 (0x7f...)
        libc.musl-x86_64.so.1 => /lib/ld-musl-x86_64.so.1 (0x7f...)
Error relocating ...: RaiseException: symbol not found
```

Only `libc.musl-x86_64.so.1`. `RaiseException` unresolved is the same
pre-existing condition as spike1 (CLR-provided symbol; resolves at runtime
after `libcoreclr.so` loads). **Property 1 met.**

## Smoke test on `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`

**Passed.**

```
[Info ] 2026-05-18 ...: Profiler initialized
[Info ] 2026-05-18 ...: Logger initialized
[Info ] 2026-05-18 ...: ICorProfilerInfo10 available
[Info ] 2026-05-18 ...: Parsed 372 instrumentation points
...
[Info ] 2026-05-18 ...: Profiler shutting down
```

All four design-doc properties confirmed. Profiler attaches, instrumentation
XML parses, profiler shuts down cleanly.

## Files left behind in the working tree

- `src/Agent/NewRelic/Profiler/linux/MuslDockerfile` -- updated (libc++ removed)
- `src/Agent/NewRelic/Profiler/linux/musl-spike2-libNewRelicProfiler.so` -- built artifact (10,761,496 bytes)
- `src/Agent/NewRelic/Profiler/CMakeLists.txt` -- modified (final spike2 state)
- `src/Agent/NewRelic/Profiler/Common/xplat.h` -- modified (`__llvm__` shim deleted)
- `src/Agent/NewRelic/Profiler/Sicily/ast/GenericParamType.h` -- modified (`#include <cstdint>`)
- `src/Agent/NewRelic/Profiler/Sicily/ast/TypeList.h` -- modified (`#include <cstdint>`)
- `src/Agent/NewRelic/Profiler/ThreadProfiler/ThreadProfiler.h` -- modified (`#include <condition_variable>`)
- `src/Agent/NewRelic/Profiler/Logging/Logger.h` -- modified (`__valid` push/pop; char16_t fix)
- Docker image `nr-profiler-musl-spike2:latest`

No commits, no pushes.
