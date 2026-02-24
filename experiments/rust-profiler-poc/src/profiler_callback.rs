// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! New Relic .NET Agent Profiler - COM callback implementation
//!
//! This module implements the ICorProfilerCallback4 COM interface that the CLR
//! calls into when profiling events occur. The `com` crate's `class!` macro
//! handles vtable layout, reference counting, and QueryInterface automatically.

use crate::ffi::*;
use crate::instrumentation::InstrumentationMatcher;
use crate::interfaces::*;
use crate::method_resolver;
use crate::profiler_info::{ICorProfilerInfo4, ICorProfilerInfo5};
use com::{
    interfaces::iunknown::IUnknown,
    sys::{GUID, HRESULT},
};
use log::{error, info, trace};
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
            // TODO: This is where IL rewriting will happen.
            // The pFunctionControl parameter has SetILFunctionBody to inject modified IL.
            S_OK
        }

        pub fn ReJITCompilationFinished(&self, functionId: FunctionID, rejitId: ReJITID, hrStatus: HRESULT, fIsSafeToBlock: BOOL) -> HRESULT { S_OK }
        pub fn ReJITError(&self, moduleId: ModuleID, methodId: mdMethodDef, functionId: FunctionID, hrStatus: HRESULT) -> HRESULT { S_OK }
        pub fn MovedReferences2(&self, cMovedObjectIDRanges: ULONG, oldObjectIDRangeStart: *const ObjectID, newObjectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT { S_OK }
        pub fn SurvivingReferences2(&self, cSurvivingObjectIDRanges: ULONG, objectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT { S_OK }
    }
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
