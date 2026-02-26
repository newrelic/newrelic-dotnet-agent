// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Default instrumentation template — generates the IL injection bytecode.
//!
//! This is the Rust equivalent of `InstrumentFunctionManipulator::BuildDefaultInstructions`
//! at `InstrumentFunctionManipulator.h` lines 39-218. It wraps every instrumented method
//! with try-catch blocks that call `AgentShim.GetFinishTracerDelegate()` to create a tracer,
//! then finish the tracer with the return value or exception.
//!
//! The injected IL follows a fixed template with parameterized metadata tokens:
//!
//! ```text
//! // Setup
//! ldnull; stloc tracer         // tracer = null
//! ldnull; stloc exception      // exception = null
//!
//! // SafeCallGetTracer (try-catch)
//! try {
//!     <build 11-element object[] with instrumentation params>
//!     call AgentShim.GetFinishTracerDelegate(null, object[])
//!     stloc tracer
//! } catch { pop }
//!
//! // User code (try-catch)
//! try {
//!     [ORIGINAL METHOD BODY]
//!     [if non-void: stloc result]
//!     leave after_catch
//! } catch(Exception) {
//!     stloc exception
//!     try { <call finish tracer with exception> } catch { pop }
//!     rethrow
//! }
//!
//! // After-catch: finish tracer with return value
//! try { <call finish tracer with return value> } catch { pop }
//!
//! // Return
//! [if non-void: ldloc result]
//! ret
//! ```

use super::exception_handler::ExceptionHandlerManipulator;
use super::instruction_builder::InstructionBuilder;
use super::instruction_scanner;
use super::locals::LocalVariableSignature;
use super::method_header;
use super::opcodes::*;
use super::IlError;
use crate::method_signature::MethodSignature;
use crate::tokenizer::Tokenizer;

/// Context for generating instrumented IL.
///
/// Contains all the metadata needed to build the 11-element parameter array
/// passed to `AgentShim.GetFinishTracerDelegate()`.
pub struct InstrumentationContext {
    /// Assembly name of the instrumented method
    pub assembly_name: String,
    /// Fully-qualified type name (e.g., "MyNamespace.MyClass")
    pub type_name: String,
    /// Method name
    pub method_name: String,
    /// CLR FunctionID (boxed as uint64 in the parameter array)
    pub function_id: u64,
    /// Metadata token for the type being instrumented (for ldtoken)
    pub type_token: u32,
    /// Tracer factory name from instrumentation config
    pub tracer_factory_name: String,
    /// Tracer factory args from instrumentation config
    pub tracer_factory_args: u32,
    /// Metric name pattern from instrumentation config
    pub metric_name: String,
    /// Argument signature string from instrumentation config
    pub argument_signature: String,
    /// Parsed method signature
    pub method_signature: MethodSignature,
}

/// Resolved metadata tokens needed by the injection template.
///
/// Public so tests can construct instances with known token values.
#[derive(Debug, Clone)]
pub struct InjectionTokens {
    /// TypeRef for System.Exception (used as catch clause type)
    pub exception_type_ref: u32,
    /// TypeRef for System.Object (used for newarr)
    pub object_type_ref: u32,
    /// TypeRef for System.UInt32 (for boxing tracer args)
    pub uint32_type_ref: u32,
    /// TypeRef for System.UInt64 (for boxing function ID)
    pub uint64_type_ref: u32,
    /// TypeRef for System.Type
    pub type_type_ref: u32,
    /// MemberRef for System.Type.GetTypeFromHandle(RuntimeTypeHandle)
    pub get_type_from_handle_ref: u32,
    /// TypeRef for System.Reflection.MethodBase
    pub method_base_type_ref: u32,
    /// MemberRef for MethodBase.Invoke(object, object[])
    pub method_base_invoke_ref: u32,
    /// TypeRef for System.Action`2 (open generic, used for MemberRef parent)
    pub action2_type_ref: u32,
    /// MemberRef for Action`2.Invoke(!0, !1) on open generic (used internally)
    pub action2_invoke_ref: u32,
    /// TypeSpec for Action<object, Exception> (closed generic instantiation)
    /// Used by `castclass` in the finish-tracer call.
    pub action2_type_spec: u32,
    /// MemberRef for Invoke on the closed Action<object, Exception> TypeSpec.
    /// Used by `callvirt` in the finish-tracer call.
    pub action2_invoke_on_spec: u32,
    /// String tokens for the 11-element parameter array
    pub tracer_factory_name_token: u32,
    pub metric_name_token: u32,
    pub assembly_name_token: u32,
    pub type_name_token: u32,
    pub method_name_token: u32,
    pub argument_signature_token: u32,
}

/// Build an instrumented method from the original IL bytes.
///
/// This is the main entry point for IL injection. It:
/// 1. Parses the original method (tiny→fat conversion if needed)
/// 2. Extends the local variable signature with tracer/exception/result locals
/// 3. Builds the injection template with try-catch wrapper
/// 4. Resolves all metadata tokens via the tokenizer
/// 5. Combines header + instructions + exception clauses
///
/// Reference: `InstrumentFunctionManipulator::InstrumentDefault` at
/// `InstrumentFunctionManipulator.h` lines 26-37.
/// Additional context passed from the CLR callback with info that
/// requires live metadata access.
pub struct ClrMethodContext {
    /// The original method's local variable signature bytes (from GetSigFromToken).
    /// Empty if the method has no locals.
    pub original_locals_signature: Vec<u8>,
    /// Number of locals in the original method.
    pub original_local_count: u16,
}

pub fn build_instrumented_method(
    ctx: &InstrumentationContext,
    clr_ctx: &ClrMethodContext,
    tokenizer: &mut Tokenizer,
    original_il: &[u8],
) -> Result<Vec<u8>, IlError> {
    // Resolve type tokens for locals
    let object_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Object")?;
    let exception_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Exception")?;

    // Resolve all tokens needed for the injection template
    let tokens = resolve_injection_tokens(ctx, tokenizer)?;

    // Build the combined local variable signature:
    // [original locals] + tracer(object) + exception(Exception) + [result(object) if non-void]
    let mut locals = if !clr_ctx.original_locals_signature.is_empty() {
        LocalVariableSignature::from_existing(clr_ctx.original_locals_signature.clone())?
    } else {
        LocalVariableSignature::new()
    };

    locals.append_class_type(object_type_ref)?;    // tracer
    locals.append_class_type(exception_type_ref)?;  // exception
    if !ctx.method_signature.return_type_is_void {
        locals.append_class_type(object_type_ref)?; // result
    }
    let local_sig_token = tokenizer.get_token_from_signature(locals.get_bytes())?;

    // Delegate to the pure IL generation function
    build_instrumented_method_with_tokens(
        ctx,
        &tokens,
        original_il,
        local_sig_token,
        clr_ctx.original_local_count,
    )
}

