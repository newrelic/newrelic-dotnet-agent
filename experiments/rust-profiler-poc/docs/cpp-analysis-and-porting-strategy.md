# C++ Implementation Analysis & Porting Strategy

## C++ Implementation Analysis

Based on analysis of the existing C++ profiler, here are the key components we need to port:

### Core COM Interface (`CorProfilerCallbackImpl.h`)

The main class `CorProfilerCallbackImpl : public ICorProfilerCallback4` implements:

#### Critical Methods (Must Implement)
1. **COM Interface Methods:**
   - `QueryInterface()` - COM interface querying
   - `AddRef()` / `Release()` - Reference counting

2. **Profiler Lifecycle:**
   - `Initialize(IUnknown* pICorProfilerInfoUnk)` - Called when profiler attaches
   - `InitializeForAttach()` - For attach scenarios (can return S_OK)

3. **Core Events:**
   - `JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)` - Primary instrumentation trigger
   - `ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)` - Module loading complete
   - `ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock)` - ReJIT events

#### Additional Callback Methods (34+ total)
The C++ implementation has numerous other callback methods that mostly return `S_OK`. For POC, we can stub these out.

### Key Dependencies Identified

#### 1. Configuration System
```cpp
#include "../Configuration/Configuration.h"
#include "../Configuration/InstrumentationConfiguration.h"
```
- Loads configuration from multiple sources (env vars, XML files)
- Manages instrumentation rules and patterns
- **Rust Port**: Use our existing validation framework structure

#### 2. Method Rewriter System
```cpp
#include "../MethodRewriter/MethodRewriter.h"
#include "../MethodRewriter/CustomInstrumentation.h"
```
- Core IL bytecode manipulation
- **Rust Port**: This is the most complex component (~525 lines in FunctionManipulator.h)

#### 3. Function Resolution
```cpp
#include "Function.h"
#include "FunctionResolver.h"
```
- Resolves method metadata from FunctionID
- **Rust Port**: Use CLR metadata APIs through windows-rs

#### 4. Logging System
```cpp
#include "../Logging/Logger.h"
```
- **Rust Port**: POC uses `log` + `env_logger` (stderr only). This is **NOT sufficient** for production — see [docs/logging-requirements.md](logging-requirements.md) for full analysis. The C++ logger writes to per-process files (`NewRelic.Profiler.<PID>.log`), supports a directory fallback hierarchy, and produces output in a specific format that integration tests match against with regex.

### Platform-Specific Code
```cpp
#ifdef PAL_STDCPP_COMPAT
#include "UnixSystemCalls.h"
#else
#include "SystemCalls.h"  // Windows-specific
#endif
```
- **Rust Port**: Platform abstraction already planned in our architecture

## AgentShim Interface Analysis

The critical P/Invoke method that injected bytecode calls:

```csharp
public static Action<object, Exception> GetFinishTracerDelegate(
    string tracerFactoryName,      // From instrumentation XML
    uint tracerArguments,          // Tracer arguments (boxed)
    string metricName,             // Metric name pattern
    string assemblyName,           // Target assembly name
    Type type,                     // Type object (via GetTypeFromHandle)
    string typeName,               // String representation of type
    string methodName,             // Method name
    string argumentSignature,      // Method signature as string
    object invocationTarget,       // 'this' reference or null for static
    object[] args,                 // Method arguments as object array
    ulong functionId               // CLR FunctionID (boxed)
);
```

**Critical Requirement**: The generated IL bytecode must call this method with exactly these parameters and types. Any deviation will cause runtime failures.

## Porting Priority Matrix

### Phase 1: Minimal Viable Profiler (POC)

| Component | Priority | Complexity | Risk | Notes |
|-----------|----------|------------|------|-------|
| COM Interface Skeleton | ✅ High | Low | Low | Already started |
| Basic Event Handling | ✅ High | Low | Low | Log events for validation |
| Configuration Loading | 🔶 Medium | Medium | Medium | Stub for POC, full implementation later |
| Method Resolution | 🔶 Medium | Medium | High | Need for any IL injection |
| Logging System | 🔶 Medium | Medium | High | Must match C++ format/naming before integration testing — see [logging-requirements.md](logging-requirements.md) |

