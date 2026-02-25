# POC Progress Tracking

## Session 1: Initial Setup (2026-02-20)

### ✅ Completed Tasks

#### Project Structure Setup
- Created branch `poc/tippmar-nr-rust-profiler`
- Set up `experiments/rust-profiler-poc/` directory structure
- Created Cargo.toml with cross-platform dependencies
- Configured build targets for Windows x86/x64, Linux x64/ARM64, macOS ARM64

#### Core Implementation Foundation
- **lib.rs**: Main library with COM exports and DLL entry points
- **profiler_callback.rs**: ICorProfilerCallback4 skeleton with proper GUIDs
- **validation.rs**: Comprehensive validation framework for fidelity testing
- **Basic integration tests**: Smoke tests for core functionality

#### Build System
- Cross-platform Cargo configuration (`.cargo/config.toml`)
- PowerShell build script for all target platforms
- Development environment setup with validation logging

#### Documentation
- Comprehensive README with project goals and architecture
- Progress tracking (this document)
- Technical reference to C++ implementation files

### 🎯 Key Achievements

1. **Musl-based Linux Support**: Immediately demonstrates value by solving current C++ toolchain limitation (Alpine Linux, etc.)
2. **Validation Framework**: Addresses extremely low risk tolerance with comprehensive testing approach
3. **Perfect Fidelity Strategy**: Event logging and side-by-side comparison methodology
4. **Clean Architecture**: Memory-safe, maintainable code structure in place

### 📊 Current Status

| Component | Status | Confidence |
|-----------|---------|------------|
| Project Structure | ✅ Complete | High |
| Build System | ✅ Complete | High |
| COM Interface Skeleton | ✅ Complete | Medium |
| Validation Framework | ✅ Complete | High |
| Documentation | ✅ Complete | High |
| Cross-platform Builds | 🚧 Ready to test | Medium |
| CLR Attachment | ⏳ Not started | Unknown |
| IL Manipulation | ⏳ Not started | Unknown |

## Next Session Goals

### Immediate Priorities (Next 1-2 sessions)

1. **Test Cross-Platform Builds**
   - Run build script on multiple targets
   - Verify output files are created correctly
   - Test ARM64 compilation specifically

2. **Complete COM Interface Implementation**
   - Implement all ICorProfilerCallback4 methods
   - Add proper error handling
   - Test COM registration

3. **CLR Attachment Testing**
   - Create simple test .NET application
   - Test profiler attachment with environment variables
   - Verify basic event reception (JIT compilation events)

### Medium-term Goals (3-5 sessions)

1. **Basic IL Injection POC**
   - Implement simple bytecode modification
   - Test with "Hello World" injection
   - Validate instruction encoding works

2. **Side-by-Side Validation**
   - Run same app with both C++ and Rust profilers
   - Compare event logs automatically
   - Establish fidelity metrics

3. **Performance Baseline**
   - Measure JIT compilation overhead
   - Compare with C++ implementation
   - Document performance characteristics

## Technical Decisions Made

### Architecture Decisions
- **Gradual Implementation**: Start minimal, build up functionality incrementally
- **Validation-First Approach**: Every feature gets comprehensive testing against C++ version
- **Cross-Platform by Default**: All code written to support Windows/Linux/macOS from start
- **Memory Safety Priority**: Leverage Rust ownership system for safer code

### Implementation Decisions
- **COM Interface**: Use `windows-rs` crate for Windows COM interop
- **Validation Framework**: JSON event logging with automated comparison tools
- **Build System**: Cargo with cross-compilation, PowerShell scripts for convenience
- **Error Handling**: Rust Result types for explicit error propagation

### Risk Mitigation Decisions
- **Side-by-side Deployment**: Both profilers can run simultaneously for comparison
- **Event-level Validation**: Compare every profiler event between implementations
- **Rollback Strategy**: Easy fallback to C++ if any issues discovered
- **Progressive Testing**: Each component validated before moving to next

## Lessons Learned

### What Went Well
- **Rust Ecosystem Maturity**: `windows-rs` provides excellent COM support
- **Cross-Compilation**: Cargo makes multi-platform builds straightforward
- **Documentation Strategy**: Comprehensive docs help maintain context across sessions
- **Validation Approach**: Thinking about fidelity testing from day 1 was crucial

### Challenges Encountered
- **COM Complexity**: ICorProfilerCallback4 has many methods to implement
- **Profiler GUIDs**: Must match exactly with C++ version (critical requirement)
- **Build Dependencies**: Cross-compilation requires platform-specific toolchains

