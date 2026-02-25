// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! New Relic .NET Agent Profiler - COM callback implementation
//!
//! This module implements the ICorProfilerCallback4 COM interface that the CLR
//! calls into when profiling events occur. The `com` crate's `class!` macro
//! handles vtable layout, reference counting, and QueryInterface automatically.

use crate::ffi::*;
use crate::function_control::ICorProfilerFunctionControl;
use crate::il::inject_default::{self, InstrumentationContext};
use crate::instrumentation::InstrumentationMatcher;
use crate::interfaces::*;
use crate::metadata_assembly::{IMetaDataAssemblyEmit, IMetaDataAssemblyImport};
use crate::metadata_emit::IMetaDataEmit2;
use crate::method_resolver;
use crate::method_signature;
use crate::profiler_info::{ICorProfilerInfo4, ICorProfilerInfo5};
use crate::tokenizer::Tokenizer;
use com::{
    interfaces::iunknown::IUnknown,
    sys::{GUID, HRESULT},
};
use log::{error, info, trace, warn};
use std::cell::RefCell;
use std::ffi::c_void;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};

/// New Relic profiler CLSID for .NET Framework
/// Must match C++ profiler: {71DA0A04-7777-4EC6-9643-7D28B46A8A41}
pub const CLSID_PROFILER_NETFX: com::CLSID = com::CLSID {
    data1: 0x71DA0A04,
    data2: 0x7777,
    data3: 0x4EC6,
    data4: [0x96, 0x43, 0x7D, 0x28, 0xB4, 0x6A, 0x8A, 0x41],
};

/// New Relic profiler CLSID for .NET Core/.NET
/// Must match C++ profiler: {36032161-FFC0-4B61-B559-F6C5D41BAE5A}
pub const CLSID_PROFILER_CORECLR: com::CLSID = com::CLSID {
    data1: 0x36032161,
    data2: 0xFFC0,
    data3: 0x4B61,
    data4: [0xB5, 0x59, 0xF6, 0xC5, 0xD4, 0x1B, 0xAE, 0x5A],
};

class! {
    /// The New Relic profiler COM object.
    ///
    /// Implements ICorProfilerCallback4 (which inherits Callback3 → 2 → 1 → IUnknown).
    /// The CLR creates an instance via our class factory and calls these methods
    /// as profiling events occur.
    pub class NewRelicProfiler:
        ICorProfilerCallback4(ICorProfilerCallback3(
            ICorProfilerCallback2(ICorProfilerCallback))) {
        is_core_clr: AtomicBool,
        jit_event_count: AtomicU64,
        module_load_count: AtomicU64,
        profiler_info: RefCell<Option<ICorProfilerInfo4>>,
        matcher: RefCell<Option<InstrumentationMatcher>>,
    }

    // ==================== ICorProfilerCallback ====================

