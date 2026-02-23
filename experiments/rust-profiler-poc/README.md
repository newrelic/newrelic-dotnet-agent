# New Relic .NET Agent Profiler - Rust POC

This is a proof-of-concept rewrite of the New Relic .NET Agent profiler from C++ to Rust.

## Project Goals

### Primary Goal: Maintainability
The existing C++ profiler (~48,000 lines across 16 projects) suffers from:
- Complex design that few engineers can modify confidently
- Memory management issues and potential safety bugs
- Difficulty onboarding new team members
- Legacy code patterns that are hard to extend

### Secondary Goals
- **Musl-based Linux support**: Current C++ build system can't target musl distributions (Alpine Linux, etc.)
- **Modern toolchain**: Move away from outdated C++ build tools
- **Platform expansion**: Full ARM64 support and potential macOS support
- **Build system improvements**: Simpler, more reliable builds

### Non-negotiable Requirements
- **Perfect fidelity**: Byte-for-byte compatibility with C++ version
- **Zero customer impact**: Extremely low risk tolerance
- **Platform support**: Windows x86/x64, Linux x64/ARM64 (glibc + musl distributions)
- **Musl compatibility**: Alpine Linux and other musl-based distributions (current limitation)
- **Performance**: No regression in JIT compilation overhead

## POC Scope

This POC focuses on proving the three most critical technical challenges:

### 1. COM Interop Validation ✓ (Planned)
- Implement `ICorProfilerCallback4` interface in Rust
- Test CLR attachment with both profiler GUIDs:
  - .NET Framework: `{71DA0A04-7777-4EC6-9643-7D28B46A8A41}`
  - .NET Core/.NET: `{36032161-FFC0-4B61-B559-F6C5D41BAE5A}`
- Verify all COM lifecycle methods work correctly

### 2. Basic IL Manipulation ⏳ (Future)
- Simple bytecode injection (e.g., `Console.WriteLine`)
- Prove instruction encoding and offset calculations work
- Validate exception handler table manipulation

### 3. AgentShim Protocol Compatibility ⏳ (Future)
- Implement P/Invoke exports that managed agent can call
- Test parameter marshaling compatibility
- Verify the native/managed boundary works identically

## Repository Structure

```
experiments/rust-profiler-poc/
├── src/
│   ├── lib.rs                    # COM exports and DLL entry point
│   ├── profiler_callback.rs      # ICorProfilerCallback4 implementation
│   └── validation.rs             # Fidelity validation framework
├── tests/
│   └── (integration tests - future)
├── docs/
│   └── (technical documentation)
├── build_targets/
│   └── build-all-targets.ps1     # Cross-platform build script
├── .cargo/
│   └── config.toml              # Cross-compilation configuration
├── Cargo.toml                   # Rust project configuration
└── README.md                    # This file
```

## Building

### Prerequisites

1. **Rust toolchain**: Install from [rustup.rs](https://rustup.rs/)
2. **Cross-compilation targets**:
   ```bash
   rustup target add i686-pc-windows-msvc
   rustup target add x86_64-pc-windows-msvc
   rustup target add x86_64-unknown-linux-gnu
   rustup target add aarch64-unknown-linux-gnu
   # KEY VALUE: musl targets (Alpine Linux support)
   rustup target add x86_64-unknown-linux-musl
   rustup target add aarch64-unknown-linux-musl
   # Stretch goal
   rustup target add aarch64-apple-darwin
   ```

3. **Platform-specific tools**:
   - Windows: Visual Studio Build Tools
   - Linux cross-compilation: GCC cross-compilers (glibc) or musl-cross (musl)
   - macOS cross-compilation: Clang

**Note**: Musl targets (Alpine Linux) use Rust's built-in `rust-lld` linker, requiring no external dependencies.

### Quick Build (Native Platform)

```bash
# Debug build
cargo build

# Release build
cargo build --release
```

### Cross-Platform Build

```powershell
# Build all supported platforms
.\build_targets\build-all-targets.ps1

# Build specific targets (including musl - key value!)
.\build_targets\build-all-targets.ps1 -Targets @("windows-x64", "linux-x64-musl", "linux-arm64-musl")

# Release build
.\build_targets\build-all-targets.ps1 -Release
```

## Testing

### Validation Framework

The POC includes a comprehensive validation framework for comparing behavior between C++ and Rust implementations:

```bash
# Enable validation logging
$env:NEWRELIC_VALIDATION_ENABLED="1"
$env:NEWRELIC_VALIDATION_OUTPUT="./validation_output"

# Run profiler and capture events
cargo run

# Events are logged to JSON files for analysis
```

### Side-by-Side Testing

1. Run the same test application with both profilers
2. Compare captured events using the validation framework
3. Analyze differences to ensure perfect fidelity

## Architecture

### Core Components

1. **COM Interface** (`profiler_callback.rs`):
   - Implements `ICorProfilerCallback4` for CLR integration
   - Handles JIT compilation events
   - Manages profiler lifecycle

2. **Validation Framework** (`validation.rs`):
   - Captures profiler events for comparison
   - Provides side-by-side testing capabilities
   - Generates fidelity reports

3. **Platform Abstraction** (future):
   - Windows vs Unix system calls
   - Registry vs filesystem configuration access

### Key Design Principles

- **Memory Safety**: Leverage Rust's ownership system to prevent memory bugs
- **Thread Safety**: Use Rust's concurrency primitives for lock-free hot paths
- **Error Handling**: Explicit error propagation with Result types
- **Performance**: Zero-cost abstractions, minimal allocations in hot paths

## Current Status

### ✅ Completed
- [x] Project structure and build system
- [x] Basic Cargo configuration for cross-platform builds
- [x] COM interface skeleton (Windows)
- [x] Validation framework foundation
- [x] Cross-platform build script

### 🚧 In Progress
- [ ] Complete `ICorProfilerCallback4` implementation
- [ ] COM registration and CLR attachment testing

### ⏳ Planned
- [ ] Basic IL injection POC
- [ ] AgentShim P/Invoke compatibility
- [ ] Side-by-side validation testing
- [ ] Performance benchmarking
- [ ] Documentation and rollout planning

## Risk Mitigation

Given the extremely low risk tolerance, the POC emphasizes validation:

1. **Comprehensive Testing**: Every change is validated against C++ behavior
2. **Gradual Implementation**: Start with minimal functionality, expand carefully
3. **Side-by-side Deployment**: Run both profilers simultaneously for comparison
4. **Automated Validation**: Continuous testing to detect any behavioral differences
5. **Rollback Strategy**: Easy fallback to C++ implementation if issues arise

## Next Steps

1. **Complete COM Interface**: Implement all required `ICorProfilerCallback4` methods
2. **Test CLR Attachment**: Verify profiler can attach to .NET processes
3. **Basic Event Handling**: Log JIT compilation events and compare with C++
4. **Simple IL Injection**: Prove bytecode manipulation works
5. **Validation Testing**: Establish side-by-side comparison methodology

## Technical Reference

- **C++ Implementation**: `src/Agent/NewRelic/Profiler/Profiler/CorProfilerCallbackImpl.h` (1,421 lines)
- **Method Rewriter**: `src/Agent/NewRelic/Profiler/MethodRewriter/FunctionManipulator.h` (525 lines)
- **AgentShim Interface**: `src/Agent/NewRelic/Agent/Core/AgentShim.cs`
- **Profiler GUIDs**: Must match existing implementation exactly

## Contributing

This is currently an exploratory POC. Once technical feasibility is proven, we'll establish formal contribution guidelines and team processes.

## Questions or Issues

For questions about this POC, contact the .NET Agent team.