### Phase 2: Basic IL Injection

| Component | Priority | Complexity | Risk | Notes |
|-----------|----------|------------|------|-------|
| Simple IL Generation | High | High | High | Most critical technical risk |
| Method Rewriter Core | High | Very High | Very High | ~525 lines of complex C++ |
| Exception Handling | Medium | High | High | Exception table manipulation |
| AgentShim Calls | High | Medium | Very High | Perfect P/Invoke compatibility required |

### Phase 3: Full Functionality

| Component | Priority | Complexity | Risk | Notes |
|-----------|----------|------------|------|-------|
| All Callback Methods | Medium | Medium | Low | Many just return S_OK |
| Advanced Configuration | Medium | Medium | Medium | XML parsing, file watching |
| Performance Optimization | High | High | Medium | Hot path optimization |
| Cross-Platform Polish | Low | Medium | Low | Linux/macOS specific code |

### Future: ICorProfilerCallback Version Upgrade

The C++ profiler implements `ICorProfilerCallback4` (.NET Framework 4.5+). The CLR negotiates
the callback version via `QueryInterface` — it probes for the highest version the profiler
supports and falls back gracefully. Implementing a higher callback version does NOT break
attachment to older CLRs; they simply never ask for the newer interface.

This means the Rust profiler can safely upgrade to a higher callback version in the future:

| Callback | Added In | Notable Methods |
|----------|----------|-----------------|
| 5 | .NET Fx 4.5.2 | `ConditionalWeakTableElementReferences` |
| 6 | .NET Fx 4.6 | `GetAssemblyReferences` |
| 7 | .NET Fx 4.6.1 | `ModuleInMemorySymbolsUpdated` |
| 8 | .NET Core 2.1 | `DynamicMethodJITCompilationStarted/Finished` |
| 9 | .NET Core 2.1 | `DynamicMethodUnloaded` |
| 10 | .NET 5 | EventPipe integration |
| 11 | .NET 8 | `LoadAsNotificationOnly` |

**POC decision**: Match C++ at Callback4 for fidelity validation. Upgrading to Callback9+
is a concrete capability improvement the Rust profiler could deliver over C++ — particularly
`DynamicMethodJITCompilation` support for .NET Core dynamic methods.

Note: The callback interface version is independent from the `ICorProfilerInfo` version.
The C++ profiler already queries for `ICorProfilerInfo5` (for `SetEventMask2`) and probes
up to `ICorProfilerInfo11` for feature detection, while only implementing Callback4.

## Technical Implementation Plan

### 1. Complete COM Interface (Next Session)

**File**: `src/profiler_callback.rs`

**TODO Items:**
```rust
impl ICorProfilerCallback4 for CorProfilerCallbackImpl {
    // Phase 1: Critical methods
    fn Initialize(&self, profiler_info_unk: &IUnknown) -> HRESULT { /* TODO */ }
    fn JITCompilationStarted(&self, function_id: FunctionID, is_safe_to_block: BOOL) -> HRESULT { /* TODO */ }
    fn ModuleLoadFinished(&self, module_id: ModuleID, hr_status: HRESULT) -> HRESULT { /* TODO */ }

    // Phase 2: ReJIT support
    fn ReJITCompilationStarted(&self, function_id: FunctionID, rejit_id: ReJITID, is_safe_to_block: BOOL) -> HRESULT { /* TODO */ }

    // Phase 3: All other methods (30+ methods that mostly return S_OK)
    // ... implement remaining callback methods
}
```

### 2. Method Resolution System

**File**: `src/method_resolver.rs` (new file needed)

**Key Functions:**
```rust
struct MethodResolver {
    profiler_info: ICorProfilerInfo4,
}

impl MethodResolver {
    fn get_function_info(&self, function_id: FunctionID) -> Result<FunctionInfo, HRESULT> {
        // Call GetFunctionInfo2 to get method metadata
        // Parse method signature and type information
        // Return structured function information
    }

    fn should_instrument_method(&self, function_info: &FunctionInfo) -> bool {
        // Check against instrumentation configuration
        // Match assembly name, class name, method name patterns
        // Apply ignore lists and filtering rules
    }
}
```

