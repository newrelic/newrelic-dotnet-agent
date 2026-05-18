# Profiler unit tests — Linux portability analysis

**Date:** 2026-05-18. Status: investigation only, no code changes. Decision:
**deferred** — not on the critical path for the dual-build (Phases 1-3 of
`PROFILER_DUAL_BUILD_DESIGN.md`).

This document preserves the findings so the team can revisit if/when the
value calculus changes (e.g., Phase 3 toolchain modernization surfaces
unexplained stdlib-divergence behavior that integration tests can't
pinpoint).

## TL;DR

Today's profiler unit tests run on Windows only. The blocker is the test
framework choice (Microsoft `CppUnitTestFramework`, proprietary to Visual
Studio), not the source under test (which is already portable — we
compiled it cleanly on Linux during the musl spike2 work).

A full port is **3-5 days of mechanical work + 1-2 weeks if done with
test-count parity** (no silent skips of platform-divergent cases). The
port doesn't unblock any phase of the dual-build; it adds belt-and-suspenders
coverage in an area (stdlib-divergence between libc++ and libstdc++) where
integration tests are already the load-bearing gate.

## Current state

**5 test projects, ~6,145 lines, ~370 test methods (counts as of 2026-05-18):**

| Project | TEST_METHODs | Coverage |
|---|---|---|
| CommonTest | 19 | StringsTest, FileUtilsTest, VersionTest |
| ConfigurationTest | 153 | XML parsing, instrumentation rules |
| LoggingTest | 49 | Logger init, env-var-driven log locations |
| MethodRewriterTest | 127 | IL rewriting, instruction sets, exception handlers |
| SignatureParserTest | 21 | Method signature parsing |

**Build system:** `.vcxproj` files only, with `Win32`/`x64` configurations.
`CMakeLists.txt` does not build tests at all — the Linux build only
produces the `.so` and the `corhlpr.cpp` translation unit from the vendored
coreclr headers.

**Test framework:** Microsoft `CppUnitTestFramework`
(`Microsoft::VisualStudio::CppUnitTestFramework` namespace, `<CppUnitTest.h>`).
Proprietary to Visual Studio; not available on Linux.

**Assertion API surface (small — mappable to any modern framework):**

| Used today | gtest equivalent | Catch2 equivalent |
|---|---|---|
| `Assert::IsTrue` | `EXPECT_TRUE` / `ASSERT_TRUE` | `REQUIRE(x)` |
| `Assert::IsFalse` | `EXPECT_FALSE` | `REQUIRE_FALSE(x)` |
| `Assert::AreEqual` | `EXPECT_EQ` | `REQUIRE(a == b)` |
| `Assert::IsNull`, `IsNotNull` | `EXPECT_EQ(_, nullptr)` | `REQUIRE(x == nullptr)` |
| `Assert::Fail` | `FAIL()` | `FAIL("...")` |
| `Assert::ExpectException<T>` | `EXPECT_THROW(_, T)` | `REQUIRE_THROWS_AS(_, T)` |
| `TEST_CLASS(name) { TEST_METHOD(m) }` | `TEST_F(name, m)` | `TEST_CASE("name.m")` |

**Source-under-test portability:** confirmed during spike2. The same
files in `Common/`, `Configuration/`, `Logging/`, `MethodRewriter/`,
`SignatureParser/`, `Sicily/` compile cleanly into a musl-native `.so`
on Alpine 3.23. The tests target portable code; the porting issue is
in the test code itself, not the production code.

## What's involved (four layers)

### Layer 1 — Replace the test framework (~1 day, mechanical)

Swap `CppUnitTestFramework` for **gtest** or **Catch2**. A short shim
header could keep the existing `TEST_CLASS`/`TEST_METHOD`/`Assert::*`
syntax intact (define them as macros expanding to the new framework's
constructs), making the diff in test bodies almost zero.

### Layer 2 — Strip Windows-only includes (~1 hour)

`ConfigurationTest/stdafx.h` and `LoggingTest/stdafx.h` `#include
<windows.h>` and `<SDKDDKVer.h>` as boilerplate inherited from the VS
template. Test bodies do not actually call Windows APIs. Remove the
includes.

### Layer 3 — String-literal portability (real work, ~few days)

The bulk of the effort. Test bodies have **~1,300+ `L""` / `std::wstring`
references** that bypass the project's `xstring_t` / `_X(...)` / `W(...)`
abstractions. On Windows these compile because `wchar_t == WCHAR` and
the public API takes `wchar_t*`; on Linux the same API takes `char16_t*`
(via the `xstring_t` typedef = `std::u16string`).

Distribution of the references:

| Project | `L""` / `wstring` count |
|---|---|
| ConfigurationTest/ConfigurationTest.cpp | 222 |
| ConfigurationTest/InstrumentationConfigurationTest.cpp | 96 |
| LoggingTest/DefaultFileLogLocationTest.cpp | 97 |
| LoggingTest/LoggerTest.cpp | 95 |
| ConfigurationTest/InstrumentationPointTest.cpp | 42 |
| ConfigurationTest/ShouldInstrumentTest.cpp | 43 |
| MethodRewriterTest/ExceptionHandlerManipulatorTest.cpp | 30 |
| (and ~15 other files with smaller counts) | ~700 combined |

