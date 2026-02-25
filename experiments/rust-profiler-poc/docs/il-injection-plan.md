# Real IL Injection Implementation Plan

## Context

The Rust profiler POC has proven the full JIT → ReJIT → GetReJITParameters pipeline with an identity rewrite (reads original IL, writes it back unchanged). The next critical milestone is **real IL injection**: modifying method bytecode to inject the try-catch wrapper and AgentShim.GetFinishTracerDelegate call that the C++ profiler uses for instrumentation.

This is the highest-risk, highest-complexity component of the entire POC. The plan is phased to build testable layers from the bottom up, with the pure-Rust IL infrastructure validated by unit tests before any CLR integration.

## Architecture Overview

```
Phase A: Pure IL Infrastructure (no CLR)     Phase B: Metadata (CLR required)
┌─────────────────────────────┐              ┌──────────────────────────┐
│ 1. Opcodes + Sig Compression│              │ 6. IMetaDataEmit2 +      │
│ 2. Instruction Builder      │              │    Assembly Emit/Import  │
│ 3. Method Header Parser     │              │ 7. Tokenizer             │
│ 4. Exception Handler Manip  │              │ 8. Method Signature Parse│
│ 5. Local Variable Signature │              └──────────────────────────┘
└─────────────────────────────┘
              ↓                                         ↓
        Phase C: Integration
        ┌──────────────────────────────────────────────┐
        │ 9. Default Instrumentation Template           │
        │ 10. Wire Into GetReJITParameters             │
        └──────────────────────────────────────────────┘
```

## New Files

All under `experiments/rust-profiler-poc/src/`:

| File | Purpose |
|------|---------|
| `il/mod.rs` | IL module root |
| `il/opcodes.rs` | CIL opcode constants |
| `il/sig_compression.rs` | ECMA-335 compressed integer encoding |
| `il/instruction_builder.rs` | Bytecode builder with jump labels + exception tracking |
| `il/method_header.rs` | Tiny/fat IL header parsing and writing |
| `il/exception_handler.rs` | Exception clause parsing, offset shifting, serialization |
| `il/locals.rs` | Local variable signature construction |
| `il/inject_default.rs` | The default instrumentation IL template |
| `metadata_emit.rs` | IMetaDataEmit2 COM interface |
| `metadata_assembly.rs` | IMetaDataAssemblyEmit + IMetaDataAssemblyImport COM interfaces |
| `tokenizer.rs` | Token resolution wrapping metadata APIs |
| `method_signature.rs` | Minimal method signature parser |

## Modified Files

| File | Changes |
|------|---------|
| `src/lib.rs` | Add new module declarations |
| `src/profiler_callback.rs` | Replace identity rewrite in GetReJITParameters with real injection |
| `src/instrumentation.rs` | Add tracer factory fields to InstrumentationPoint |
| `src/method_resolver.rs` | Add method signature retrieval |
| `src/ffi.rs` | Add metadata open flags, additional constants |
| `test-app/ProfilerTestApp/Program.cs` | Add simpler void/static/no-param test method |

---

## Phase A: Pure IL Infrastructure (No CLR Dependency)

### Work Item 1: Opcode Constants + ECMA-335 Signature Compression ✅

**Files**: `il/mod.rs`, `il/opcodes.rs`, `il/sig_compression.rs`

**Status**: Complete. 30 tests passing.

**`il/opcodes.rs`** — ~40 CIL opcode constants as `pub const u16`. Single-byte opcodes use low byte directly; two-byte opcodes (0xFE prefix) encode as `0xFE00 | second_byte`. Helper functions: `is_two_byte_opcode`, `encode_opcode`, `ldc_i4_opcode_for`.

**`il/sig_compression.rs`** — ECMA-335 compressed integer encoding/decoding:
- `compress_data(value: u32) -> Result<Vec<u8>>` — 0-0x7F → 1 byte; 0x80-0x3FFF → 2 bytes; 0x4000-0x1FFFFFFF → 4 bytes
- `uncompress_data(bytes: &[u8]) -> Result<(u32, usize)>` — Returns (value, bytes_consumed)
- `compress_token(token: u32) -> Result<Vec<u8>>` — TypeDefOrRefOrSpecEncoded
- `uncompress_token(bytes: &[u8]) -> Result<(u32, usize)>`