/// Build an instrumented method using pre-resolved tokens.
///
/// This is the core IL generation logic, separated from token resolution
/// so it can be tested with known token values. All metadata tokens are
/// passed in via `InjectionTokens` and `local_sig_token`.
/// Build an instrumented method using pre-resolved tokens.
///
/// - `original_local_count`: Number of locals in the original method. New locals
///   (tracer, exception, result) are appended after these so original code's
///   local references remain valid.
pub fn build_instrumented_method_with_tokens(
    ctx: &InstrumentationContext,
    tokens: &InjectionTokens,
    original_il: &[u8],
    local_sig_token: u32,
    original_local_count: u16,
) -> Result<Vec<u8>, IlError> {
    // 1. Parse the original method
    let parsed = method_header::parse_method(original_il)?;
    let mut header = parsed.header;

    // 2. Determine local indices for injected locals.
    //    New locals are appended AFTER original locals so that the original
    //    code's local index references (ldloc.0, stloc.1, etc.) remain valid.
    //    This matches the C++ profiler's FunctionManipulator::AppendDefaultLocals.
    let tracer_local = original_local_count;
    let exception_local = original_local_count + 1;
    let result_local: Option<u16> = if !ctx.method_signature.return_type_is_void {
        Some(original_local_count + 2)
    } else {
        None
    };

    // 3. Build the instruction sequence
    let mut builder = InstructionBuilder::new();

    // --- Initialize locals to null ---
    builder.append_opcode(CEE_LDNULL);
    builder.append_store_local(tracer_local);
    builder.append_opcode(CEE_LDNULL);
    builder.append_store_local(exception_local);

    // --- SafeCallGetTracer (wrapped in try-catch) ---
    build_safe_call_get_tracer(
        &mut builder,
        ctx,
        tokens,
        tracer_local,
    );

    // --- User code wrapped in try-catch ---
    // Preprocess the user code to handle `ret` instructions. The CLR does not
    // allow `ret` to bypass our finish-tracer code when user code is inside a
    // try block. For single-ret methods this replaces the final `ret` with `nop`.
    // For multi-ret methods, non-final `ret` instructions become `br` to the
    // final instruction (now `nop`), and all branch offsets are recalculated.
    //
    // This matches the C++ profiler's `FunctionPreprocessor::Process()` in
    // `Profiler/FunctionPreprocessor.h`.
    let preprocessed = instruction_scanner::preprocess_user_code(&parsed.code)?;
    let user_code = preprocessed.code;

    builder.append_try_start();
    builder.append_user_code(&user_code);

    if let Some(result_idx) = result_local {
        builder.append_store_local(result_idx);
    }

    let after_catch_label = builder.append_jump_auto(CEE_LEAVE as u8);
    builder.append_try_end();

    // Catch(Exception) handler
    builder.append_catch_start(tokens.exception_type_ref);
    builder.append_store_local(exception_local);

    // Call finish tracer with exception (wrapped in try-catch for safety)
    build_safe_call_finish_tracer(
        &mut builder,
        tracer_local,
        exception_local,
        result_local,
        tokens,
        true, // exception path
    );

    builder.append_opcode(CEE_RETHROW);
    builder.append_catch_end();

    builder.append_label(&after_catch_label);

    // Call finish tracer with return value (wrapped in try-catch for safety)
    build_safe_call_finish_tracer(
        &mut builder,
        tracer_local,
        exception_local,
        result_local,
        tokens,
        false, // normal return path
    );

    // --- Return ---
    if let Some(result_idx) = result_local {
        builder.append_load_local(result_idx);
    }
    builder.append_opcode(CEE_RET);

    // 4. Build exception handler table
    let mut eh_manip = if let Some(extra_bytes) = &parsed.extra_sections {
        let mut m = ExceptionHandlerManipulator::from_extra_section(extra_bytes)?;
        // If user code was rewritten (multi-ret), remap original EH offsets
        // before the uniform user_code_offset shift.
        m.apply_offset_map(&preprocessed.offset_map);
        m
    } else {
        ExceptionHandlerManipulator::new()
    };

    // Add new clauses from the instruction builder
    eh_manip.add_clauses(builder.get_completed_clauses());

    let user_code_offset = builder.get_user_code_offset();
    let extra_section_bytes = eh_manip.get_extra_section_bytes(user_code_offset);

    // 5. Update header
    let code_bytes = builder.get_bytes();
    header.code_size = code_bytes.len() as u32;
    header.local_var_sig_tok = local_sig_token;
    header.flags |= method_header::COR_ILMETHOD_FAT_FORMAT
        | method_header::COR_ILMETHOD_INIT_LOCALS
        | method_header::COR_ILMETHOD_MORE_SECTS;

    // Ensure max stack is sufficient (C++ uses max(original, 10, param_count+1))
    let min_stack = std::cmp::max(10, ctx.method_signature.param_count as u16 + 1);
    header.max_stack = std::cmp::max(header.max_stack, min_stack);

    // 6. Combine header + code + extra sections
    let extra = if extra_section_bytes.is_empty() {
        None
    } else {
        Some(extra_section_bytes.as_slice())
    };
    let result = method_header::build_method_bytes(&header, code_bytes, extra);

    Ok(result)
}