### 3. Simple IL Injection (Proof of Concept)

**File**: `src/il_injector.rs` (new file needed)

**Initial Goal**: Inject a simple `Console.WriteLine("Hello from Rust profiler!");` call at the beginning of instrumented methods.

```rust
struct SimpleILInjector {
    // For POC: just inject a simple method call
}

impl SimpleILInjector {
    fn inject_hello_world(&self, original_il: &[u8]) -> Result<Vec<u8>, String> {
        // 1. Parse original IL header
        // 2. Add space for new instructions
        // 3. Inject Console.WriteLine call at method start
        // 4. Adjust branch targets and exception handlers
        // 5. Return modified IL bytecode
    }
}
```

## Validation Strategy

Validation is organized into three layers, from fastest/most precise to slowest/most realistic. Each layer catches different classes of bugs.

### Layer 1: Unit-Level IL Generation Tests (No CLR Required)

This is the **most valuable** layer because IL generation is fully deterministic — given the same inputs (original IL bytes, method signature, instrumentation config), the C++ profiler always produces the same output bytes. No timestamps, no randomness. This means we can test the Rust IL generator as a pure function.

**Approach:**
1. Capture reference inputs and outputs from the C++ profiler:
   - Use the existing `WRITE_BYTES_TO_DISK` preprocessor flag (in `Function.h` line 375) to dump instrumented IL to `.bin` files
   - Alternatively, extract test cases from the C++ unit tests (`MethodRewriterTest/`)
   - Record: original IL bytes, method signature, instrumentation point config → expected output IL bytes
2. Write Rust unit tests that take the same inputs and assert byte-for-byte identical output
3. Cover all method shapes:
   - Tiny header methods (≤64 bytes, no locals, no exception handlers)
   - Fat header methods (locals, exception handlers)
   - Void vs. non-void return types
   - Static vs. instance methods
   - Methods with existing try-catch blocks (exception handler table adjustment)
   - Various parameter counts (affects the 11-element object array construction)

**What this catches:** Opcode encoding errors, wrong operand sizes, incorrect exception handler offsets, stack size miscalculation, local variable signature encoding bugs.

**What this doesn't catch:** Metadata token resolution differences (tokens come from the CLR, not from the IL generator).

**Current C++ test coverage to build on:**
- `InstructionSetTest.cpp` — byte-level opcode encoding (little-endian, operand sizes)
- `ExceptionHandlerManipulatorTest.cpp` — exception clause parsing and offset shifting
- `FunctionManipulatorTest.cpp` — method wrapping (mostly commented out, but provides structure)
- `MockFunction.h` / `RealisticTokenizer` — test doubles for method metadata

### Layer 2: IL Dump Comparison (With CLR)

Run the same .NET application with both profilers and compare the actual IL they inject.

**Approach:**
1. Enable `WRITE_BYTES_TO_DISK` in the C++ profiler (or add a runtime-configurable equivalent via environment variable)
2. Implement the same dump capability in the Rust profiler
3. Run a test application that exercises known instrumentation points
4. Binary diff the dumped IL files: `TypeName.MethodName.bin`

**What this catches:** Metadata token resolution differences (the C++ profiler resolves tokens via `ITokenizer` at instrumentation time — if the Rust profiler resolves different tokens for the same type references, the IL will diverge even if the instruction sequences are structurally correct).

**Implementation notes:**
- The C++ `WRITE_BYTES_TO_DISK` flag is compile-time only. Consider adding a runtime env var (e.g., `NEW_RELIC_PROFILER_DUMP_IL=1`) to both profilers so this can be used without recompilation.
- File naming must match (`TypeName.MethodName.bin`) for automated diffing.

### Layer 3: Integration Test Validation (Runtime Behavior)

Run the existing integration test suite with the Rust profiler and verify identical telemetry output.

**Approach:**
1. Swap the profiler DLL path (`COR_PROFILER_PATH` / `CORECLR_PROFILER_PATH`) to point at the Rust profiler
2. Run integration tests from `tests/Agent/IntegrationTests/`
3. Compare: metrics, transaction traces, span events, error events, log output