---

### Work Item 2: IL Instruction Builder ✅

**File**: `il/instruction_builder.rs`

**Status**: Complete. 25 tests passing.

Rust equivalent of `InstructionSet.h` (~800 lines). Core byte-vector builder with:
- Opcode encoding (1-byte and 2-byte 0xFE-prefixed)
- Little-endian operand encoding (u8, u16, u32, u64)
- Jump label system with forward-reference patching
- Optimized ldarg/ldloc/stloc (0-3 inline, short, long forms)
- Optimized ldc.i4 (-1..8 inline, short, long)
- User code insertion with offset tracking
- Exception clause boundary tracking (try/catch start/end)

---

### Work Item 3: IL Method Header Parser/Writer

**File**: `il/method_header.rs`

Parse and construct IL method headers:

**Tiny header**: 1 byte, `bits[1:0]=0x2`, `bits[7:2]=code_size` (max 63 bytes)
**Fat header**: 12 bytes — flags u16, max_stack u16, code_size u32 LE, local_var_sig_tok u32 LE

Key operations:
- `parse_method(bytes) → (header, code_bytes, extra_section_bytes)`
- `tiny_to_fat(code_size) → FatHeader` — Sets InitLocals, max_stack=8
- `write_fat_header(header) → [u8; 12]`

**Reference**: `FunctionManipulator.h` lines 134-194.

---

### Work Item 4: Exception Handler Manipulator

**File**: `il/exception_handler.rs`

Parse existing exception clauses, shift offsets, build new extra section bytes:

- Parse fat clauses (24 bytes: flags, tryOffset, tryLength, handlerOffset, handlerLength, classToken — all u32 LE)
- Parse small clauses (12 bytes: flags u16, tryOffset u16, tryLength u8, handlerOffset u16, handlerLength u8, classToken u32)
- `shift_offsets(user_code_offset)` on original clauses
- Build extra section: header byte `0x41` (EHTable|Fat), 24-bit LE size, then serialized clauses

**Reference**: `ExceptionHandlerManipulator.h` lines 147-277.

---

### Work Item 5: Local Variable Signature Builder

**File**: `il/locals.rs`

Build/modify LOCAL_SIG blobs (ECMA-335 II.23.2.6):
- Format: `0x07` (LOCAL_SIG), compressed count, type signatures...
- `new()` → `[0x07, 0x00]` (empty)
- `from_existing(bytes)` → wrap existing blob
- `append_type(type_bytes) → local_index` — Append type, increment compressed count
- `append_object_type(class_token) → local_index` — Append `[0x12, compressed_token]`

**Reference**: `FunctionManipulator.h` lines 196-470.

---

## Phase B: Metadata COM Interfaces and Tokenizer

### Work Item 6: IMetaDataEmit2 + Assembly Emit/Import COM Interfaces

**Files**: `metadata_emit.rs`, `metadata_assembly.rs`

Define COM interfaces for writing metadata (same pattern as existing `metadata_import.rs`):

**IMetaDataEmit2** (inherits IMetaDataEmit → IUnknown):
- ~40 vtable slots total (all must be present for correct layout)
- Critical methods: `DefineTypeRefByName`, `DefineMemberRef`, `DefineUserString`, `GetTokenFromTypeSpec`, `GetTokenFromSig`
- IMetaDataEmit2 adds: `DefineMethodSpec`

**IMetaDataAssemblyImport** — `EnumAssemblyRefs`, `GetAssemblyRefProps`, `CloseEnum`
**IMetaDataAssemblyEmit** — `DefineAssemblyRef`

---

### Work Item 7: Tokenizer Module

**File**: `tokenizer.rs`

Wraps metadata COM interfaces to resolve tokens. Rust equivalent of `CorTokenizer.h` (323 lines):