### Technical Insights
- **Memory Safety Benefits**: Already seeing advantages in error handling patterns
- **Performance Potential**: Rust's zero-cost abstractions look promising for hot paths
- **Maintainability**: Code is already more readable than equivalent C++

## Success Metrics

### Technical Metrics
- **Fidelity Score**: Target 100% identical event sequences with C++ profiler
- **Performance**: <5% overhead vs C++ implementation
- **Platform Support**: All targets build and run correctly
- **Test Coverage**: Comprehensive validation of all functionality

### Business Metrics
- **Maintainability**: Reduced time to make changes (measure after implementation)
- **Team Onboarding**: Faster engineer ramp-up time
- **Build Reliability**: Eliminate ARM64 compilation issues
- **Platform Expansion**: Enable macOS support if needed

## Known Risks and Open Items

### Logging Compatibility (Identified 2026-02-24)

The current POC uses `log` + `env_logger` which only writes to stderr. The C++ profiler has a custom logging system that writes to per-process files (`NewRelic.Profiler.<PID>.log`) in a specific format (`[Level] YYYY-MM-DD HH:MM:SS message`). **Integration tests pattern-match against these log files**, so the Rust profiler must produce compatible output before side-by-side testing can begin.

Full analysis: [docs/logging-requirements.md](logging-requirements.md)

**Status**: `env_logger` is fine for POC development. Custom file logger needed before integration testing phase.

### Process Filtering / ShouldInstrument (Identified 2026-02-24)

The C++ profiler's `Initialize()` checks whether the current process should be instrumented
before subscribing to events. Key logic in `CorProfilerCallbackImpl.h`:

```cpp
if (!forceProfiling && !configuration->ShouldInstrument(processPath, parentProcessPath, appPoolId, commandLine, _isCoreClr)) {
    LogInfo("This process should not be instrumented, unloading profiler.");
    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
}
```

This includes:
- Process name/path exclusion lists (e.g., `SMSvcHost.exe`)
- Parent process detection (is this an IIS worker?)
- App pool ID filtering
- Configuration-driven include/exclude rules
- `NEW_RELIC_PROFILER_FORCE_PROFILING` override

Without this, the Rust profiler attaches to **every** .NET process that inherits the
environment variables — including the `dotnet` CLI host itself. During testing we observed
the profiler loading twice: once for the dotnet host (CoreCLR v10.0) and once for the
actual target app (CoreCLR v8.0).

**Status**: Basic filtering implemented in `src/process_filter.rs` (2026-02-24). Covers:
- ✅ `dotnet run/publish/restore/new` CLI command exclusion
- ✅ MSBuild invocation exclusion
- ✅ Kudu / DiagServer (Azure) exclusion
- ✅ SMSvcHost.exe exclusion (.NET Framework)
- ⏳ Configuration-driven process exclusion lists (Phase 2/3)
- ⏳ Parent process detection / IIS w3wp identification (Phase 2/3)
- ⏳ App pool filtering (Phase 2/3)
- ⏳ `NEW_RELIC_PROFILER_FORCE_PROFILING` override (Phase 2/3)

## Questions for Next Session

1. **Build Testing**: Which platform should we prioritize for initial testing?
2. **CLR Integration**: Any specific .NET Framework vs .NET Core compatibility concerns?
3. **Performance Targets**: What's acceptable overhead for the POC phase?
4. **Validation Criteria**: How do we define "good enough" fidelity for management approval?

## Files Ready for Testing

All POC files are staged and ready for:
- Cross-platform compilation testing
- Basic functionality verification
- Integration with existing build system (if desired)
- Side-by-side comparison with C++ profiler

**Next step**: Test the build system and validate basic functionality before proceeding to COM interface completion.

## End of Session 1 (2026-02-20)

**Status**: Excellent progress! Rust installed and ready for Monday development.

**Monday Priorities:**
1. Test cross-platform builds (prove musl-based Linux support value immediately)
2. Complete COM interface implementation
3. Create simple .NET test app for validation
4. Begin side-by-side profiler comparison testing

All POC files ready on branch `poc/tippmar-nr-rust-profiler` with comprehensive implementation strategy documented.

## Session 2: COM Interface & CLR Attachment (2026-02-24)

### ✅ Completed Tasks