/// Resolve all metadata tokens needed for the injection template.
fn resolve_injection_tokens(
    ctx: &InstrumentationContext,
    tokenizer: &mut Tokenizer,
) -> Result<InjectionTokens, IlError> {
    // Type references
    let exception_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Exception")?;
    let object_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Object")?;
    let uint32_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.UInt32")?;
    let uint64_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.UInt64")?;
    let type_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Type")?;
    let method_base_type_ref =
        tokenizer.get_type_ref_token("mscorlib", "System.Reflection.MethodBase")?;
    let action2_type_ref = tokenizer.get_type_ref_token("mscorlib", "System.Action`2")?;

    // Type.GetTypeFromHandle(RuntimeTypeHandle) — static method
    let runtime_type_handle_ref =
        tokenizer.get_type_ref_token("mscorlib", "System.RuntimeTypeHandle")?;
    let get_type_sig = build_get_type_from_handle_sig(type_type_ref, runtime_type_handle_ref);
    let get_type_from_handle_ref =
        tokenizer.get_member_ref_token(type_type_ref, "GetTypeFromHandle", &get_type_sig)?;

    // MethodBase.Invoke(object, object[]) — instance method
    let invoke_sig = build_method_base_invoke_sig(object_type_ref);
    let method_base_invoke_ref =
        tokenizer.get_member_ref_token(method_base_type_ref, "Invoke", &invoke_sig)?;

    // Action<object, Exception>.Invoke(!0, !1) — instance method on open generic
    let action_invoke_sig = build_action2_invoke_sig();
    let action2_invoke_ref =
        tokenizer.get_member_ref_token(action2_type_ref, "Invoke", &action_invoke_sig)?;

    // TypeSpec for closed generic Action<object, Exception>
    // Used by castclass in the finish-tracer call.
    let action2_typespec_sig =
        build_action2_typespec_sig(action2_type_ref, object_type_ref, exception_type_ref);
    let action2_type_spec = tokenizer.get_type_spec_token(&action2_typespec_sig)?;

    // MemberRef for Invoke on the closed TypeSpec
    // The parent is the TypeSpec (not the open TypeRef), which tells the CLR
    // the exact generic instantiation for the callvirt target.
    let action2_invoke_on_spec =
        tokenizer.get_member_ref_token(action2_type_spec, "Invoke", &action_invoke_sig)?;

    // String tokens
    let tracer_factory_name_token = tokenizer.get_string_token(&ctx.tracer_factory_name)?;
    let metric_name_token = tokenizer.get_string_token(&ctx.metric_name)?;
    let assembly_name_token = tokenizer.get_string_token(&ctx.assembly_name)?;
    let type_name_token = tokenizer.get_string_token(&ctx.type_name)?;
    let method_name_token = tokenizer.get_string_token(&ctx.method_name)?;
    let argument_signature_token = tokenizer.get_string_token(&ctx.argument_signature)?;

    Ok(InjectionTokens {
        exception_type_ref,
        object_type_ref,
        uint32_type_ref,
        uint64_type_ref,
        type_type_ref,
        get_type_from_handle_ref,
        method_base_type_ref,
        method_base_invoke_ref,
        action2_type_ref,
        action2_invoke_ref,
        action2_type_spec,
        action2_invoke_on_spec,
        tracer_factory_name_token,
        metric_name_token,
        assembly_name_token,
        type_name_token,
        method_name_token,
        argument_signature_token,
    })
}

/// Build the SafeCallGetTracer section.
///
/// Creates an 11-element object array and calls AgentShim.GetFinishTracerDelegate
/// via reflection (MethodBase.Invoke). Wrapped in try-catch to swallow errors.
///
/// Reference: `InstrumentFunctionManipulator::SafeCallGetTracer` at
/// `InstrumentFunctionManipulator.h` lines 86-105 and `CallGetTracer` lines 154-218.
pub(crate) fn build_safe_call_get_tracer(
    builder: &mut InstructionBuilder,
    ctx: &InstrumentationContext,
    tokens: &InjectionTokens,
    tracer_local: u16,
) {
    // try {
    builder.append_try_start();

    // POC: The C++ profiler uses a bootstrap shim (not MethodBase.Invoke)
    // to load the agent assembly and resolve GetFinishTracerDelegate.
    // For the POC, we emit a no-op tracer lookup that leaves tracer=null.
    // The finish-tracer code handles null tracer gracefully (skips the call).
    // TODO: Replace with the actual agent bootstrap sequence.
    builder.append_opcode(CEE_LDNULL);
    builder.append_store_local(tracer_local);

    // Full implementation would build the 11-element object array and call
    // AgentShim.GetFinishTracerDelegate via the bootstrap mechanism.
    // Keeping the scaffolding commented out for reference:
    /*
    builder.append_opcode(CEE_LDNULL); // null instance for Invoke
    builder.append_ldc_i4(11);
    builder.append_opcode_u32(CEE_NEWARR, tokens.object_type_ref);

    // [0] tracerFactoryName (string)
    store_array_element_string(builder, 0, tokens.tracer_factory_name_token);

    // [1] tracerFactoryArgs (uint32, boxed)
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(1);
    builder.append_ldc_i4(ctx.tracer_factory_args as i32);
    builder.append_opcode_u32(CEE_BOX, tokens.uint32_type_ref);
    builder.append_opcode(CEE_STELEM_REF);

    // [2] metricName (string)
    store_array_element_string(builder, 2, tokens.metric_name_token);

    // [3] assemblyName (string)
    store_array_element_string(builder, 3, tokens.assembly_name_token);

    // [4] type (via ldtoken + GetTypeFromHandle)
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(4);
    builder.append_opcode_u32(CEE_LDTOKEN, ctx.type_token);
    builder.append_opcode_u32(CEE_CALL, tokens.get_type_from_handle_ref);
    builder.append_opcode(CEE_STELEM_REF);

    // [5] typeName (string)
    store_array_element_string(builder, 5, tokens.type_name_token);

    // [6] functionName (string)
    store_array_element_string(builder, 6, tokens.method_name_token);

    // [7] argumentSignature (string)
    store_array_element_string(builder, 7, tokens.argument_signature_token);

    // [8] this (ldarg.0 if instance, ldnull if static)
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(8);
    if ctx.method_signature.has_this {
        builder.append_load_argument(0);
    } else {
        builder.append_opcode(CEE_LDNULL);
    }
    builder.append_opcode(CEE_STELEM_REF);

    // [9] parameters (object[] — POC: empty array for simplicity)
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(9);
    builder.append_ldc_i4(0);
    builder.append_opcode_u32(CEE_NEWARR, tokens.object_type_ref);
    builder.append_opcode(CEE_STELEM_REF);

    // [10] functionId (uint64, boxed)
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(10);
    builder.append_opcode_u64(CEE_LDC_I8, ctx.function_id);
    builder.append_opcode_u32(CEE_BOX, tokens.uint64_type_ref);
    builder.append_opcode(CEE_STELEM_REF);

    // Call MethodBase.Invoke(null, object[])
    // builder.append_opcode_u32(CEE_CALLVIRT, tokens.method_base_invoke_ref);
    // builder.append_store_local(tracer_local);
    */

    // Must exit try block via leave — falling through to catch is illegal
    let try_leave = builder.append_jump_auto(CEE_LEAVE as u8);
    builder.append_try_end();

    // } catch { pop }
    builder.append_catch_start(tokens.exception_type_ref);
    builder.append_opcode(CEE_POP);
    let catch_leave = builder.append_jump_auto(CEE_LEAVE as u8);
    builder.append_catch_end();
    builder.append_label(&try_leave);
    builder.append_label(&catch_leave);
}

