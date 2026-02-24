// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! COM interface definitions for the CLR Profiling API
//!
//! These interface definitions are derived from Microsoft's corprof.h header
//! in the .NET runtime (MIT licensed). The method names, parameter types,
//! GUIDs, and vtable ordering are all dictated by the CLR Profiling API
//! specification and must match exactly.
//!
//! The `com` crate's `interfaces!` macro is used to generate the vtable layout.
//! See: https://github.com/microsoft/com-rs

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::{GUID, HRESULT},
};
use std::ffi::c_void;

interfaces! {
    /// ICorProfilerCallback - base profiler callback interface
    /// GUID: 176FBED1-A55C-4796-98CA-A9DA0EF883E7
    #[uuid("176FBED1-A55C-4796-98CA-A9DA0EF883E7")]
    pub unsafe interface ICorProfilerCallback: IUnknown {
        pub fn Initialize(&self, pICorProfilerInfoUnk: IUnknown) -> HRESULT;
        pub fn Shutdown(&self) -> HRESULT;
        pub fn AppDomainCreationStarted(&self, appDomainId: AppDomainID) -> HRESULT;
        pub fn AppDomainCreationFinished(&self, appDomainId: AppDomainID, hrStatus: HRESULT) -> HRESULT;
        pub fn AppDomainShutdownStarted(&self, appDomainId: AppDomainID) -> HRESULT;
        pub fn AppDomainShutdownFinished(&self, appDomainId: AppDomainID, hrStatus: HRESULT) -> HRESULT;
        pub fn AssemblyLoadStarted(&self, assemblyId: AssemblyID) -> HRESULT;
        pub fn AssemblyLoadFinished(&self, assemblyId: AssemblyID, hrStatus: HRESULT) -> HRESULT;
        pub fn AssemblyUnloadStarted(&self, assemblyId: AssemblyID) -> HRESULT;
        pub fn AssemblyUnloadFinished(&self, assemblyId: AssemblyID, hrStatus: HRESULT) -> HRESULT;
        pub fn ModuleLoadStarted(&self, moduleId: ModuleID) -> HRESULT;
        pub fn ModuleLoadFinished(&self, moduleId: ModuleID, hrStatus: HRESULT) -> HRESULT;
        pub fn ModuleUnloadStarted(&self, moduleId: ModuleID) -> HRESULT;
        pub fn ModuleUnloadFinished(&self, moduleId: ModuleID, hrStatus: HRESULT) -> HRESULT;
        pub fn ModuleAttachedToAssembly(&self, moduleId: ModuleID, AssemblyId: AssemblyID) -> HRESULT;
        pub fn ClassLoadStarted(&self, classId: ClassID) -> HRESULT;
        pub fn ClassLoadFinished(&self, classId: ClassID, hrStatus: HRESULT) -> HRESULT;
        pub fn ClassUnloadStarted(&self, classId: ClassID) -> HRESULT;
        pub fn ClassUnloadFinished(&self, classId: ClassID, hrStatus: HRESULT) -> HRESULT;
        pub fn FunctionUnloadStarted(&self, functionId: FunctionID) -> HRESULT;
        pub fn JITCompilationStarted(&self, functionId: FunctionID, fIsSafeToBlock: BOOL) -> HRESULT;
        pub fn JITCompilationFinished(&self, functionId: FunctionID, hrStatus: HRESULT, fIsSafeToBlock: BOOL) -> HRESULT;
        pub fn JITCachedFunctionSearchStarted(&self, functionId: FunctionID, pbUseCachedFunction: *mut BOOL) -> HRESULT;
        pub fn JITCachedFunctionSearchFinished(&self, functionId: FunctionID, result: COR_PRF_JIT_CACHE) -> HRESULT;
        pub fn JITFunctionPitched(&self, functionId: FunctionID) -> HRESULT;
        pub fn JITInlining(&self, callerId: FunctionID, calleeId: FunctionID, pfShouldInline: *mut BOOL) -> HRESULT;
        pub fn ThreadCreated(&self, threadId: ThreadID) -> HRESULT;
        pub fn ThreadDestroyed(&self, threadId: ThreadID) -> HRESULT;
        pub fn ThreadAssignedToOSThread(&self, managedThreadId: ThreadID, osThreadId: DWORD) -> HRESULT;
        pub fn RemotingClientInvocationStarted(&self) -> HRESULT;
        pub fn RemotingClientSendingMessage(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT;
        pub fn RemotingClientReceivingReply(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT;
        pub fn RemotingClientInvocationFinished(&self) -> HRESULT;
        pub fn RemotingServerReceivingMessage(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT;
        pub fn RemotingServerInvocationStarted(&self) -> HRESULT;
        pub fn RemotingServerInvocationReturned(&self) -> HRESULT;
        pub fn RemotingServerSendingReply(&self, pCookie: *const GUID, fIsAsync: BOOL) -> HRESULT;
        pub fn UnmanagedToManagedTransition(&self, functionId: FunctionID, reason: COR_PRF_TRANSITION_REASON) -> HRESULT;
        pub fn ManagedToUnmanagedTransition(&self, functionId: FunctionID, reason: COR_PRF_TRANSITION_REASON) -> HRESULT;
        pub fn RuntimeSuspendStarted(&self, suspendReason: COR_PRF_SUSPEND_REASON) -> HRESULT;
        pub fn RuntimeSuspendFinished(&self) -> HRESULT;
        pub fn RuntimeSuspendAborted(&self) -> HRESULT;
        pub fn RuntimeResumeStarted(&self) -> HRESULT;
        pub fn RuntimeResumeFinished(&self) -> HRESULT;
        pub fn RuntimeThreadSuspended(&self, threadId: ThreadID) -> HRESULT;
        pub fn RuntimeThreadResumed(&self, threadId: ThreadID) -> HRESULT;
        pub fn MovedReferences(&self, cMovedObjectIDRanges: ULONG, oldObjectIDRangeStart: *const ObjectID, newObjectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const ULONG) -> HRESULT;
        pub fn ObjectAllocated(&self, objectId: ObjectID, classId: ClassID) -> HRESULT;
        pub fn ObjectsAllocatedByClass(&self, cClassCount: ULONG, classIds: *const ClassID, cObjects: *const ULONG) -> HRESULT;
        pub fn ObjectReferences(&self, objectId: ObjectID, classId: ClassID, cObjectRefs: ULONG, objectRefIds: *const ObjectID) -> HRESULT;
        pub fn RootReferences(&self, cRootRefs: ULONG, rootRefIds: *const ObjectID) -> HRESULT;
        pub fn ExceptionThrown(&self, thrownObjectId: ObjectID) -> HRESULT;
        pub fn ExceptionSearchFunctionEnter(&self, functionId: FunctionID) -> HRESULT;
        pub fn ExceptionSearchFunctionLeave(&self) -> HRESULT;
        pub fn ExceptionSearchFilterEnter(&self, functionId: FunctionID) -> HRESULT;
        pub fn ExceptionSearchFilterLeave(&self) -> HRESULT;
        pub fn ExceptionSearchCatcherFound(&self, functionId: FunctionID) -> HRESULT;
        pub fn ExceptionOSHandlerEnter(&self, __unused: UINT_PTR) -> HRESULT;
        pub fn ExceptionOSHandlerLeave(&self, __unused: UINT_PTR) -> HRESULT;
        pub fn ExceptionUnwindFunctionEnter(&self, functionId: FunctionID) -> HRESULT;
        pub fn ExceptionUnwindFunctionLeave(&self) -> HRESULT;
        pub fn ExceptionUnwindFinallyEnter(&self, functionId: FunctionID) -> HRESULT;
        pub fn ExceptionUnwindFinallyLeave(&self) -> HRESULT;
        pub fn ExceptionCatcherEnter(&self, functionId: FunctionID, objectId: ObjectID) -> HRESULT;
        pub fn ExceptionCatcherLeave(&self) -> HRESULT;
        pub fn COMClassicVTableCreated(&self, wrappedClassId: ClassID, implementedIID: REFGUID, pVTable: *const c_void, cSlots: ULONG) -> HRESULT;
        pub fn COMClassicVTableDestroyed(&self, wrappedClassId: ClassID, implementedIID: REFGUID, pVTable: *const c_void) -> HRESULT;
        pub fn ExceptionCLRCatcherFound(&self) -> HRESULT;
        pub fn ExceptionCLRCatcherExecute(&self) -> HRESULT;
    }

    /// ICorProfilerCallback2
    /// GUID: 8A8CC829-CCF2-49FE-BBAE-0F022228071A
    #[uuid("8A8CC829-CCF2-49FE-BBAE-0F022228071A")]
    pub unsafe interface ICorProfilerCallback2: ICorProfilerCallback {
        pub fn ThreadNameChanged(&self, threadId: ThreadID, cchName: ULONG, name: *const WCHAR) -> HRESULT;
        pub fn GarbageCollectionStarted(&self, cGenerations: i32, generationCollected: *const BOOL, reason: COR_PRF_GC_REASON) -> HRESULT;
        pub fn SurvivingReferences(&self, cSurvivingObjectIDRanges: ULONG, objectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const ULONG) -> HRESULT;
        pub fn GarbageCollectionFinished(&self) -> HRESULT;
        pub fn FinalizeableObjectQueued(&self, finalizerFlags: DWORD, objectID: ObjectID) -> HRESULT;
        pub fn RootReferences2(&self, cRootRefs: ULONG, rootRefIds: *const ObjectID, rootKinds: *const COR_PRF_GC_ROOT_KIND, rootFlags: *const COR_PRF_GC_ROOT_FLAGS, rootIds: *const UINT_PTR) -> HRESULT;
        pub fn HandleCreated(&self, handleId: GCHandleID, initialObjectId: ObjectID) -> HRESULT;
        pub fn HandleDestroyed(&self, handleId: GCHandleID) -> HRESULT;
    }

    /// ICorProfilerCallback3
    /// GUID: 4FD2ED52-7731-4B8D-9469-03D2CC3086C5
    #[uuid("4FD2ED52-7731-4B8D-9469-03D2CC3086C5")]
    pub unsafe interface ICorProfilerCallback3: ICorProfilerCallback2 {
        pub fn InitializeForAttach(&self, pCorProfilerInfoUnk: IUnknown, pvClientData: *const c_void, cbClientData: UINT) -> HRESULT;
        pub fn ProfilerAttachComplete(&self) -> HRESULT;
        pub fn ProfilerDetachSucceeded(&self) -> HRESULT;
    }

    /// ICorProfilerCallback4
    /// GUID: 7B63B2E3-107D-4D48-B2F6-F61E229470D2
    #[uuid("7B63B2E3-107D-4D48-B2F6-F61E229470D2")]
    pub unsafe interface ICorProfilerCallback4: ICorProfilerCallback3 {
        pub fn ReJITCompilationStarted(&self, functionId: FunctionID, rejitId: ReJITID, fIsSafeToBlock: BOOL) -> HRESULT;
        pub fn GetReJITParameters(&self, moduleId: ModuleID, methodId: mdMethodDef, pFunctionControl: *const c_void) -> HRESULT;
        pub fn ReJITCompilationFinished(&self, functionId: FunctionID, rejitId: ReJITID, hrStatus: HRESULT, fIsSafeToBlock: BOOL) -> HRESULT;
        pub fn ReJITError(&self, moduleId: ModuleID, methodId: mdMethodDef, functionId: FunctionID, hrStatus: HRESULT) -> HRESULT;
        pub fn MovedReferences2(&self, cMovedObjectIDRanges: ULONG, oldObjectIDRangeStart: *const ObjectID, newObjectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT;
        pub fn SurvivingReferences2(&self, cSurvivingObjectIDRanges: ULONG, objectIDRangeStart: *const ObjectID, cObjectIDRangeLength: *const SIZE_T) -> HRESULT;
    }
}