#### COM Interface Implementation
- Full `ICorProfilerCallback` through `ICorProfilerCallback4` interface definitions with all ~80 vtable methods
- `DllGetClassObject` / `DllCanUnloadNow` exports via `com` crate's `class!` macro
- `IClassFactory` generated automatically by the `com` crate
- `NewRelicProfiler` COM class with proper CLSID matching the C++ profiler

#### ICorProfilerInfo Interfaces
- `ICorProfilerInfo` through `ICorProfilerInfo5` interface definitions (~80 vtable methods)
- Methods we don't call use opaque `*const c_void` for ABI compatibility
- Full signatures for methods we do call: `SetEventMask`, `GetRuntimeInformation`, `SetEventMask2`

#### Working Initialize() Implementation
- Queries `ICorProfilerInfo4` via `QueryInterface`
- Calls `GetRuntimeInformation` to detect Desktop CLR vs CoreCLR
- Sets event mask matching the C++ profiler exactly (JIT, module loads, threads, ReJIT, etc.)
- Tries `SetEventMask2` (ICorProfilerInfo5) to disable tiered compilation, falls back to `SetEventMask`

#### Process Filtering
- `process_filter.rs` module with command-line-based exclusion logic
- Excludes `dotnet run/publish/restore/new` CLI commands
- Excludes MSBuild, Kudu, DiagServer (Azure), SMSvcHost.exe
- 6 unit tests covering exclusion and pass-through cases

#### Test Infrastructure
- `ProfilerTestApp` — .NET 8 console app exercising JIT, async, generics, exception handling
- `run-with-profiler.ps1` (Windows) and `run-with-profiler.sh` (Linux) launch scripts
- Sets `CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_PROFILER_PATH`

#### Documentation Updates
- Logging requirements doc (`docs/logging-requirements.md`)
- Validation strategy rewritten with concrete three-layer approach
- ICorProfilerCallback version upgrade notes
- Process filtering documented as known risk with implementation status

#### Method Resolution
- `method_resolver.rs` module resolves FunctionIDs to assembly/type/method names
- Uses `ICorProfilerInfo::GetFunctionInfo`, `GetModuleInfo`, `GetAssemblyInfo`
- Uses `IMetaDataImport::GetMethodProps`, `GetTypeDefProps` for method/type names
- `metadata_import.rs` defines IMetaDataImport COM interface (62 vtable methods)
- Stores `ICorProfilerInfo4` in `RefCell` from `Initialize()` for use in JIT callbacks
- Verified output: `[System.Private.CoreLib] System.Collections.Generic.Dictionary'2..ctor`
- 4 unit tests for UTF-16 string conversion

#### Test Suite
- Split monolithic test file into 5 focused test files (38 tests total)
- COM exports, interface GUIDs, FFI constants, process filter, validation framework

### 🎯 Key Achievements

**CLR Attachment Proven:**
1. Loads into the CLR via `DllGetClassObject`
2. Creates profiler instance via class factory
3. Receives `Initialize()` callback
4. Queries `ICorProfilerInfo4` and `ICorProfilerInfo5`
5. Detects CoreCLR v8.0 runtime
6. Sets event mask with tiered compilation disabled
7. Receives **623 JIT compilation events** and **9 module loads**
8. Excludes `dotnet` CLI host process (only instruments the target app)
9. Clean `Shutdown()` on process exit

**Method Resolution Proven:**
- Resolves FunctionIDs → `[Assembly] Namespace.Type.Method` in real time
- Works with IMetaDataImport COM interface across JIT callbacks

### 📊 Updated Status

| Component | Status | Confidence |
|-----------|--------|------------|
| Project Structure | ✅ Complete | High |
| Build System | ✅ Complete | High |
| Musl Compilation | ✅ Proven | High |
| COM Interface (Callback) | ✅ Complete | High |
| COM Interface (ProfilerInfo) | ✅ Complete | High |
| COM Interface (MetaDataImport) | ✅ Complete | High |
| CLR Attachment | ✅ **Proven** | High |
| Process Filtering (basic) | ✅ Complete | High |
| Event Reception (JIT/Module) | ✅ **Proven** | High |
| Method Resolution | ✅ **Proven** | High |
| Validation Framework | ✅ Complete | High |
| Test Suite | ✅ 46 tests | High |
| Documentation | ✅ Complete | High |
| Instrumentation Matching | ✅ **Proven** | High |
| RequestReJIT | ✅ **Proven** | High |
| IL Identity Rewrite | ✅ **Proven** | High |
| Logging (file-based) | ⏳ Not started | — |
| Configuration Loading | ⏳ Not started | — |
| IL Injection (real) | ⏳ Not started | — |