    impl ICorProfilerCallback for NewRelicProfiler {
        pub fn Initialize(&self, pICorProfilerInfoUnk: IUnknown) -> HRESULT {
            info!("New Relic Rust Profiler: Initialize called");

            unsafe {
                // Query for ICorProfilerInfo4 using the com crate's safe wrapper
                let profiler_info = match pICorProfilerInfoUnk.query_interface::<ICorProfilerInfo4>() {
                    Some(info) => info,
                    None => {
                        error!("Failed to get ICorProfilerInfo4");
                        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                    }
                };

                // Get runtime information to determine CLR type
                let mut runtime_type: u32 = 0;
                let mut major_version: USHORT = 0;
                let mut minor_version: USHORT = 0;
                let hr = profiler_info.GetRuntimeInformation(
                    std::ptr::null_mut(), // pClrInstanceId - don't need
                    &mut runtime_type,
                    &mut major_version,
                    &mut minor_version,
                    std::ptr::null_mut(), // pBuildNumber
                    std::ptr::null_mut(), // pQFEVersion
                    0,                     // cchVersionString
                    std::ptr::null_mut(), // pcchVersionString
                    std::ptr::null_mut(), // szVersionString
                );
                if failed(hr) {
                    error!("Failed to get runtime information: 0x{:08x}", hr);
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                let is_core_clr = runtime_type == COR_PRF_RUNTIME_TYPE::COR_PRF_CORE_CLR as u32;
                self.is_core_clr.store(is_core_clr, Ordering::Relaxed);
                info!(
                    "Runtime: {} v{}.{}",
                    if is_core_clr { "CoreCLR" } else { "Desktop CLR" },
                    major_version,
                    minor_version
                );

                // Check if this process should be instrumented
                if !crate::process_filter::should_instrument_process(is_core_clr) {
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                // Set event mask — matches the C++ profiler's mask exactly
                let event_mask: DWORD = (COR_PRF_MONITOR::COR_PRF_MONITOR_JIT_COMPILATION
                    | COR_PRF_MONITOR::COR_PRF_MONITOR_MODULE_LOADS
                    | COR_PRF_MONITOR::COR_PRF_USE_PROFILE_IMAGES
                    | COR_PRF_MONITOR::COR_PRF_MONITOR_THREADS
                    | COR_PRF_MONITOR::COR_PRF_ENABLE_STACK_SNAPSHOT
                    | COR_PRF_MONITOR::COR_PRF_ENABLE_REJIT
                    | COR_PRF_MONITOR::COR_PRF_DISABLE_ALL_NGEN_IMAGES)
                    .bits();

                // Try SetEventMask2 (ICorProfilerInfo5) to also disable tiered compilation
                let mask_set = if let Some(info5) = pICorProfilerInfoUnk.query_interface::<ICorProfilerInfo5>() {
                    // 0x8 = COR_PRF_HIGH_DISABLE_TIERED_COMPILATION
                    let hr = info5.SetEventMask2(event_mask, 0x8);
                    if !failed(hr) {
                        info!("Event mask set via SetEventMask2 (tiered compilation disabled)");
                        true
                    } else {
                        false
                    }
                } else {
                    false
                };

                if !mask_set {
                    let hr = profiler_info.SetEventMask(event_mask);
                    if failed(hr) {
                        error!("SetEventMask failed: 0x{:08x}", hr);
                        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                    }
                    info!("Event mask set via SetEventMask");
                }

                // Store ICorProfilerInfo4 for use in JIT callbacks
                self.profiler_info.replace(Some(profiler_info));

                // Initialize instrumentation matcher with POC test targets
                let matcher = InstrumentationMatcher::with_test_targets();
                info!("Loaded {} instrumentation points", matcher.points().len());
                self.matcher.replace(Some(matcher));

                info!("Profiler initialized successfully");
            }
            S_OK
        }

        pub fn Shutdown(&self) -> HRESULT {
            let jit_count = self.jit_event_count.load(Ordering::Relaxed);
            let module_count = self.module_load_count.load(Ordering::Relaxed);
            info!(
                "New Relic Rust Profiler: Shutdown. JIT events: {}, Module loads: {}",
                jit_count, module_count
            );
            S_OK
        }

        pub fn AppDomainCreationStarted(&self, appDomainId: AppDomainID) -> HRESULT { S_OK }
        pub fn AppDomainCreationFinished(&self, appDomainId: AppDomainID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn AppDomainShutdownStarted(&self, appDomainId: AppDomainID) -> HRESULT { S_OK }
        pub fn AppDomainShutdownFinished(&self, appDomainId: AppDomainID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn AssemblyLoadStarted(&self, assemblyId: AssemblyID) -> HRESULT { S_OK }
        pub fn AssemblyLoadFinished(&self, assemblyId: AssemblyID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn AssemblyUnloadStarted(&self, assemblyId: AssemblyID) -> HRESULT { S_OK }
        pub fn AssemblyUnloadFinished(&self, assemblyId: AssemblyID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn ModuleLoadStarted(&self, moduleId: ModuleID) -> HRESULT { S_OK }

        pub fn ModuleLoadFinished(&self, moduleId: ModuleID, hrStatus: HRESULT) -> HRESULT {
            let count = self.module_load_count.fetch_add(1, Ordering::Relaxed) + 1;
            trace!("ModuleLoadFinished: ModuleID=0x{:x}, Count={}", moduleId, count);
            S_OK
        }

        pub fn ModuleUnloadStarted(&self, moduleId: ModuleID) -> HRESULT { S_OK }
        pub fn ModuleUnloadFinished(&self, moduleId: ModuleID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn ModuleAttachedToAssembly(&self, moduleId: ModuleID, AssemblyId: AssemblyID) -> HRESULT { S_OK }
        pub fn ClassLoadStarted(&self, classId: ClassID) -> HRESULT { S_OK }
        pub fn ClassLoadFinished(&self, classId: ClassID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn ClassUnloadStarted(&self, classId: ClassID) -> HRESULT { S_OK }
        pub fn ClassUnloadFinished(&self, classId: ClassID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn FunctionUnloadStarted(&self, functionId: FunctionID) -> HRESULT { S_OK }

        pub fn JITCompilationStarted(&self, functionId: FunctionID, fIsSafeToBlock: BOOL) -> HRESULT {
            let count = self.jit_event_count.fetch_add(1, Ordering::Relaxed) + 1;

            if let Some(ref profiler_info) = *self.profiler_info.borrow() {
                unsafe {
                    if let Some(func_info) = method_resolver::resolve_function_name(profiler_info, functionId) {
                        // Check instrumentation match
                        let matcher_borrow = self.matcher.borrow();
                        if let Some(ref matcher) = *matcher_borrow {
                            if matcher.matches(&func_info.assembly_name, &func_info.type_name, &func_info.method_name) {
                                info!(
                                    "MATCH: [{}] {}.{} — requesting ReJIT",
                                    func_info.assembly_name, func_info.type_name,
                                    func_info.method_name
                                );
                                let mut module_ids = [func_info.module_id];
                                let mut method_ids = [func_info.method_token];
                                let hr = profiler_info.RequestReJIT(
                                    1,
                                    module_ids.as_mut_ptr(),
                                    method_ids.as_mut_ptr(),
                                );
                                if failed(hr) {
                                    error!(
                                        "RequestReJIT failed: 0x{:08x} for {}.{}",
                                        hr, func_info.type_name, func_info.method_name
                                    );
                                }
                            }
                        }

                        // Periodic logging for visibility
                        if count <= 10 || count % 500 == 0 {
                            trace!(
                                "JIT #{}: [{}] {}.{}",
                                count, func_info.assembly_name, func_info.type_name, func_info.method_name
                            );
                        }
                    }
                }
            }

            S_OK
        }

        pub fn JITCompilationFinished(&self, functionId: FunctionID, hrStatus: HRESULT, fIsSafeToBlock: BOOL) -> HRESULT { S_OK }
        pub fn JITCachedFunctionSearchStarted(&self, functionId: FunctionID, pbUseCachedFunction: *mut BOOL) -> HRESULT { S_OK }
        pub fn JITCachedFunctionSearchFinished(&self, functionId: FunctionID, result: COR_PRF_JIT_CACHE) -> HRESULT { S_OK }
        pub fn JITFunctionPitched(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn JITInlining(&self, callerId: FunctionID, calleeId: FunctionID, pfShouldInline: *mut BOOL) -> HRESULT { S_OK }
        pub fn ThreadCreated(&self, threadId: ThreadID) -> HRESULT { S_OK }
        pub fn ThreadDestroyed(&self, threadId: ThreadID) -> HRESULT { S_OK }
        pub fn ThreadAssignedToOSThread(&self, managedThreadId: ThreadID, osThreadId: DWORD) -> HRESULT { S_OK }
        pub fn RemotingClientInvocationStarted(&self) -> HRESULT { S_OK }
        pub fn RemotingClientSendingMessage(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT { S_OK }
        pub fn RemotingClientReceivingReply(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT { S_OK }
        pub fn RemotingClientInvocationFinished(&self) -> HRESULT { S_OK }
        pub fn RemotingServerReceivingMessage(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT { S_OK }
        pub fn RemotingServerInvocationStarted(&self) -> HRESULT { S_OK }
        pub fn RemotingServerInvocationReturned(&self) -> HRESULT { S_OK }
        pub fn RemotingServerSendingReply(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT { S_OK }
        pub fn UnmanagedToManagedTransition(&self, functionId: FunctionID, reason: COR_PRF_TRANSITION_REASON) -> HRESULT { S_OK }
        pub fn ManagedToUnmanagedTransition(&self, functionId: FunctionID, reason: COR_PRF_TRANSITION_REASON) -> HRESULT { S_OK }
        pub fn RuntimeSuspendStarted(&self, suspendReason: COR_PRF_SUSPEND_REASON) -> HRESULT { S_OK }
        pub fn RuntimeSuspendFinished(&self) -> HRESULT { S_OK }
        pub fn RuntimeSuspendAborted(&self) -> HRESULT { S_OK }
        pub fn RuntimeResumeStarted(&self) -> HRESULT { S_OK }
        pub fn RuntimeResumeFinished(&self) -> HRESULT { S_OK }
        pub fn RuntimeThreadSuspended(&self, threadId: ThreadID) -> HRESULT { S_OK }
        pub fn RuntimeThreadResumed(&self, threadId: ThreadID) -> HRESULT { S_OK }
        pub fn MovedReferences(&self, cMovedObjectIDRanges: ULONG, oldObjectIDRangeStart: *const ObjectID, newObjectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const ULONG) -> HRESULT { S_OK }
        pub fn ObjectAllocated(&self, objectId: ObjectID, classId: ClassID) -> HRESULT { S_OK }
        pub fn ObjectsAllocatedByClass(&self, cClassCount: ULONG, classIds: *const ClassID, cObjects: *const ULONG) -> HRESULT { S_OK }
        pub fn ObjectReferences(&self, objectId: ObjectID, classId: ClassID, cObjectRefs: ULONG, objectRefIds: *const ObjectID) -> HRESULT { S_OK }
        pub fn RootReferences(&self, cRootRefs: ULONG, rootRefIds: *const ObjectID) -> HRESULT { S_OK }
        pub fn ExceptionThrown(&self, thrownObjectId: ObjectID) -> HRESULT { S_OK }
        pub fn ExceptionSearchFunctionEnter(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn ExceptionSearchFunctionLeave(&self) -> HRESULT { S_OK }
        pub fn ExceptionSearchFilterEnter(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn ExceptionSearchFilterLeave(&self) -> HRESULT { S_OK }
        pub fn ExceptionSearchCatcherFound(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn ExceptionOSHandlerEnter(&self, __unused: UINT_PTR) -> HRESULT { S_OK }
        pub fn ExceptionOSHandlerLeave(&self, __unused: UINT_PTR) -> HRESULT { S_OK }
        pub fn ExceptionUnwindFunctionEnter(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn ExceptionUnwindFunctionLeave(&self) -> HRESULT { S_OK }
        pub fn ExceptionUnwindFinallyEnter(&self, functionId: FunctionID) -> HRESULT { S_OK }
        pub fn ExceptionUnwindFinallyLeave(&self) -> HRESULT { S_OK }
        pub fn ExceptionCatcherEnter(&self, functionId: FunctionID, objectId: ObjectID) -> HRESULT { S_OK }
        pub fn ExceptionCatcherLeave(&self) -> HRESULT { S_OK }
        pub fn COMClassicVTableCreated(&self, wrappedClassId: ClassID, implementedIID: REFGUID, pVTable: *const c_void, cSlots: ULONG) -> HRESULT { S_OK }
        pub fn COMClassicVTableDestroyed(&self, wrappedClassId: ClassID, implementedIID: REFGUID, pVTable: *const c_void) -> HRESULT { S_OK }
        pub fn ExceptionCLRCatcherFound(&self) -> HRESULT { S_OK }
        pub fn ExceptionCLRCatcherExecute(&self) -> HRESULT { S_OK }
    }

    // ==================== ICorProfilerCallback2 ====================

    impl ICorProfilerCallback2 for NewRelicProfiler {
        pub fn ThreadNameChanged(&self, threadId: ThreadID, cchName: ULONG, name: *const WCHAR) -> HRESULT { S_OK }
        pub fn GarbageCollectionStarted(&self, cGenerations: i32, generationCollected: *const BOOL, reason: COR_PRF_GC_REASON) -> HRESULT { S_OK }
        pub fn SurvivingReferences(&self, cSurvivingObjectIDRanges: ULONG, objectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const ULONG) -> HRESULT { S_OK }
        pub fn GarbageCollectionFinished(&self) -> HRESULT { S_OK }
        pub fn FinalizeableObjectQueued(&self, finalizerFlags: DWORD, objectID: ObjectID) -> HRESULT { S_OK }
        pub fn RootReferences2(&self, cRootRefs: ULONG, rootRefIds: *const ObjectID, rootKinds: *const COR_PRF_GC_ROOT_KIND, rootFlags: *const COR_PRF_GC_ROOT_FLAGS, rootIds: *const UINT_PTR) -> HRESULT { S_OK }
        pub fn HandleCreated(&self, handleId: GCHandleID, initialObjectId: ObjectID) -> HRESULT { S_OK }
        pub fn HandleDestroyed(&self, handleId: GCHandleID) -> HRESULT { S_OK }
    }

    // ==================== ICorProfilerCallback3 ====================

    impl ICorProfilerCallback3 for NewRelicProfiler {
        pub fn InitializeForAttach(&self, pCorProfilerInfoUnk: IUnknown, pvClientData: *const c_void, cbClientData: UINT) -> HRESULT { S_OK }
        pub fn ProfilerAttachComplete(&self) -> HRESULT { S_OK }
        pub fn ProfilerDetachSucceeded(&self) -> HRESULT { S_OK }
    }

    // ==================== ICorProfilerCallback4 ====================

    impl ICorProfilerCallback4 for NewRelicProfiler {
        pub fn ReJITCompilationStarted(&self, functionId: FunctionID, rejitId: ReJITID, fIsSafeToBlock: BOOL) -> HRESULT {
            info!("ReJITCompilationStarted: FunctionID=0x{:x}, ReJITID={}", functionId, rejitId);
            S_OK
        }

        pub fn GetReJITParameters(&self, moduleId: ModuleID, methodId: mdMethodDef, pFunctionControl: *const c_void) -> HRESULT {
            info!("GetReJITParameters: ModuleID=0x{:x}, MethodDef=0x{:x}", moduleId, methodId);

            unsafe {
                if pFunctionControl.is_null() {
                    error!("GetReJITParameters: pFunctionControl is null");
                    return S_OK;
                }

                // Get the ICorProfilerFunctionControl interface
                let function_control: ICorProfilerFunctionControl =
                    std::mem::transmute(pFunctionControl);

                // Get the original IL body
                if let Some(ref profiler_info) = *self.profiler_info.borrow() {
                    let mut il_header: LPCBYTE = std::ptr::null();
                    let mut il_size: ULONG = 0;

                    let hr = profiler_info.GetILFunctionBody(
                        moduleId,
                        methodId,
                        &mut il_header,
                        &mut il_size,
                    );
                    if failed(hr) || il_header.is_null() || il_size == 0 {
                        error!("GetILFunctionBody failed: 0x{:08x}", hr);
                        return S_OK;
                    }

                    let original_il = std::slice::from_raw_parts(il_header, il_size as usize);
                    info!("  Original IL: {} bytes", il_size);

                    // Try real IL injection; fall back to identity rewrite on error
                    let is_core_clr = self.is_core_clr.load(Ordering::Relaxed);
                    let matcher_borrow = self.matcher.borrow();
                    match try_inject_il(profiler_info, moduleId, methodId, original_il, is_core_clr, matcher_borrow.as_ref()) {
                        Ok(instrumented_il) => {
                            info!(
                                "  IL injection successful: {} → {} bytes",
                                il_size,
                                instrumented_il.len()
                            );

                            // Dump IL to files and log for debugging
                            dump_il_hex("ORIGINAL", original_il);
                            dump_il_hex("INJECTED", &instrumented_il);
                            dump_il_to_files(profiler_info, moduleId, methodId, original_il, &instrumented_il);

                            let hr = function_control.SetILFunctionBody(
                                instrumented_il.len() as ULONG,
                                instrumented_il.as_ptr(),
                            );
                            if failed(hr) {
                                error!("SetILFunctionBody (injected) failed: 0x{:08x}", hr);
                                // Fall back to identity rewrite
                                let _ = function_control.SetILFunctionBody(il_size, il_header);
                            }
                        }
                        Err(e) => {
                            warn!("  IL injection failed, using identity rewrite: {}", e);
                            let hr = function_control.SetILFunctionBody(il_size, il_header);
                            if failed(hr) {
                                error!("SetILFunctionBody (identity) failed: 0x{:08x}", hr);
                            }
                        }
                    }
                }
            }

            S_OK
        }

        pub fn ReJITCompilationFinished(&self, functionId: FunctionID, rejitId: ReJITID, hrStatus: HRESULT, fIsSafeToBlock: BOOL) -> HRESULT { S_OK }
        pub fn ReJITError(&self, moduleId: ModuleID, methodId: mdMethodDef, functionId: FunctionID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn MovedReferences2(&self, cMovedObjectIDRanges: ULONG, oldObjectIDRangeStart: *const ObjectID, newObjectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT { S_OK }
        pub fn SurvivingReferences2(&self, cSurvivingObjectIDRanges: ULONG, objectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT { S_OK }
    }
}

/// Dump IL bytes as hex for debugging.
fn dump_il_hex(label: &str, bytes: &[u8]) {
    let hex: Vec<String> = bytes.iter().map(|b| format!("{:02X}", b)).collect();
    // Print in rows of 16
    info!("  {} IL ({} bytes):", label, bytes.len());
    for (i, chunk) in hex.chunks(16).enumerate() {
        info!("    {:04X}: {}", i * 16, chunk.join(" "));
    }
    // Also parse and dump the header
    if bytes.len() >= 12 {
        let flags_size = u16::from_le_bytes([bytes[0], bytes[1]]);
        let flags = flags_size & 0x0FFF;
        let size = (flags_size >> 12) & 0x0F;
        let max_stack = u16::from_le_bytes([bytes[2], bytes[3]]);
        let code_size = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]);
        let local_sig = u32::from_le_bytes([bytes[8], bytes[9], bytes[10], bytes[11]]);
        if (flags & 0x3) == 0x3 {
            info!(
                "  {} Header: flags=0x{:04X} size={} max_stack={} code_size={} local_sig=0x{:08X}",
                label, flags, size, max_stack, code_size, local_sig
            );
        }
    }
}

/// Dump IL bytes to .bin files when NEW_RELIC_PROFILER_DUMP_IL=1 is set.
///
/// Files are written to the current working directory with the pattern:
/// `TypeName.MethodName.original.bin` and `TypeName.MethodName.rust-instrumented.bin`
///
/// The ".rust-instrumented" suffix distinguishes from the C++ profiler's
/// ".instrumented" suffix so both can be collected from the same directory.
unsafe fn dump_il_to_files(
    profiler_info: &ICorProfilerInfo4,
    module_id: ModuleID,
    method_id: mdMethodDef,
    original_il: &[u8],
    instrumented_il: &[u8],
) {
    use std::sync::Once;
    static mut DUMP_ENABLED: bool = false;
    static INIT: Once = Once::new();
    INIT.call_once(|| {
        if let Ok(val) = std::env::var("NEW_RELIC_PROFILER_DUMP_IL") {
            unsafe { DUMP_ENABLED = val == "1"; }
        }
    });
    if !DUMP_ENABLED {
        return;
    }

    // Resolve type and method names for the file name
    let iid_metadata_import: com::sys::GUID = com::sys::GUID {
        data1: 0x7DAC8207, data2: 0xD3AE, data3: 0x4C75,
        data4: [0x9B, 0x67, 0x92, 0x80, 0x1A, 0x49, 0x7D, 0x44],
    };
    let mut import_ptr: *mut IUnknown = std::ptr::null_mut();
    let hr = profiler_info.GetModuleMetaData(module_id, OF_READ, &iid_metadata_import, &mut import_ptr);
    if failed(hr) || import_ptr.is_null() {
        return;
    }
    let metadata_import: crate::metadata_import::IMetaDataImport = std::mem::transmute(import_ptr);

    let mut type_def: mdTypeDef = 0;
    let mut method_name_buf = [0u16; 256];
    let hr = metadata_import.GetMethodProps(
        method_id, &mut type_def,
        method_name_buf.as_mut_ptr(), 256, std::ptr::null_mut(),
        std::ptr::null_mut(), std::ptr::null_mut(), std::ptr::null_mut(),
        std::ptr::null_mut(), std::ptr::null_mut(),
    );
    if failed(hr) { return; }

    let mut type_name_buf = [0u16; 256];
    let _hr = metadata_import.GetTypeDefProps(
        type_def, type_name_buf.as_mut_ptr(), 256, std::ptr::null_mut(),
        std::ptr::null_mut(), std::ptr::null_mut(),
    );

    let type_name = crate::method_resolver::wchar_to_string(&type_name_buf);
    let method_name = crate::method_resolver::wchar_to_string(&method_name_buf);
    let base_name = format!("{}.{}", type_name, method_name);

    // Write original IL
    if let Ok(mut f) = std::fs::File::create(format!("{}.original.bin", base_name)) {
        use std::io::Write;
        let _ = f.write_all(original_il);
        info!("  Dumped original IL to {}.original.bin", base_name);
    }

    // Write Rust-instrumented IL
    if let Ok(mut f) = std::fs::File::create(format!("{}.rust-instrumented.bin", base_name)) {
        use std::io::Write;
        let _ = f.write_all(instrumented_il);
        info!("  Dumped instrumented IL to {}.rust-instrumented.bin", base_name);
    }
}

/// Attempt real IL injection for a method being re-JIT compiled.
///
/// Obtains the metadata emit interfaces, resolves method signature and
/// instrumentation config, then calls `build_instrumented_method`.
///
/// Returns the instrumented IL bytes on success, or an error message on failure.
/// The caller should fall back to identity rewrite on error.
unsafe fn try_inject_il(
    profiler_info: &ICorProfilerInfo4,
    module_id: ModuleID,
    method_id: mdMethodDef,
    original_il: &[u8],
    is_core_clr: bool,
    matcher: Option<&InstrumentationMatcher>,
) -> Result<Vec<u8>, crate::il::IlError> {
    use crate::il::IlError;

    // IID constants for QueryInterface
    let iid_metadata_emit2: com::sys::GUID = com::sys::GUID {
        data1: 0xF5DD9950, data2: 0xF693, data3: 0x42E6,
        data4: [0x83, 0x0E, 0x7B, 0x83, 0x3E, 0x81, 0x46, 0xA9],
    };
    let iid_assembly_emit: com::sys::GUID = com::sys::GUID {
        data1: 0x211EF15B, data2: 0x5317, data3: 0x4438,
        data4: [0xB1, 0x96, 0xDE, 0xC8, 0x7B, 0x88, 0x76, 0x93],
    };
    let iid_assembly_import: com::sys::GUID = com::sys::GUID {
        data1: 0xEE62470B, data2: 0xE94B, data3: 0x424E,
        data4: [0x9B, 0x7C, 0x2F, 0x00, 0xC9, 0x24, 0x9F, 0x93],
    };

    // Get IMetaDataEmit2 via GetModuleMetaData
    let mut emit_ptr: *mut IUnknown = std::ptr::null_mut();
    let hr = profiler_info.GetModuleMetaData(
        module_id,
        OF_READ | OF_WRITE,
        &iid_metadata_emit2,
        &mut emit_ptr,
    );
    if failed(hr) || emit_ptr.is_null() {
        return Err(IlError::TokenResolutionFailed(format!(
            "GetModuleMetaData(IMetaDataEmit2) failed: 0x{:08x}", hr
        )));
    }
    let metadata_emit: IMetaDataEmit2 = std::mem::transmute(emit_ptr);

    // Get IMetaDataAssemblyEmit
    let mut asm_emit_ptr: *mut IUnknown = std::ptr::null_mut();
    let hr = profiler_info.GetModuleMetaData(
        module_id,
        OF_READ | OF_WRITE,
        &iid_assembly_emit,
        &mut asm_emit_ptr,
    );
    if failed(hr) || asm_emit_ptr.is_null() {
        return Err(IlError::TokenResolutionFailed(format!(
            "GetModuleMetaData(IMetaDataAssemblyEmit) failed: 0x{:08x}", hr
        )));
    }
    let metadata_assembly_emit: IMetaDataAssemblyEmit = std::mem::transmute(asm_emit_ptr);

    // Get IMetaDataAssemblyImport
    let mut asm_import_ptr: *mut IUnknown = std::ptr::null_mut();
    let hr = profiler_info.GetModuleMetaData(
        module_id,
        OF_READ,
        &iid_assembly_import,
        &mut asm_import_ptr,
    );
    if failed(hr) || asm_import_ptr.is_null() {
        return Err(IlError::TokenResolutionFailed(format!(
            "GetModuleMetaData(IMetaDataAssemblyImport) failed: 0x{:08x}", hr
        )));
    }
    let metadata_assembly_import: IMetaDataAssemblyImport = std::mem::transmute(asm_import_ptr);

    // Get method signature via IMetaDataImport (reuse GetMethodProps)
    let iid_metadata_import: com::sys::GUID = com::sys::GUID {
        data1: 0x7DAC8207, data2: 0xD3AE, data3: 0x4C75,
        data4: [0x9B, 0x67, 0x92, 0x80, 0x1A, 0x49, 0x7D, 0x44],
    };
    let mut import_ptr: *mut IUnknown = std::ptr::null_mut();
    let hr = profiler_info.GetModuleMetaData(
        module_id,
        OF_READ,
        &iid_metadata_import,
        &mut import_ptr,
    );
    if failed(hr) || import_ptr.is_null() {
        return Err(IlError::TokenResolutionFailed(format!(
            "GetModuleMetaData(IMetaDataImport) failed: 0x{:08x}", hr
        )));
    }
    let metadata_import: crate::metadata_import::IMetaDataImport = std::mem::transmute(import_ptr);

    // Get method props to retrieve signature and type token
    let mut type_def: mdTypeDef = 0;
    let mut method_name_buf = [0u16; 512];
    let mut method_name_len: ULONG = 0;
    let mut sig_blob: crate::metadata_import::PCCOR_SIGNATURE = std::ptr::null();
    let mut sig_blob_len: ULONG = 0;

    let hr = metadata_import.GetMethodProps(
        method_id,
        &mut type_def,
        method_name_buf.as_mut_ptr(),
        512,
        &mut method_name_len,
        std::ptr::null_mut(), // pdwAttr
        &mut sig_blob,
        &mut sig_blob_len,
        std::ptr::null_mut(), // pulCodeRVA
        std::ptr::null_mut(), // pdwImplFlags
    );
    if failed(hr) {
        return Err(IlError::TokenResolutionFailed(format!(
            "GetMethodProps failed: 0x{:08x}", hr
        )));
    }

    // Parse method signature
    let sig_bytes = std::slice::from_raw_parts(sig_blob, sig_blob_len as usize);
    let method_sig = method_signature::parse_method_signature(sig_bytes)?;

    // Get type name
    let mut type_name_buf = [0u16; 512];
    let mut type_name_len: ULONG = 0;
    let _hr = metadata_import.GetTypeDefProps(
        type_def,
        type_name_buf.as_mut_ptr(),
        512,
        &mut type_name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );

    let type_name = crate::method_resolver::wchar_to_string(&type_name_buf);
    let method_name = crate::method_resolver::wchar_to_string(&method_name_buf);

    // Resolve the function info for assembly name (reuse method_resolver)
    // For now, get assembly name from module
    let mut assembly_id: AssemblyID = 0;
    let _hr = profiler_info.GetModuleInfo(
        module_id,
        std::ptr::null_mut(),
        0,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        &mut assembly_id,
    );
    let mut asm_name_buf = [0u16; 512];
    let mut asm_name_len: ULONG = 0;
    let _hr = profiler_info.GetAssemblyInfo(
        assembly_id,
        512,
        &mut asm_name_len,
        asm_name_buf.as_mut_ptr(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    let assembly_name = crate::method_resolver::wchar_to_string(&asm_name_buf);

    // Look up instrumentation point
    let instr_point = match matcher {
        Some(m) => m.find_point(&assembly_name, &type_name, &method_name),
        None => None,
    };
    let instr_point = instr_point.ok_or_else(|| {
        IlError::GenerationError(format!(
            "No instrumentation point for {}.{}", type_name, method_name
        ))
    })?;

    info!(
        "  Injecting IL for [{}] {}.{} (tracer: {})",
        assembly_name, type_name, method_name, instr_point.tracer_factory_name
    );

    // Get original local variable info from the method header
    let parsed_header = crate::il::method_header::parse_method(original_il)
        .map_err(|e| IlError::InvalidHeader(format!("{}", e)))?;
    let (original_locals_signature, original_local_count) =
        if parsed_header.header.local_var_sig_tok != 0 {
            // Get the signature bytes from the metadata
            let mut sig_ptr: crate::metadata_import::PCCOR_SIGNATURE = std::ptr::null();
            let mut sig_len: ULONG = 0;
            let hr = metadata_import.GetSigFromToken(
                parsed_header.header.local_var_sig_tok,
                &mut sig_ptr,
                &mut sig_len,
            );
            if !failed(hr) && !sig_ptr.is_null() && sig_len > 0 {
                let sig_bytes = std::slice::from_raw_parts(sig_ptr, sig_len as usize).to_vec();
                // Count locals from the signature: byte 0 is 0x07 (LOCAL_SIG),
                // then compressed count
                let count = if sig_bytes.len() >= 2 {
                    let (c, _) = crate::il::sig_compression::uncompress_data(&sig_bytes[1..])
                        .unwrap_or((0, 0));
                    c as u16
                } else {
                    0
                };
                (sig_bytes, count)
            } else {
                (Vec::new(), 0)
            }
        } else {
            (Vec::new(), 0)
        };

    info!(
        "  Original locals: {} (sig_tok=0x{:08x})",
        original_local_count, parsed_header.header.local_var_sig_tok
    );

    // Create tokenizer
    let mut tokenizer = Tokenizer::new(
        metadata_emit,
        metadata_assembly_emit,
        metadata_assembly_import,
        is_core_clr,
    );

    // Build CLR method context
    let clr_ctx = inject_default::ClrMethodContext {
        original_locals_signature,
        original_local_count,
    };

    // Build instrumentation context
    let ctx = InstrumentationContext {
        assembly_name: assembly_name.clone(),
        type_name: type_name.clone(),
        method_name: method_name.clone(),
        function_id: 0, // FunctionID not available in GetReJITParameters
        type_token: type_def,
        tracer_factory_name: instr_point.tracer_factory_name.clone(),
        tracer_factory_args: instr_point.tracer_factory_args,
        metric_name: instr_point.metric_name.clone(),
        argument_signature: String::new(), // TODO: build from method signature
        method_signature: method_sig,
    };

    // Generate instrumented IL
    inject_default::build_instrumented_method(&ctx, &clr_ctx, &mut tokenizer, original_il)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_profiler_clsid_netfx() {
        // Verify GUID matches C++ profiler: {71DA0A04-7777-4EC6-9643-7D28B46A8A41}
        assert_eq!(CLSID_PROFILER_NETFX.data1, 0x71DA0A04);
        assert_eq!(CLSID_PROFILER_NETFX.data2, 0x7777);
        assert_eq!(CLSID_PROFILER_NETFX.data3, 0x4EC6);
        assert_eq!(CLSID_PROFILER_NETFX.data4, [0x96, 0x43, 0x7D, 0x28, 0xB4, 0x6A, 0x8A, 0x41]);
    }

    #[test]
    fn test_profiler_clsid_coreclr() {
        // Verify GUID matches C++ profiler: {36032161-FFC0-4B61-B559-F6C5D41BAE5A}
        assert_eq!(CLSID_PROFILER_CORECLR.data1, 0x36032161);
        assert_eq!(CLSID_PROFILER_CORECLR.data2, 0xFFC0);
        assert_eq!(CLSID_PROFILER_CORECLR.data3, 0x4B61);
        assert_eq!(CLSID_PROFILER_CORECLR.data4, [0xB5, 0x59, 0xF6, 0xC5, 0xD4, 0x1B, 0xAE, 0x5A]);
    }
}
