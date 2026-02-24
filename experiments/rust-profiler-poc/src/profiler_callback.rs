// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! New Relic .NET Agent Profiler - COM callback implementation
//!
//! This module implements the ICorProfilerCallback4 COM interface that the CLR
//! calls into when profiling events occur. The `com` crate's `class!` macro
//! handles vtable layout, reference counting, and QueryInterface automatically.

use crate::ffi::*;
use crate::interfaces::*;
use crate::profiler_info::{ICorProfilerInfo4, ICorProfilerInfo5};
use com::{
    interfaces::iunknown::IUnknown,
    sys::{GUID, HRESULT},
    Interface,
};
use log::{error, info, trace};
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
    }

    // ==================== ICorProfilerCallback ====================

    impl ICorProfilerCallback for NewRelicProfiler {
        pub fn Initialize(&self, pICorProfilerInfoUnk: IUnknown) -> HRESULT {
            info!("New Relic Rust Profiler: Initialize called");

            unsafe {
                // Query for ICorProfilerInfo4
                let mut profiler_info: Option<ICorProfilerInfo4> = None;
                let hr = pICorProfilerInfoUnk.QueryInterface(
                    &ICorProfilerInfo4::IID,
                    &mut profiler_info as *mut _ as *mut *mut c_void,
                );
                if failed(hr) || profiler_info.is_none() {
                    error!("Failed to get ICorProfilerInfo4: 0x{:08x}", hr);
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }
                let profiler_info = profiler_info.unwrap();

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
                let mut profiler_info5: Option<ICorProfilerInfo5> = None;
                let hr5 = pICorProfilerInfoUnk.QueryInterface(
                    &ICorProfilerInfo5::IID,
                    &mut profiler_info5 as *mut _ as *mut *mut c_void,
                );
                if !failed(hr5) {
                    if let Some(info5) = profiler_info5 {
                        // 0x8 = COR_PRF_HIGH_DISABLE_TIERED_COMPILATION
                        let hr = info5.SetEventMask2(event_mask, 0x8);
                        if failed(hr) {
                            error!("SetEventMask2 failed: 0x{:08x}, falling back to SetEventMask", hr);
                            let hr = profiler_info.SetEventMask(event_mask);
                            if failed(hr) {
                                error!("SetEventMask failed: 0x{:08x}", hr);
                                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                            }
                        } else {
                            info!("Event mask set via SetEventMask2 (tiered compilation disabled)");
                        }
                    }
                } else {
                    // Fall back to SetEventMask (no tiered compilation control)
                    let hr = profiler_info.SetEventMask(event_mask);
                    if failed(hr) {
                        error!("SetEventMask failed: 0x{:08x}", hr);
                        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                    }
                    info!("Event mask set via SetEventMask");
                }

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

            // Log first few events and then periodically to avoid log spam
            if count <= 10 || count % 1000 == 0 {
                trace!(
                    "JITCompilationStarted: FunctionID=0x{:x}, SafeToBlock={}, Count={}",
                    functionId, fIsSafeToBlock != 0, count
                );
            }

            // TODO: Phase 2 - Check if method should be instrumented, request ReJIT
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
            trace!("ReJITCompilationStarted: FunctionID=0x{:x}, ReJITID={}", functionId, rejitId);
            // TODO: Phase 2 - Implement IL rewriting here
            S_OK
        }

        pub fn GetReJITParameters(&self, moduleId: ModuleID, methodId: mdMethodDef, pFunctionControl: *const c_void) -> HRESULT {
            trace!("GetReJITParameters: ModuleID=0x{:x}, MethodDef=0x{:x}", moduleId, methodId);
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