Conversion plan:
- ~80% sed-able: `L"foo"` → `_X("foo")`, `std::wstring` → `xstring_t`.
- ~20% requires judgment:
  - Tests that assert against literal Windows paths (e.g.,
    `L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe"` in `StringsTest`) need
    either `#ifdef _WIN32`-gated variants or duplicated Linux versions
    with POSIX paths.
  - `FileUtilsTest` reads/writes files using `wofstream` with UTF-16 BOM
    handling and `codecvt_utf8_utf16`. The codecvt path is
    stdlib-implementation-dependent; libc++ and libstdc++ diverge subtly.
    Some test bodies may need rework to stay green across both.
  - Env-variable tests (`DefaultFileLogLocationTest`) reference
    `NEW_RELIC_HOME`, `NEW_RELIC_PROFILER_LOG_DIRECTORY`, etc. These
    work on both platforms but the test setup uses Windows
    `SetEnvironmentVariable` semantics in a few places.

### Layer 4 — Build & CI infrastructure (~1 day)

- Extend `CMakeLists.txt` with 5 `add_executable()` targets, one per
  test project, linked against the same source files the `.so` builds
  from.
- Pull in gtest via `FetchContent` or vendor it under `externals/`.
- Wire into CTest so `ctest --output-on-failure` runs them.
- Add a CI step in `build_profiler.yml` that runs tests inside the
  build container after the `.so` build succeeds — both glibc and musl
  variants, both x64 and arm64 (4 test runs per CI invocation when the
  dual-build is fully wired).
- Windows side keeps `.vcxproj` for now (it's part of FullAgent.sln);
  this work is purely additive on the Linux side.

## Subtle issues to flag upfront if/when the port lands

1. **Test count parity.** A port that ships with ~30% of cases
   `#ifdef`'d out is worse than no port — it creates the illusion of
   coverage. Either commit to porting all tests (with platform-conditional
   variants where needed) or explicitly enumerate the skipped set with
   rationale.

2. **`_NEVER_`-gated code.** `LoggingTest/DefaultFileLogLocationTest.cpp`
   has `#ifdef _NEVER_` blocks — tests that someone disabled on Windows.
   The Linux port should not silently re-enable them; treat
   `_NEVER_`-gated tests as deliberately disabled.

3. **`MethodRewriterTest` complexity.** Largest project (127 methods),
   exercises IL rewriting deeply. Mocks (`MockSystemCalls.h`,
   `MockTokenizer.h`, `MockFunction.h`, `MockFunctionHeaderInfo.h`) may
   carry Windows-specific assumptions not visible in a quick scan. Worth
   a closer look before committing to porting this project specifically.

4. **Source-side `xstring_t` consistency.** Spike2 confirmed the source
   compiles to a Linux `.so`, but did not run the unit tests against it.
   Latent `wstring`/`u16string` mismatches that the `.so` build doesn't
   trip but the tests would, would surface during the port.

5. **`MockSystemCalls.h` differences.** The `ConfigurationTest` and
   `MethodRewriterTest` both define their own `MockSystemCalls.h` —
   they're separate copies. A port might be a good time to consolidate,
   or might surface that they're deliberately divergent.

## Why deferring is the right call right now

The dual-build's verification gates already cover the load-bearing risks:

- **Build-time inspection** (`readelf -d` / `-V`, `nm -D`) catches ABI
  and symbol-level issues.
- **Smoke test** (already passed in spike2) catches "does the .so load
  and parse instrumentation?"
- **`ContainerIntegrationTests`** + the proposed `AlpineX64` /
  `AlpineArm64` fixtures cover the end-to-end functional path against
  real instrumented apps.

What Linux unit tests would *uniquely* add: stdlib-divergence catch in
specific code paths (`FileUtilsTest` UTF-16 `codecvt`, `StringsTest`
conversions) that integration tests don't typically exercise. This
risk is theoretical, not measured. If Phase 3 (toolchain modernization
on the glibc path, also dropping `-stdlib=libc++`) surfaces unexplained
behavioral drift between stdlibs that integration tests can't pinpoint,
that's the time to revisit this analysis.

## Estimates if/when the team decides to do it

- **Layer 1+2 spike on `CommonTest`** (smallest project, 19 tests) to
  validate the gtest+CMake pipeline before committing to the full port:
  **half a day**.
- **All 5 test projects, mechanical port (Layers 1+2 + sed-able cases
  in Layer 3)**: **3-5 days**.
- **All 5 test projects, including platform-divergent cases handled
  cleanly (Layer 3 hard cases + Layer 4 + CI integration)**: **1-2 weeks
  of focused work**.

## Where to start when revisiting

1. Re-read this document. Verify the test count and `L""`/`wstring`
   reference counts haven't drifted significantly (rerun the same
   `grep -c` queries against the current tree).
2. Pick `CommonTest` for the Layer 1+2 spike — smallest, simplest,
   exercises only `Strings.h`, `FileUtils.h`, `Version.h` from the
   production code.
3. Vendor or `FetchContent`-pull gtest under `externals/gtest/`. Add
   a `CommonTest` target in `CMakeLists.txt` gated on a new
   `NR_BUILD_TESTS` cmake option (default `OFF`).
4. Do the framework swap (TEST_CLASS/TEST_METHOD/Assert::* → gtest)
   either via shim macros or full rewrite — whichever team prefers.
5. Convert `L""` → `_X("")` and `std::wstring` → `xstring_t` in the
   `CommonTest` bodies. Verify it builds and runs in the existing
   `MuslDockerfile` container.
6. If that proof-of-concept lands cleanly, scope the full port using
   the per-project line/method counts in this document.
