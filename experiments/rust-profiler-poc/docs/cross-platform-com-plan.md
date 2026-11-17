# Cross-Platform COM Vtable Implementation Plan

## Context

The Rust profiler POC works end-to-end on Windows but the CLR integration layer (`profiler_callback`, metadata interfaces, tokenizer, etc.) is gated behind `#[cfg(windows)]` because it depends on the `com` crate. The CLR on Linux uses the **exact same COM vtable layout** — we just need to implement it without the `com` crate using manual `#[repr(C)]` vtable structs.

## Approach

Replace the `com` crate with hand-rolled vtable structs:
- **Server interfaces** (CLR calls us): Full vtable struct with function pointers for all ~84 methods
- **Client interfaces** (we call CLR): Thin wrappers that call through vtable slot offsets
- `extern "system"` maps to the correct calling convention on both platforms

## Work Items

### WI-1: Foundation types in `ffi.rs`
Define cross-platform `GUID` struct, `HRESULT` type, and standard constants.

### WI-2: Client interface vtables
For each of 7 client interfaces, define `#[repr(C)]` vtable struct + thin wrapper.

### WI-3: Server profiler object + IClassFactory
Replace `class!` macro with manual vtable (84 slots), ClassFactory, ref counting, QueryInterface.

### WI-4: Update `lib.rs` entry points
Remove `#[cfg(windows)]` gates, remove `extern crate com`.

### WI-5: Update `method_resolver.rs` and `tokenizer.rs`
Replace `com::Interface` trait with manual QueryInterface calls.

### WI-6: Remove `com` from `Cargo.toml`

### WI-7: Update tests

### WI-8: Linux Docker integration test
Multi-stage build: Rust profiler `.so` + .NET 8 test app with profiler attached.

## Verification
1. `cargo test` on Windows — 214+ tests pass
2. `cargo test` on Linux (Docker) — all tests pass
3. Runtime test on Windows — 4 methods instrumented
4. Runtime test on Linux (Docker) — profiler attaches, IL injection works