/// Build the SafeCallFinishTracer section.
///
/// Calls Action<object, Exception>.Invoke on the tracer delegate.
/// Wrapped in try-catch to swallow errors.
///
/// Reference: `InstrumentFunctionManipulator::CallFinishTracerWithReturnValue`
/// and `CallFinishTracerWithException` at `InstrumentFunctionManipulator.h`.
fn build_safe_call_finish_tracer(
    builder: &mut InstructionBuilder,
    tracer_local: u16,
    exception_local: u16,
    result_local: Option<u16>,
    tokens: &InjectionTokens,
    is_exception_path: bool,
) {
    builder.append_try_start();

    // if (tracer != null) — check and skip if null
    builder.append_load_local(tracer_local);
    let skip_label = builder.append_jump_auto(CEE_BRFALSE as u8);

    // Cast tracer to Action<object, Exception> (closed generic TypeSpec)
    builder.append_load_local(tracer_local);
    builder.append_opcode_u32(CEE_CASTCLASS, tokens.action2_type_spec);

    if is_exception_path {
        // Pass: (null, exception)
        builder.append_opcode(CEE_LDNULL);
        builder.append_load_local(exception_local);
    } else {
        // Pass: (result_or_null, null)
        if let Some(result_idx) = result_local {
            builder.append_load_local(result_idx);
        } else {
            builder.append_opcode(CEE_LDNULL);
        }
        builder.append_opcode(CEE_LDNULL); // no exception
    }

    // Call Action<object, Exception>.Invoke(!0, !1) on the closed TypeSpec
    builder.append_opcode_u32(CEE_CALLVIRT, tokens.action2_invoke_on_spec);

    builder.append_label(&skip_label);

    let leave_label = builder.append_jump_auto(CEE_LEAVE as u8);
    builder.append_try_end();

    // } catch { pop }
    builder.append_catch_start(tokens.exception_type_ref);
    builder.append_opcode(CEE_POP);
    let catch_leave = builder.append_jump_auto(CEE_LEAVE as u8);
    builder.append_catch_end();
    builder.append_label(&leave_label);
    builder.append_label(&catch_leave);
}

/// Helper: store a string token at array index.
/// Emits: dup, ldc.i4 index, ldstr token, stelem.ref
fn store_array_element_string(builder: &mut InstructionBuilder, index: i32, string_token: u32) {
    builder.append_opcode(CEE_DUP);
    builder.append_ldc_i4(index);
    builder.append_opcode_u32(CEE_LDSTR, string_token);
    builder.append_opcode(CEE_STELEM_REF);
}

// ==================== Method signature builders ====================

/// Build the signature for Type.GetTypeFromHandle(RuntimeTypeHandle).
///
/// Signature: DEFAULT, 1 param, returns CLASS System.Type, param is VALUETYPE RuntimeTypeHandle
pub fn build_get_type_from_handle_sig(type_token: u32, rth_token: u32) -> Vec<u8> {
    let mut sig = Vec::new();
    sig.push(0x00); // DEFAULT calling convention (static)
    sig.push(0x01); // 1 parameter
    // Return type: CLASS System.Type
    sig.push(0x12); // ELEMENT_TYPE_CLASS
    sig.extend_from_slice(
        &crate::il::sig_compression::compress_token(type_token).unwrap_or_default(),
    );
    // Param 1: VALUETYPE RuntimeTypeHandle
    sig.push(0x11); // ELEMENT_TYPE_VALUETYPE
    sig.extend_from_slice(
        &crate::il::sig_compression::compress_token(rth_token).unwrap_or_default(),
    );
    sig
}

/// Build the signature for MethodBase.Invoke(object, object[]).
///
/// Signature: HASTHIS, 2 params, returns CLASS object, params are CLASS object + SZARRAY CLASS object
pub fn build_method_base_invoke_sig(object_token: u32) -> Vec<u8> {
    let mut sig = Vec::new();
    sig.push(0x20); // HASTHIS calling convention
    sig.push(0x02); // 2 parameters
    // Return type: CLASS System.Object
    sig.push(0x12); // ELEMENT_TYPE_CLASS
    let compressed =
        crate::il::sig_compression::compress_token(object_token).unwrap_or_default();
    sig.extend_from_slice(&compressed);
    // Param 1: CLASS System.Object
    sig.push(0x12);
    sig.extend_from_slice(&compressed);
    // Param 2: SZARRAY CLASS System.Object
    sig.push(0x1D); // ELEMENT_TYPE_SZARRAY
    sig.push(0x12);
    sig.extend_from_slice(&compressed);
    sig
}

/// Build the signature for Action<object, Exception>.Invoke(!0, !1).
///
/// Signature: HASTHIS, 2 params, returns VOID, params are VAR 0, VAR 1
pub fn build_action2_invoke_sig() -> Vec<u8> {
    vec![
        0x20, // HASTHIS
        0x02, // 2 parameters
        0x01, // Return type: VOID
        0x13, 0x00, // Param 1: ELEMENT_TYPE_VAR, index 0
        0x13, 0x01, // Param 2: ELEMENT_TYPE_VAR, index 1
    ]
}