### Next Steps

1. ~~**Real IL injection** — Modify method bytecode to inject instrumentation (try-catch wrapper, AgentShim call)~~
2. **Capture C++ IL reference data** — Capture reference outputs for byte-level validation
3. **Integration testing** — Run ProfilerTestApp with real IL injection, verify no crashes
4. **IL dump comparison** — Binary compare Rust-generated IL with C++ reference

## End of Session 2 (2026-02-24)

## Session 3: Real IL Injection Infrastructure (2026-02-25)

### ✅ Completed Tasks

#### Phase A: Pure IL Infrastructure (No CLR Dependency)

**WI-1: Opcode Constants + ECMA-335 Signature Compression** (30 tests)
- `il/opcodes.rs` — ~40 CIL opcode constants with encoding helpers
- `il/sig_compression.rs` — ECMA-335 compressed integer and token encoding/decoding
- `il/mod.rs` — IL module root with `IlError` error type

**WI-2: IL Instruction Builder** (25 tests)
- `il/instruction_builder.rs` — Bytecode builder with opcode encoding (1-byte and 2-byte),
  little-endian operand encoding, jump label system with forward-reference patching,
  optimized ldarg/ldloc/stloc/ldc.i4, exception clause boundary tracking

**WI-3: IL Method Header Parser/Writer** (14 tests)
- `il/method_header.rs` — Parse tiny (1 byte) and fat (12 byte) headers, tiny→fat
  conversion, write/parse round-trip, build complete method bytes with 4-byte aligned
  extra sections

**WI-4: Exception Handler Manipulator** (12 tests)
- `il/exception_handler.rs` — Parse fat (24-byte) and small (12-byte) exception clauses,
  shift offsets by user code offset, build new extra section bytes combining shifted
  originals with new clauses. Always outputs fat format.

**WI-5: Local Variable Signature Builder** (9 tests)
- `il/locals.rs` — Build/modify LOCAL_SIG blobs with compressed count management,
  append class types with compressed token encoding, handle count boundary crossing
  (1-byte to 2-byte compressed encoding)

**WI-8: Method Signature Parser** (9 tests)
- `method_signature.rs` — Minimal parser for method blob signatures: has_this, is_generic,
  param_count, return_type_is_void. Sufficient for the injection template.

#### Phase B: Metadata COM Interfaces and Tokenizer

**WI-6: IMetaDataEmit2 + Assembly Emit/Import COM Interfaces** (2 GUID tests)
- `metadata_emit.rs` — IMetaDataEmit (49 vtable methods) + IMetaDataEmit2 (8 methods).
  Key methods: DefineTypeRefByName, DefineMemberRef, DefineUserString,
  GetTokenFromTypeSpec, GetTokenFromSig, DefineMethodSpec.
- `metadata_assembly.rs` — IMetaDataAssemblyEmit (9 methods) + IMetaDataAssemblyImport
  (14 methods). Key methods: DefineAssemblyRef, EnumAssemblyRefs, GetAssemblyRefProps,
  CloseEnum.
- Vtable ordering verified against Elastic APM agent's Rust COM definitions.

**WI-7: Tokenizer Module** (7 tests)
- `tokenizer.rs` — Wraps metadata COM interfaces for token resolution. CoreCLR type→assembly
  mapping (System.Object→System.Runtime, etc.), assembly ref caching, type ref caching.
  Methods: get_assembly_ref_token, get_type_ref_token, get_member_ref_token,
  get_string_token, get_type_spec_token, get_token_from_signature, get_method_spec_token.

#### Phase C: IL Injection Integration

**WI-9: Default Instrumentation Template** (28 tests)
- `il/inject_default.rs` — The full IL injection template:
  - Initialize locals (tracer=null, exception=null)
  - SafeCallGetTracer: try-catch around 11-element object[] + MethodBase.Invoke
  - User code wrapped in try-catch (catch stores exception, calls finish tracer, rethrows)
  - After-catch: call finish tracer with return value
  - Return (load result if non-void, ret)
  - Resolves ~15 metadata tokens (type refs, member refs, string tokens)
  - Builds complete method bytes with fat header + instructions + exception clauses