**What this catches:** End-to-end behavioral differences — wrong AgentShim parameters, incorrect parameter marshaling, missing or extra instrumentation, performance regressions.

**What this requires:** The Rust profiler must be far enough along to actually attach to the CLR and inject IL. This is a later-stage validation.

**Profiler log compatibility note:** Integration tests also inspect `NewRelic.Profiler.*.log` files for specific messages. See [logging-requirements.md](logging-requirements.md) for details.

### Validation Tooling Needed

| Tool | Purpose | When Needed |
|------|---------|-------------|
| IL reference corpus | Captured C++ outputs for known inputs | Before starting IL generation in Rust |
| Binary diff script | Automated comparison of `.bin` dumps | Layer 2 testing |
| IL disassembler view | Human-readable dump for debugging failures | When byte-for-byte diffs fail (use `ildasm` or dnSpy) |
| Runtime IL dump flag | Env var to trigger IL dump in both profilers | Layer 2 testing |

### Key Architectural Insight: The Injection is Templated

The C++ profiler's IL injection follows a **fixed template** with parameterized slots (from `InstrumentFunctionManipulator.h`):

```
1. Initialize locals (tracer=null, exception=null)
2. Try { GetFinishTracerDelegate via reflection with 11-param array } Catch { pop }
3. Try { [ORIGINAL METHOD BYTES shifted by userCodeOffset] } Catch(Exception) { store exception, call finish tracer, rethrow }
4. If tracer != null: Try { callvirt Action<object,Exception>.Invoke(result, null) } Catch { pop }
5. Load result (if non-void), ret
```

The variable parts are:
- Metadata tokens (resolved from assembly/type/method names)
- The 11 parameters in the object array (tracer factory name, metric name, assembly name, type token, etc.)
- Local variable types (return type determines local #2)
- Original method bytes and their exception handler offsets

This template structure means we can validate the Rust implementation piece by piece — get the template right first with a simple void/no-args method, then progressively test more complex signatures.

## Risk Mitigation Strategies

### 1. Incremental Validation
- Port the IL generation as a pure function first (no CLR dependency)
- Validate against captured C++ reference outputs before ever attaching to the CLR
- Start with the simplest method shape (void, no args, no locals, tiny header) and work up
- Never move to next component until current one is proven

### 2. Fallback Strategy
- Environment variable to switch between C++ and Rust profilers
- Automatic detection of issues and fallback to C++
- Gradual rollout to minimize customer impact

### 3. Performance Monitoring
- Continuous benchmarking against C++ version
- Alert on any performance regression >5%
- Memory usage monitoring

## Next Session Goals (If Rust Gets Installed)

### Immediate Tasks
1. **Test Cross-Platform Compilation**:
   ```powershell
   .\build_targets\build-all-targets.ps1 -Targets @("linux-x64-musl", "linux-arm64-musl")
   ```
   - Verify musl compilation works (immediate value demonstration - Alpine Linux support)
   - Test all target platforms including glibc and musl variants
   - Measure build times vs C++

2. **Implement Core COM Methods**:
   - Complete `Initialize()` method
   - Add proper error handling
   - Test CLR attachment with simple .NET app

3. **Basic Event Logging**:
   - Implement `JITCompilationStarted()` with validation logging
   - Compare event sequences with C++ profiler
   - Establish baseline fidelity metrics

### Success Criteria for POC Completion
- ✅ Cross-platform compilation (especially ARM64)
- ✅ CLR attachment works without crashing
- ✅ Basic profiler events received and logged
- ✅ Side-by-side validation framework operational
- ✅ Fidelity score >95% for basic event sequences

## Conclusion

The POC is well-positioned to prove technical feasibility. The C++ implementation analysis shows:

**✅ Manageable Scope**: Core functionality is concentrated in a few key methods
**✅ Clear Architecture**: Well-defined interfaces and responsibilities
**✅ Validation Strategy**: Comprehensive testing approach for zero-risk deployment
**⚠️ High Complexity**: IL manipulation and AgentShim protocol are technically challenging
**⚠️ Perfect Fidelity Requirement**: Any deviation from C++ behavior could cause customer issues

**Recommendation**: Install Rust and proceed with Phase 1 implementation to validate the technical approach before requesting broader organizational resources.