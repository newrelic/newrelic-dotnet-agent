# Agent Bootstrap Sequence Implementation Plan

## Context

The Rust profiler POC has a structurally complete IL injection pipeline — ret rewriting, EH clause merging, TypeSpec resolution, and finish-tracer calls all work. But `SafeCallGetTracer` is a no-op (`ldnull; stloc tracer`), so the tracer is always null and finish-tracer calls are always skipped via `brfalse`. This is the last piece to make the instrumentation pipeline structurally complete.

The C++ profiler's CoreCLR path uses direct reflection (no `CannotUnloadAppDomainException` injection needed). We implement the same approach: load agent assembly, resolve method, invoke via `MethodBase.Invoke`.

## Files to Modify

| File | Changes |
|------|---------|
| `src/il/inject_default.rs` | Add bootstrap tokens/fields, signature builders, implement full SafeCallGetTracer body, update tests |
| `src/profiler_callback.rs` | Read `CORECLR_NEWRELIC_HOME` env var, add `agent_core_path` to InstrumentationContext |

No changes to `tokenizer.rs` — `System.Reflection.Assembly` and `System.Reflection.MethodInfo` are already in the CoreCLR type map (lines 61-62).

## Work Items

### WI-1: Add fields to InjectionTokens and InstrumentationContext

**InjectionTokens** — 7 new fields:
- `assembly_type_ref` — TypeRef for `System.Reflection.Assembly`
- `assembly_load_from_ref` — MemberRef for `Assembly.LoadFrom(string)` (static)
- `assembly_get_type_ref` — MemberRef for `Assembly.GetType(string)` (instance)
- `type_get_method_ref` — MemberRef for `Type.GetMethod(string)` (instance)
- `agent_core_path_token` — string token for DLL path
- `agent_shim_class_token` — string token for `"NewRelic.Agent.Core.AgentShim"`
- `get_finish_tracer_method_token` — string token for `"GetFinishTracerDelegate"`

**InstrumentationContext** — 1 new field:
- `agent_core_path: String` — full path to `NewRelic.Agent.Core.dll`

### WI-2: Add 3 signature builder functions

Following the existing pattern (e.g., `build_get_type_from_handle_sig`):

- `build_assembly_load_from_sig(assembly_type_ref)` — `static Assembly LoadFrom(string)`: DEFAULT, 1 param, return CLASS Assembly, param STRING (0x0E)
- `build_assembly_get_type_sig(type_type_ref)` — `instance Type GetType(string)`: HASTHIS, 1 param, return CLASS Type, param STRING
- `build_type_get_method_sig(method_info_type_ref)` — `instance MethodInfo GetMethod(string)`: HASTHIS, 1 param, return CLASS MethodInfo, param STRING

### WI-3: Resolve tokens in `resolve_injection_tokens`

After existing resolution, add:
1. Resolve `System.Reflection.Assembly` and `System.Reflection.MethodInfo` TypeRefs
2. Build + resolve 3 MemberRefs (LoadFrom, GetType, GetMethod)
3. Resolve 3 string tokens (DLL path, class name, method name)
4. Add all 7 to returned `InjectionTokens`

### WI-4: Implement full `build_safe_call_get_tracer`

Replace no-op + commented scaffolding with the real CoreCLR bootstrap IL:

```
// Bootstrap: resolve MethodInfo via reflection
ldstr agent_core_path_token           // "path/to/NewRelic.Agent.Core.dll"
call assembly_load_from_ref           // Assembly.LoadFrom(string) → Assembly
ldstr agent_shim_class_token          // "NewRelic.Agent.Core.AgentShim"
callvirt assembly_get_type_ref        // Assembly.GetType(string) → Type
ldstr get_finish_tracer_method_token  // "GetFinishTracerDelegate"
callvirt type_get_method_ref          // Type.GetMethod(string) → MethodInfo

// Build 11-element object[] and invoke
ldnull; ldc.i4.s 11; newarr Object
[0] tracerFactoryName (ldstr)
[1] tracerFactoryArgs (ldc.i4 + box uint32)
[2..7] strings (ldstr)
[4] type (ldtoken + GetTypeFromHandle)
[8] this (ldarg.0 or ldnull)
[9] parameters (empty object[])
[10] functionId (ldc.i8 + box uint64)
callvirt MethodBase.Invoke            // → object (Action delegate)
stloc tracer
```

The try-catch wrapper is already in place — no structural changes needed.

### WI-5: Read agent home path in `try_inject_il`

In `profiler_callback.rs`, read `CORECLR_NEWRELIC_HOME` env var. Build the DLL path using `std::path::Path::join` (uses `MAIN_SEPARATOR` — backslash on Windows, forward slash on Linux). Add to `InstrumentationContext`.

Cross-platform note: `Assembly.LoadFrom(string)` in .NET handles both path separator styles on all platforms, so the embedded `ldstr` path will work regardless of OS.

### WI-6: Update tests

- Update `test_tokens()` with 7 new fake token values
- Update `test_ctx_*()` with `agent_core_path` field
- Un-ignore all 7 remaining tests
- Fix ldstr count assertion: 6 → 9 (6 param strings + 3 bootstrap strings)
- Add 3 signature builder unit tests
- Add 1 bootstrap integration test (validate bootstrap IL structure)

## Verification

1. `cargo test` — all 210+ tests pass, **0 ignored**
2. Runtime: `dotnet run --project test-app/ProfilerTestApp -c Release --no-build` with profiler
   - Bootstrap calls `Assembly.LoadFrom` which throws `FileNotFoundException` (no agent DLL in test app)
   - SafeCallGetTracer catch swallows it, tracer stays null
   - App runs correctly (same behavior as today, but with full IL structure)
   - All 4 methods MATCH and inject with larger IL sizes (~400+ bytes)
3. JIT validates the full IL structure including bootstrap tokens — if tokens are wrong, we'd get `TypeLoadException` at JIT time, not a silent failure