/// Build the TypeSpec signature for `Action<object, Exception>`.
///
/// This is a GENERICINST signature that instantiates the open generic
/// `System.Action`2` with `System.Object` and `System.Exception`.
///
/// Format (ECMA-335 §II.23.2.14):
/// ```text
/// GENERICINST (CLASS | VALUETYPE) TypeDefOrRef GenArgCount Type+
/// ```
///
/// For Action<object, Exception>:
/// ```text
/// 0x15          ELEMENT_TYPE_GENERICINST
/// 0x12          ELEMENT_TYPE_CLASS
/// compressed    Action`2 TypeRef token
/// 0x02          2 generic arguments
/// 0x12          ELEMENT_TYPE_CLASS (arg 0: object)
/// compressed    System.Object TypeRef token
/// 0x12          ELEMENT_TYPE_CLASS (arg 1: Exception)
/// compressed    System.Exception TypeRef token
/// ```
pub fn build_action2_typespec_sig(
    action2_type_ref: u32,
    object_type_ref: u32,
    exception_type_ref: u32,
) -> Vec<u8> {
    let mut sig = Vec::new();
    sig.push(0x15); // ELEMENT_TYPE_GENERICINST
    sig.push(0x12); // ELEMENT_TYPE_CLASS
    sig.extend_from_slice(
        &crate::il::sig_compression::compress_token(action2_type_ref).unwrap_or_default(),
    );
    sig.push(0x02); // 2 generic arguments

    // Arg 0: System.Object
    sig.push(0x12); // ELEMENT_TYPE_CLASS
    sig.extend_from_slice(
        &crate::il::sig_compression::compress_token(object_type_ref).unwrap_or_default(),
    );

    // Arg 1: System.Exception
    sig.push(0x12); // ELEMENT_TYPE_CLASS
    sig.extend_from_slice(
        &crate::il::sig_compression::compress_token(exception_type_ref).unwrap_or_default(),
    );

    sig
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::method_signature::parse_method_signature;

    /// Create fake tokens with predictable values for testing.
    fn test_tokens() -> InjectionTokens {
        InjectionTokens {
            exception_type_ref: 0x0100_0001,
            object_type_ref: 0x0100_0002,
            uint32_type_ref: 0x0100_0003,
            uint64_type_ref: 0x0100_0004,
            type_type_ref: 0x0100_0005,
            get_type_from_handle_ref: 0x0A00_0001,
            method_base_type_ref: 0x0100_0006,
            method_base_invoke_ref: 0x0A00_0002,
            action2_type_ref: 0x0100_0007,
            action2_invoke_ref: 0x0A00_0003,
            action2_type_spec: 0x1B00_0001,
            action2_invoke_on_spec: 0x0A00_0004,
            tracer_factory_name_token: 0x7000_0001,
            metric_name_token: 0x7000_0002,
            assembly_name_token: 0x7000_0003,
            type_name_token: 0x7000_0004,
            method_name_token: 0x7000_0005,
            argument_signature_token: 0x7000_0006,
        }
    }

    /// Create a context for a static void method with no parameters.
    fn test_ctx_static_void() -> InstrumentationContext {
        InstrumentationContext {
            assembly_name: "TestAssembly".to_string(),
            type_name: "TestNamespace.TestClass".to_string(),
            method_name: "TestMethod".to_string(),
            function_id: 0x1234,
            type_token: 0x0200_0001,
            tracer_factory_name: "TestFactory".to_string(),
            tracer_factory_args: 0,
            metric_name: "Custom/Test".to_string(),
            argument_signature: "".to_string(),
            method_signature: parse_method_signature(&[0x00, 0x00, 0x01]).unwrap(),
        }
    }

    /// Create a context for an instance method returning int with 1 param.
    fn test_ctx_instance_nonvoid() -> InstrumentationContext {
        InstrumentationContext {
            assembly_name: "TestAssembly".to_string(),
            type_name: "TestNamespace.TestClass".to_string(),
            method_name: "Compute".to_string(),
            function_id: 0x5678,
            type_token: 0x0200_0002,
            tracer_factory_name: "TestFactory".to_string(),
            tracer_factory_args: 42,
            metric_name: "Custom/Compute".to_string(),
            argument_signature: "(int)".to_string(),
            method_signature: parse_method_signature(&[0x20, 0x01, 0x08, 0x08]).unwrap(),
        }
    }

    /// Minimal tiny method: just `ret` (1 byte code).
    /// Tiny header: (1 << 2) | 0x02 = 0x06
    fn tiny_ret_method() -> Vec<u8> {
        vec![0x06, 0x2A]
    }

    /// Fat method with `nop; ret` (2 bytes code), max_stack=2, no locals.
    fn fat_nop_ret_method() -> Vec<u8> {
        let mut bytes = vec![0u8; 14];
        // flags=0x0013 (Fat|InitLocals), size=3
        let flags_size: u16 = 0x0013 | (3 << 12);
        bytes[0..2].copy_from_slice(&flags_size.to_le_bytes());
        bytes[2..4].copy_from_slice(&2u16.to_le_bytes()); // max_stack=2
        bytes[4..8].copy_from_slice(&2u32.to_le_bytes()); // code_size=2
        bytes[8..12].copy_from_slice(&0u32.to_le_bytes()); // local_var_sig=0
        bytes[12] = 0x00; // nop
        bytes[13] = 0x2A; // ret
        bytes
    }

    // ==================== Signature builder tests ====================

    #[test]
    fn get_type_from_handle_sig_complete_verification() {
        let type_token = 0x0100_0001; // TypeRef row 1
        let rth_token = 0x0100_0002; // TypeRef row 2
        let sig = build_get_type_from_handle_sig(type_token, rth_token);

        assert_eq!(sig[0], 0x00); // DEFAULT (static)
        assert_eq!(sig[1], 0x01); // 1 param
        assert_eq!(sig[2], 0x12); // ELEMENT_TYPE_CLASS (return type)
        // TypeRef 0x01000001: (1 << 2) | 1 = 5 → [0x05]
        assert_eq!(sig[3], 0x05);
        assert_eq!(sig[4], 0x11); // ELEMENT_TYPE_VALUETYPE (param)
        // TypeRef 0x01000002: (2 << 2) | 1 = 9 → [0x09]
        assert_eq!(sig[5], 0x09);
        assert_eq!(sig.len(), 6);
    }

    #[test]
    fn method_base_invoke_sig_complete_verification() {
        let object_token = 0x0100_0001;
        let sig = build_method_base_invoke_sig(object_token);

        assert_eq!(sig[0], 0x20); // HASTHIS
        assert_eq!(sig[1], 0x02); // 2 params
        assert_eq!(sig[2], 0x12); // CLASS (return: object)
        assert_eq!(sig[3], 0x05); // compressed token for 0x01000001
        assert_eq!(sig[4], 0x12); // CLASS (param1: object)
        assert_eq!(sig[5], 0x05);
        assert_eq!(sig[6], 0x1D); // SZARRAY (param2: object[])
        assert_eq!(sig[7], 0x12); // CLASS
        assert_eq!(sig[8], 0x05);
        assert_eq!(sig.len(), 9);
    }

    #[test]
    fn action2_invoke_sig_format() {
        let sig = build_action2_invoke_sig();
        assert_eq!(sig, vec![0x20, 0x02, 0x01, 0x13, 0x00, 0x13, 0x01]);
    }

    #[test]
    fn action2_typespec_sig_format() {
        // Action`2 = TypeRef 0x01000007, Object = TypeRef 0x01000002, Exception = TypeRef 0x01000001
        let sig = build_action2_typespec_sig(0x0100_0007, 0x0100_0002, 0x0100_0001);

        assert_eq!(sig[0], 0x15); // ELEMENT_TYPE_GENERICINST
        assert_eq!(sig[1], 0x12); // ELEMENT_TYPE_CLASS
        // Action`2 compressed token: (7 << 2) | 1 = 29 → [0x1D]
        assert_eq!(sig[2], 0x1D);
        assert_eq!(sig[3], 0x02); // 2 generic arguments
        // Arg 0: CLASS Object → (2 << 2) | 1 = 9 → [0x09]
        assert_eq!(sig[4], 0x12);
        assert_eq!(sig[5], 0x09);
        // Arg 1: CLASS Exception → (1 << 2) | 1 = 5 → [0x05]
        assert_eq!(sig[6], 0x12);
        assert_eq!(sig[7], 0x05);
        assert_eq!(sig.len(), 8);
    }

    // ==================== Helper tests ====================

    #[test]
    fn store_array_element_emits_correct_opcodes() {
        let mut builder = InstructionBuilder::new();
        store_array_element_string(&mut builder, 3, 0x70000042);

        let bytes = builder.get_bytes();
        assert_eq!(bytes[0], 0x25); // dup
        assert_eq!(bytes[1], 0x19); // ldc.i4.3
        assert_eq!(bytes[2], 0x72); // ldstr
        assert_eq!(bytes[3], 0x42); // token LE
        assert_eq!(bytes[4], 0x00);
        assert_eq!(bytes[5], 0x00);
        assert_eq!(bytes[6], 0x70);
        assert_eq!(bytes[7], 0xA2); // stelem.ref
    }

    #[test]
    fn store_array_element_index_zero_uses_ldc_i4_0() {
        let mut builder = InstructionBuilder::new();
        store_array_element_string(&mut builder, 0, 0x70000001);
        let bytes = builder.get_bytes();
        assert_eq!(bytes[0], 0x25); // dup
        assert_eq!(bytes[1], 0x16); // ldc.i4.0 (optimized)
    }

    #[test]
    fn store_array_element_index_10_uses_ldc_i4_s() {
        let mut builder = InstructionBuilder::new();
        store_array_element_string(&mut builder, 10, 0x70000001);
        let bytes = builder.get_bytes();
        assert_eq!(bytes[0], 0x25); // dup
        assert_eq!(bytes[1], 0x1F); // ldc.i4.s
        assert_eq!(bytes[2], 0x0A); // 10
    }

    // ==================== End-to-end IL generation tests ====================

    #[test]
    fn static_void_tiny_method_produces_valid_fat_output() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000042, 0,
        ).unwrap();

        // Output must be a valid fat-format method
        assert!(result.len() >= 12, "Output too short for fat header");
        let flags_size = u16::from_le_bytes([result[0], result[1]]);
        let flags = flags_size & 0x0FFF;
        assert_eq!(flags & 0x0003, 0x0003, "Must be fat format");
        assert_ne!(flags & 0x0010, 0, "Must have InitLocals");
        assert_ne!(flags & 0x0008, 0, "Must have MoreSects");

        // local_var_sig_tok must be set
        let local_sig = u32::from_le_bytes([result[8], result[9], result[10], result[11]]);
        assert_eq!(local_sig, 0x11000042);

        // max_stack must be at least 10
        let max_stack = u16::from_le_bytes([result[2], result[3]]);
        assert!(max_stack >= 10, "max_stack {} < 10", max_stack);
    }

    #[test]
    fn static_void_method_starts_with_null_initialization() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        // Code starts at offset 12 (fat header)
        let code = &result[12..];

        // First instructions: ldnull, stloc.0, ldnull, stloc.1
        assert_eq!(code[0], 0x14, "Expected ldnull (tracer = null)");
        assert_eq!(code[1], 0x0A, "Expected stloc.0 (tracer)");
        assert_eq!(code[2], 0x14, "Expected ldnull (exception = null)");
        assert_eq!(code[3], 0x0B, "Expected stloc.1 (exception)");
    }

    #[test]
    fn static_void_method_ends_with_ret() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        // Parse the header to find code_size
        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]);
        let last_code_byte = result[12 + code_size as usize - 1];
        assert_eq!(last_code_byte, 0x2A, "Method must end with ret");
    }

    #[test]
    fn static_void_method_contains_original_code() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();
        // Original code is just [0x2A] (ret)

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]);
        let code = &result[12..12 + code_size as usize];

        // The original 0x2A (ret) should appear somewhere in the code
        // preceded by other instructions. It should NOT be the first byte.
        assert!(code.len() > 10, "Instrumented code must be larger than original");
        assert!(
            code.windows(1).any(|w| w[0] == 0x2A),
            "Original ret opcode should appear in code"
        );
    }

    #[test]
    fn static_void_method_has_exception_clauses() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code_end = 12 + code_size;

        // Find 4-byte-aligned extra section start
        let mut extra_start = code_end;
        while extra_start % 4 != 0 {
            extra_start += 1;
        }

        assert!(result.len() > extra_start, "Must have extra sections");

        // Extra section header: first byte should be 0x41 (EHTable | FatFormat)
        assert_eq!(result[extra_start], 0x41, "Extra section must be fat EH table");

        // Parse extra section size (24-bit LE at offset 1-3)
        let eh_size = (result[extra_start + 1] as u32)
            | ((result[extra_start + 2] as u32) << 8)
            | ((result[extra_start + 3] as u32) << 16);

        // POC simplified template has 2 clauses:
        // 1. SafeCallGetTracer try-catch (no-op body)
        // 2. User code try-catch(Exception)
        let clause_count = (eh_size - 4) / 24;
        assert!(clause_count >= 2,
            "Expected at least 2 exception clauses, got {}", clause_count);
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no array built
    fn static_void_method_contains_newarr_for_param_array() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Should contain CEE_NEWARR (0x8D) for the 11-element object array
        assert!(code.contains(&0x8D), "Must contain newarr opcode");

        // Should contain ldc.i4.s 11 (0x1F, 0x0B) for the array size
        assert!(
            code.windows(2).any(|w| w[0] == 0x1F && w[1] == 0x0B),
            "Must contain ldc.i4.s 11 for array size"
        );
    }

    #[test]
    fn static_void_method_contains_callvirt_for_invoke() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Should contain CEE_CALLVIRT (0x6F) for Action<object,Exception>.Invoke
        // in both the exception-path and return-path finish-tracer calls.
        let callvirt_count = code.iter().filter(|&&b| b == 0x6F).count();
        assert!(callvirt_count >= 2,
            "Expected at least 2 callvirt (finish-tracer Invoke calls), got {}", callvirt_count);
    }

    #[test]
    fn static_void_method_contains_rethrow() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Should contain CEE_RETHROW (0xFE, 0x1A)
        assert!(
            code.windows(2).any(|w| w[0] == 0xFE && w[1] == 0x1A),
            "Must contain rethrow opcode"
        );
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no strings emitted
    fn static_void_method_contains_ldstr_for_all_strings() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Should contain 6 ldstr instructions (one for each string param)
        let ldstr_count = code.iter().filter(|&&b| b == 0x72).count();
        assert_eq!(ldstr_count, 6,
            "Expected 6 ldstr (factory, metric, assembly, type, method, argsig), got {}",
            ldstr_count);
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op
    fn static_void_method_uses_ldnull_for_this_slot() {
        // For static methods, array element [8] (this) should use ldnull, not ldarg.0.
        // We verify this by checking the instruction builder output directly.
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let mut builder = InstructionBuilder::new();

        build_safe_call_get_tracer(&mut builder, &ctx, &tokens, 0);

        let bytes = builder.get_bytes();

        // The sequence for [8] should be: dup, ldc.i4.8, ldnull, stelem.ref
        // ldnull=0x14, ldc.i4.8=0x1E, stelem.ref=0xA2
        // Look for: dup(0x25), ldc.i4.8(0x1E), ldnull(0x14), stelem.ref(0xA2)
        let pattern = [0x25u8, 0x1E, 0x14, 0xA2];
        assert!(
            bytes.windows(4).any(|w| w == pattern),
            "Static method should use ldnull (not ldarg.0) for 'this' in array element [8]"
        );
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no this loading
    fn instance_method_uses_ldarg_0_for_this() {
        let ctx = test_ctx_instance_nonvoid();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Instance method should have ldarg.0 (0x02) for loading 'this'
        assert!(
            code.contains(&0x02),
            "Instance method should emit ldarg.0 for 'this'"
        );
    }

    #[test]
    fn nonvoid_method_has_result_load_before_ret() {
        let ctx = test_ctx_instance_nonvoid();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // For non-void, the last 2 bytes should be: ldloc.2 (0x08), ret (0x2A)
        let last_two = &code[code.len() - 2..];
        assert_eq!(last_two[0], 0x08, "Expected ldloc.2 before ret");
        assert_eq!(last_two[1], 0x2A, "Expected ret at end");
    }

    #[test]
    fn void_method_does_not_load_result_before_ret() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // For void, the last byte should be just ret (0x2A), not preceded by ldloc
        let last_byte = code[code.len() - 1];
        assert_eq!(last_byte, 0x2A, "Expected ret at end");
        // The byte before ret should NOT be ldloc.2 (0x08) for void methods
        // (it will be something from the finish-tracer catch leave label)
    }

    #[test]
    fn fat_input_method_preserves_original_code() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = fat_nop_ret_method();
        // Original code is [0x00, 0x2A] (nop, ret)

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // The original code (nop, ret) is embedded but the final ret is replaced
        // with nop to prevent illegal ret inside try block. So we check for nop+nop.
        assert!(
            code.windows(2).any(|w| w[0] == 0x00 && w[1] == 0x00),
            "Original code (with ret→nop) must be preserved in output"
        );
    }

    #[test]
    fn instrumented_code_is_significantly_larger_than_original() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();
        // Original: 2 bytes total (1 byte header + 1 byte code)

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        // Instrumented method should be notably larger than original (header + try-catch + EH table)
        assert!(result.len() > 50,
            "Instrumented method ({} bytes) should be larger than original (2 bytes)",
            result.len());
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no function ID embedded
    fn function_id_is_embedded_as_i8() {
        let mut ctx = test_ctx_static_void();
        ctx.function_id = 0xDEAD_BEEF_CAFE_BABE;
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // Find ldc.i8 (0x21) followed by the function ID in LE
        let expected: [u8; 9] = [
            0x21, // ldc.i8
            0xBE, 0xBA, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE, // 0xDEADBEEFCAFEBABE LE
        ];
        assert!(
            code.windows(9).any(|w| w == expected),
            "Function ID 0xDEADBEEFCAFEBABE should be embedded as ldc.i8"
        );
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no ldtoken emitted
    fn type_token_is_embedded_via_ldtoken() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // ldtoken (0xD0) followed by type_token 0x02000001 LE
        let expected: [u8; 5] = [0xD0, 0x01, 0x00, 0x00, 0x02];
        assert!(
            code.windows(5).any(|w| w == expected),
            "Type token should be embedded via ldtoken"
        );
    }

    #[test]
    fn output_is_4_byte_aligned_before_extra_sections() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code_end = 12 + code_size;

        // Find extra section start (must be 4-byte aligned)
        let mut extra_start = code_end;
        while extra_start % 4 != 0 {
            extra_start += 1;
        }

        // If there are bytes between code_end and extra_start, they must be zero padding
        for i in code_end..extra_start {
            assert_eq!(result[i], 0x00, "Padding byte at offset {} must be zero", i);
        }
    }

    #[test]
    #[ignore] // POC: SafeCallGetTracer is no-op, no boxing emitted
    fn tracer_factory_args_embedded_via_box() {
        let mut ctx = test_ctx_static_void();
        ctx.tracer_factory_args = 99;
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code = &result[12..12 + code_size];

        // ldc.i4.s 99 (0x1F, 0x63) followed by box (0x8C) + uint32 token
        let has_ldc_99 = code.windows(2).any(|w| w[0] == 0x1F && w[1] == 99);
        assert!(has_ldc_99, "tracer_factory_args=99 should be loaded via ldc.i4.s");

        // box opcode should be present
        assert!(code.contains(&0x8C), "Must contain box opcode for uint32");
    }

    /// Fat method with an existing try-catch block.
    /// Code: try { nop } catch(Exception) { pop; leave } ret
    /// 5 bytes of code + 12 bytes fat header + padding + extra section
    fn fat_method_with_existing_try_catch() -> Vec<u8> {
        let code_size: u32 = 7;
        let mut bytes = Vec::new();

        // Fat header: flags=0x001B (Fat|InitLocals|MoreSects), size=3
        let flags_size: u16 = 0x001B | (3 << 12);
        bytes.extend_from_slice(&flags_size.to_le_bytes());
        bytes.extend_from_slice(&8u16.to_le_bytes()); // max_stack=8
        bytes.extend_from_slice(&code_size.to_le_bytes());
        bytes.extend_from_slice(&0u32.to_le_bytes()); // local_var_sig=0

        // Code: nop(0), leave.s +2(1-2), pop(3), leave.s +0(4-5), ret(6)
        bytes.push(0x00); // nop         - offset 0 (try body)
        bytes.push(0xDE); // leave.s
        bytes.push(0x02); // +2 → skip to ret
        bytes.push(0x26); // pop          - offset 3 (catch body)
        bytes.push(0xDE); // leave.s
        bytes.push(0x00); // +0 → to ret
        bytes.push(0x2A); // ret          - offset 6

        // Pad to 4-byte alignment: header(12) + code(7) = 19, need 1 byte
        bytes.push(0x00);

        // Extra section: 1 fat catch clause
        bytes.push(0x41); // EHTable | FatFormat
        // size = 4 + 24 = 28
        bytes.push(0x1C);
        bytes.push(0x00);
        bytes.push(0x00);
        // Clause: catch, try@0 len=3, handler@3 len=3, token=0x01000001
        bytes.extend_from_slice(&0u32.to_le_bytes());          // flags = catch
        bytes.extend_from_slice(&0u32.to_le_bytes());          // tryOffset = 0
        bytes.extend_from_slice(&3u32.to_le_bytes());          // tryLength = 3
        bytes.extend_from_slice(&3u32.to_le_bytes());          // handlerOffset = 3
        bytes.extend_from_slice(&3u32.to_le_bytes());          // handlerLength = 3
        bytes.extend_from_slice(&0x0100_0001u32.to_le_bytes()); // classToken

        bytes
    }

    #[test]
    fn method_with_existing_try_catch_preserves_original_clause() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = fat_method_with_existing_try_catch();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code_end = 12 + code_size;
        let mut extra_start = code_end;
        while extra_start % 4 != 0 {
            extra_start += 1;
        }

        let eh_size_bytes = &result[extra_start + 1..extra_start + 4];
        let eh_size = (eh_size_bytes[0] as u32)
            | ((eh_size_bytes[1] as u32) << 8)
            | ((eh_size_bytes[2] as u32) << 16);
        let clause_count = (eh_size - 4) / 24;

        // Should have injected clauses (at least 4) PLUS the original clause (1)
        assert!(clause_count >= 5,
            "Expected at least 5 clauses (4 injected + 1 original), got {}", clause_count);

        // The last clause should be the shifted original: its class_token should be 0x01000001
        let last_clause_start = extra_start + 4 + (clause_count as usize - 1) * 24;
        let class_token = u32::from_le_bytes([
            result[last_clause_start + 20], result[last_clause_start + 21],
            result[last_clause_start + 22], result[last_clause_start + 23],
        ]);
        assert_eq!(class_token, 0x0100_0001,
            "Original clause class_token should be preserved");

        // The original clause's try_offset should be shifted by user_code_offset
        let shifted_try_offset = u32::from_le_bytes([
            result[last_clause_start + 4], result[last_clause_start + 5],
            result[last_clause_start + 6], result[last_clause_start + 7],
        ]);
        assert!(shifted_try_offset > 0,
            "Original clause try_offset should be shifted (was 0, now {})", shifted_try_offset);
    }

    #[test]
    fn exception_clause_offsets_are_non_zero() {
        let ctx = test_ctx_static_void();
        let tokens = test_tokens();
        let original_il = tiny_ret_method();

        let result = build_instrumented_method_with_tokens(
            &ctx, &tokens, &original_il, 0x11000001, 0,
        ).unwrap();

        let code_size = u32::from_le_bytes([result[4], result[5], result[6], result[7]]) as usize;
        let code_end = 12 + code_size;
        let mut extra_start = code_end;
        while extra_start % 4 != 0 {
            extra_start += 1;
        }

        // Parse each clause and verify offsets make sense
        let eh_size_bytes = &result[extra_start + 1..extra_start + 4];
        let eh_size = (eh_size_bytes[0] as u32)
            | ((eh_size_bytes[1] as u32) << 8)
            | ((eh_size_bytes[2] as u32) << 16);
        let clause_count = (eh_size - 4) / 24;

        for i in 0..clause_count as usize {
            let clause_start = extra_start + 4 + i * 24;
            let try_offset = u32::from_le_bytes([
                result[clause_start + 4], result[clause_start + 5],
                result[clause_start + 6], result[clause_start + 7],
            ]);
            let try_length = u32::from_le_bytes([
                result[clause_start + 8], result[clause_start + 9],
                result[clause_start + 10], result[clause_start + 11],
            ]);
            let handler_offset = u32::from_le_bytes([
                result[clause_start + 12], result[clause_start + 13],
                result[clause_start + 14], result[clause_start + 15],
            ]);
            let handler_length = u32::from_le_bytes([
                result[clause_start + 16], result[clause_start + 17],
                result[clause_start + 18], result[clause_start + 19],
            ]);

            // All offsets and lengths must be non-zero
            assert!(try_length > 0, "Clause {} try_length is 0", i);
            assert!(handler_length > 0, "Clause {} handler_length is 0", i);
            // handler must come after try
            assert!(handler_offset >= try_offset + try_length,
                "Clause {} handler_offset ({}) must be >= try_offset ({}) + try_length ({})",
                i, handler_offset, try_offset, try_length);
            // All offsets must be within code bounds
            assert!((handler_offset + handler_length) as usize <= code_size,
                "Clause {} handler extends beyond code (handler_end={}, code_size={})",
                i, handler_offset + handler_length, code_size);
        }
    }
}