Key methods:
- `get_assembly_ref_token(name)` — Enumerate existing refs, return match. CoreCLR: map "mscorlib" → "System.Runtime"
- `get_type_ref_token(assembly, type_name)` — CoreCLR: resolve assembly via hard-coded type→assembly map, then `DefineTypeRefByName`
- `get_member_ref_token(parent, method_name, signature)` — `DefineMemberRef`
- `get_string_token(string)` — `DefineUserString`
- `get_token_from_type_spec(signature)` — `GetTokenFromTypeSpec`
- `get_token_from_signature(sig_bytes)` — `GetTokenFromSig` (for local vars)

**CoreCLR type→assembly map**:
```
System.Object → System.Runtime
System.Exception → System.Runtime
System.Type → System.Runtime
System.UInt32 → System.Runtime
System.UInt64 → System.Runtime
System.Reflection.MethodBase → System.Reflection
System.Action`2 → System.Core (or System.Runtime on newer)
```

---

### Work Item 8: Method Signature Parser (Minimal)

**File**: `method_signature.rs`

Parse blob signature to determine: has_this, is_generic, param_count, return_type_is_void.

```rust
pub struct MethodSignature {
    pub has_this: bool,        // bit 0x20 in calling convention
    pub is_generic: bool,      // bit 0x10
    pub generic_param_count: u32,
    pub param_count: u32,
    pub return_type_is_void: bool,  // 0x01 = VOID
    pub raw_signature: Vec<u8>,
}
```

---

## Phase C: IL Injection Integration

### Work Item 9: Default Instrumentation Template

**File**: `il/inject_default.rs`

The core function:
```rust
pub fn build_instrumented_method(
    ctx: &DefaultInstrumentationContext,
    tokenizer: &Tokenizer,
    original_il: &[u8],
) -> Result<Vec<u8>, IlError>
```

Assembles the full instrumented method:
1. Parse original header (tiny→fat) and extract code + exception clauses
2. Build extended local variable signature (add tracer, exception, optionally return value)
3. Build instruction sequence with try-catch wrapper pattern:
   - Initialize locals to null
   - SafeCallGetTracer (try-catch around 11-element object[] + AgentShim call)
   - User code wrapped in try-catch (catch stores exception, calls finish tracer, rethrows)
   - After-catch: call finish tracer with return value
   - Load return value (if non-void), ret
4. Resolve all metadata tokens via tokenizer
5. Combine fat header + instructions + padded extra section

**Start simple**: void static method, no parameters.

---

### Work Item 10: Wire Into GetReJITParameters

**Files**: `profiler_callback.rs`, `method_resolver.rs`, `instrumentation.rs`, `ffi.rs`

Replace the identity rewrite with:
1. Get original IL (existing)
2. Get method signature via `IMetaDataImport::GetMethodProps`
3. Get metadata emit interfaces via `ICorProfilerInfo::GetModuleMetaData`
4. Construct Tokenizer
5. Parse method signature
6. Call `build_instrumented_method`
7. Write via `SetILFunctionBody`

---

## Execution Order

```
Parallel batch 1: WI-1 ✅ (opcodes/compression), WI-3 (header parser), WI-6 (COM interfaces)
Parallel batch 2: WI-2 ✅ (instruction builder), WI-5 (locals), WI-8 (sig parser)
Parallel batch 3: WI-4 (exception handler), WI-7 (tokenizer)
Sequential:       WI-9 (injection template) → WI-10 (wire up)
```

## Verification

1. **Unit tests** (Phase A): Every IL infrastructure component has byte-level tests matching C++ test outputs
2. **`cargo test`**: All existing 46 tests + new tests pass
3. **Integration test** (Phase C): Run ProfilerTestApp with `run-with-profiler.ps1`, verify:
   - Profiler logs show "IL injection successful" for matched methods
   - App executes normally (no crashes)
   - Instrumented methods still produce correct output
4. **IL dump comparison** (stretch goal): Add env-var-triggered IL dump to both profilers, binary diff

## Risk Mitigation

- **Highest risk** (WI-9): IL template generates incorrect bytes → CLR crash. Mitigation: extensive unit tests before any CLR integration.
- **High risk** (WI-6): Incorrect vtable ordering → CLR crash. Mitigation: verify against official headers, test simple calls first.
- **Medium risk** (WI-7): CoreCLR assembly resolution edge cases. Mitigation: log all token resolutions, test with real CLR early.