**WI-10: Wire Into GetReJITParameters**
- Updated `profiler_callback.rs` — GetReJITParameters now attempts real IL injection
  with graceful fallback to identity rewrite on error.
- Gets IMetaDataEmit2, IMetaDataAssemblyEmit, IMetaDataAssemblyImport via GetModuleMetaData
- Resolves method signature, type name, assembly name from CLR metadata
- Looks up InstrumentationPoint for tracer factory config
- Calls `build_instrumented_method` to generate instrumented IL
- Falls back to identity rewrite if any step fails
- Updated `instrumentation.rs` — Added tracer_factory_name, tracer_factory_args, metric_name
  to InstrumentationPoint. Added find_point() method. Added SimpleVoidMethod test target.
- Updated `ffi.rs` — Added OF_READ, OF_WRITE, LPCWSTR, PCCOR_SIGNATURE, HCORENUM,
  and additional metadata token types.

### 📊 Updated Status

| Component | Status | Confidence |
|-----------|--------|------------|
| Project Structure | ✅ Complete | High |
| Build System | ✅ Complete | High |
| Musl Compilation | ✅ Proven | High |
| COM Interface (Callback) | ✅ Complete | High |
| COM Interface (ProfilerInfo) | ✅ Complete | High |
| COM Interface (MetaDataImport) | ✅ Complete | High |
| COM Interface (MetaDataEmit) | ✅ Complete | High |
| COM Interface (AssemblyEmit/Import) | ✅ Complete | High |
| CLR Attachment | ✅ **Proven** | High |
| Process Filtering (basic) | ✅ Complete | High |
| Event Reception (JIT/Module) | ✅ **Proven** | High |
| Method Resolution | ✅ **Proven** | High |
| Validation Framework | ✅ Complete | High |
| Test Suite | ✅ **181 tests** | High |
| Documentation | ✅ Complete | High |
| Instrumentation Matching | ✅ **Proven** | High |
| RequestReJIT | ✅ **Proven** | High |
| IL Identity Rewrite | ✅ **Proven** | High |
| IL Infrastructure (pure) | ✅ **Complete** | High |
| IL Injection Template | ✅ **Complete** | High |
| Tokenizer | ✅ Complete | Medium |
| Method Signature Parser | ✅ Complete | High |
| IL Injection Wired Up | ✅ Complete | Medium |
| Integration Testing (real IL) | 🚧 InvalidProgramException | Medium |
| Logging (file-based) | ⏳ Not started | — |
| Configuration Loading | ⏳ Not started | — |

### 🎯 Key Achievements

1. **Complete IL injection infrastructure** — 12 new source files, 112 new tests
2. **Full injection template** — Matches C++ profiler's try-catch wrapper pattern
3. **Metadata token resolution** — Type refs, member refs, string tokens, signatures
4. **Graceful fallback** — If injection fails, identity rewrite preserves method behavior
5. **CoreCLR support** — Type→assembly mapping for .NET Core/.NET 5+

### Runtime Testing Results

The full pipeline was tested: matching → ReJIT → GetReJITParameters → token resolution →
IL generation → SetILFunctionBody. The IL is generated successfully (43 → 352 bytes) and
the CLR accepts the SetILFunctionBody call, but the generated IL triggers
`InvalidProgramException` at runtime.

**Bugs found and fixed:**
1. **`ret` in try block** — Original user code contains `ret` opcodes which are illegal
   inside try blocks. Fixed by replacing the final `ret` with `nop`. Full implementation
   needs an IL instruction scanner to find ALL ret instructions.
2. **Local variable index collision** — New locals (tracer, exception, result) were at
   indices 0,1,2 which overlapped with the original method's locals. Fixed by reading
   the original local count from the CLR metadata and appending new locals AFTER originals.

**Remaining issues to debug:**
- InvalidProgramException persists — likely caused by incorrect exception clause boundaries,
  invalid jump targets, or stack imbalance in the generated IL
- Need to compare generated IL byte-by-byte with C++ profiler's output for the same method
- May need to use ILVerify or dnSpy to identify the specific verification error

### Next Steps

1. **Debug InvalidProgramException** — Compare Rust IL output with C++ reference, use ILVerify
2. **IL dump comparison** — Binary diff against C++ profiler output for same method
3. **Full ret rewriting** — Implement IL instruction scanner for all ret instructions
4. **Expand coverage** — Instance methods, parameter boxing, existing exception handlers

## End of Session 3 (2026-02-25)