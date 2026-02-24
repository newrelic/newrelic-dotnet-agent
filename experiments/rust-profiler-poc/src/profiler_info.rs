// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! ICorProfilerInfo COM interface definitions
//!
//! These interface definitions are derived from Microsoft's corprof.h header
//! in the .NET runtime (MIT licensed). The method names and vtable ordering
//! are dictated by the CLR Profiling API specification and must match exactly.
//!
//! For methods we don't call in the POC, complex parameter types (structs,
//! function pointers, enum pointers) are replaced with opaque `*const c_void`
//! or `*mut c_void`. This is ABI-compatible since all are pointer-sized.
//! These should be replaced with proper types as the implementation matures.

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::{GUID, HRESULT},
};
use std::ffi::c_void;

interfaces! {
    // ==================== ICorProfilerInfo ====================
    // 33 methods. GUID from corprof.idl.

    #[uuid("28B5557D-3F3F-48b4-90B2-5F9EEA2F6C48")]
    pub unsafe interface ICorProfilerInfo: IUnknown {
        // 1
        pub fn GetClassFromObject(&self, objectId: ObjectID, pClassId: *mut ClassID) -> HRESULT;
        // 2
        pub fn GetClassFromToken(&self, moduleId: ModuleID, typeDef: mdTypeDef, pClassId: *mut ClassID) -> HRESULT;
        // 3
        pub fn GetCodeInfo(&self, functionId: FunctionID, pStart: *mut LPCBYTE, pcSize: *mut ULONG) -> HRESULT;
        // 4
        pub fn GetEventMask(&self, pdwEvents: *mut DWORD) -> HRESULT;
        // 5
        pub fn GetFunctionFromIP(&self, ip: LPCBYTE, pFunctionId: *mut FunctionID) -> HRESULT;
        // 6
        pub fn GetFunctionFromToken(&self, moduleId: ModuleID, token: mdToken, pFunctionId: *mut FunctionID) -> HRESULT;
        // 7
        pub fn GetHandleFromThread(&self, threadId: ThreadID, phThread: *mut HANDLE) -> HRESULT;
        // 8
        pub fn GetObjectSize(&self, objectId: ObjectID, pcSize: *mut ULONG) -> HRESULT;
        // 9
        pub fn IsArrayClass(&self, classId: ClassID, pBaseElemType: *mut u32, pBaseClassId: *mut ClassID, pcRank: *mut ULONG) -> HRESULT;
        // 10
        pub fn GetThreadInfo(&self, threadId: ThreadID, pdwWin32ThreadId: *mut DWORD) -> HRESULT;
        // 11
        pub fn GetCurrentThreadID(&self, pThreadId: *mut ThreadID) -> HRESULT;
        // 12
        pub fn GetClassIDInfo(&self, classId: ClassID, pModuleId: *mut ModuleID, pTypeDefToken: *mut mdTypeDef) -> HRESULT;
        // 13
        pub fn GetFunctionInfo(&self, functionId: FunctionID, pClassId: *mut ClassID, pModuleId: *mut ModuleID, pToken: *mut mdToken) -> HRESULT;
        // 14 ** Used in Initialize **
        pub fn SetEventMask(&self, dwEvents: DWORD) -> HRESULT;
        // 15
        pub fn SetEnterLeaveFunctionHooks(&self, pFuncEnter: *const c_void, pFuncLeave: *const c_void, pFuncTailcall: *const c_void) -> HRESULT;
        // 16
        pub fn SetFunctionIDMapper(&self, pFunc: *const c_void) -> HRESULT;
        // 17
        pub fn GetTokenAndMetaDataFromFunction(&self, functionId: FunctionID, riid: REFIID, ppImport: *mut *mut IUnknown, pToken: *mut mdToken) -> HRESULT;
        // 18
        pub fn GetModuleInfo(&self, moduleId: ModuleID, ppBaseLoadAddress: *mut LPCBYTE, cchName: ULONG, pcchName: *mut ULONG, szName: *mut WCHAR, pAssemblyId: *mut AssemblyID) -> HRESULT;
        // 19
        pub fn GetModuleMetaData(&self, moduleId: ModuleID, dwOpenFlags: DWORD, riid: REFIID, ppOut: *mut *mut IUnknown) -> HRESULT;
        // 20
        pub fn GetILFunctionBody(&self, moduleId: ModuleID, methodId: mdMethodDef, ppMethodHeader: *mut LPCBYTE, pcbMethodSize: *mut ULONG) -> HRESULT;
        // 21
        pub fn GetILFunctionBodyAllocator(&self, moduleId: ModuleID, ppMalloc: *mut *mut c_void) -> HRESULT;
        // 22
        pub fn SetILFunctionBody(&self, moduleId: ModuleID, methodid: mdMethodDef, pbNewILMethodHeader: LPCBYTE) -> HRESULT;
        // 23
        pub fn GetAppDomainInfo(&self, appDomainId: AppDomainID, cchName: ULONG, pcchName: *mut ULONG, szName: *mut WCHAR, pProcessId: *mut ProcessID) -> HRESULT;
        // 24
        pub fn GetAssemblyInfo(&self, assemblyId: AssemblyID, cchName: ULONG, pcchName: *mut ULONG, szName: *mut WCHAR, pAppDomainId: *mut AppDomainID, pModuleId: *mut ModuleID) -> HRESULT;
        // 25
        pub fn SetFunctionReJIT(&self, functionId: FunctionID) -> HRESULT;
        // 26
        pub fn ForceGC(&self) -> HRESULT;
        // 27
        pub fn SetILInstrumentedCodeMap(&self, functionId: FunctionID, fStartJit: BOOL, cILMapEntries: ULONG, rgILMapEntries: *const c_void) -> HRESULT;
        // 28
        pub fn GetInprocInspectionInterface(&self, ppicd: *mut *mut IUnknown) -> HRESULT;
        // 29
        pub fn GetInprocInspectionIThisThread(&self, ppicd: *mut *mut IUnknown) -> HRESULT;
        // 30
        pub fn GetThreadContext(&self, threadId: ThreadID, pContextId: *mut ContextID) -> HRESULT;
        // 31
        pub fn BeginInprocDebugging(&self, fThisThreadOnly: BOOL, pdwProfilerContext: *mut DWORD) -> HRESULT;
        // 32
        pub fn EndInprocDebugging(&self, dwProfilerContext: DWORD) -> HRESULT;
        // 33
        pub fn GetILToNativeMapping(&self, functionId: FunctionID, cMap: ULONG32, pcMap: *mut ULONG32, map: *mut c_void) -> HRESULT;
    }

    // ==================== ICorProfilerInfo2 ====================
    // 21 methods.

    #[uuid("CC0935CD-A518-487d-B0BB-A93214E65478")]
    pub unsafe interface ICorProfilerInfo2: ICorProfilerInfo {
        // 1
        pub fn DoStackSnapshot(&self, thread: ThreadID, callback: *const c_void, infoFlags: ULONG32, clientData: *const c_void, context: *const BYTE, contextSize: ULONG32) -> HRESULT;
        // 2
        pub fn SetEnterLeaveFunctionHooks2(&self, pFuncEnter: *const c_void, pFuncLeave: *const c_void, pFuncTailcall: *const c_void) -> HRESULT;
        // 3
        pub fn GetFunctionInfo2(&self, funcId: FunctionID, frameInfo: COR_PRF_FRAME_INFO, pClassId: *mut ClassID, pModuleId: *mut ModuleID, pToken: *mut mdToken, cTypeArgs: ULONG32, pcTypeArgs: *mut ULONG32, typeArgs: *mut ClassID) -> HRESULT;
        // 4
        pub fn GetStringLayout(&self, pBufferLengthOffset: *mut ULONG, pStringLengthOffset: *mut ULONG, pBufferOffset: *mut ULONG) -> HRESULT;
        // 5
        pub fn GetClassLayout(&self, classID: ClassID, rFieldOffset: *mut c_void, cFieldOffset: ULONG, pcFieldOffset: *mut ULONG, pulClassSize: *mut ULONG) -> HRESULT;
        // 6
        pub fn GetClassIDInfo2(&self, classId: ClassID, pModuleId: *mut ModuleID, pTypeDefToken: *mut mdTypeDef, pParentClassId: *mut ClassID, cNumTypeArgs: ULONG32, pcNumTypeArgs: *mut ULONG32, typeArgs: *mut ClassID) -> HRESULT;
        // 7
        pub fn GetCodeInfo2(&self, functionID: FunctionID, cCodeInfos: ULONG32, pcCodeInfos: *mut ULONG32, codeInfos: *mut c_void) -> HRESULT;
        // 8
        pub fn GetClassFromTokenAndTypeArgs(&self, moduleID: ModuleID, typeDef: mdTypeDef, cTypeArgs: ULONG32, typeArgs: *const ClassID, pClassID: *mut ClassID) -> HRESULT;
        // 9
        pub fn GetFunctionFromTokenAndTypeArgs(&self, moduleID: ModuleID, funcDef: mdMethodDef, classId: ClassID, cTypeArgs: ULONG32, typeArgs: *const ClassID, pFunctionID: *mut FunctionID) -> HRESULT;
        // 10
        pub fn EnumModuleFrozenObjects(&self, moduleID: ModuleID, ppEnum: *mut *mut c_void) -> HRESULT;
        // 11
        pub fn GetArrayObjectInfo(&self, objectId: ObjectID, cDimensions: ULONG32, pDimensionSizes: *mut ULONG32, pDimensionLowerBounds: *mut i32, ppData: *mut *mut BYTE) -> HRESULT;
        // 12
        pub fn GetBoxClassLayout(&self, classId: ClassID, pBufferOffset: *mut ULONG32) -> HRESULT;
        // 13
        pub fn GetThreadAppDomain(&self, threadId: ThreadID, pAppDomainId: *mut AppDomainID) -> HRESULT;
        // 14
        pub fn GetRVAStaticAddress(&self, classId: ClassID, fieldToken: mdFieldDef, ppAddress: *mut *mut c_void) -> HRESULT;
        // 15
        pub fn GetAppDomainStaticAddress(&self, classId: ClassID, fieldToken: mdFieldDef, appDomainId: AppDomainID, ppAddress: *mut *mut c_void) -> HRESULT;
        // 16
        pub fn GetThreadStaticAddress(&self, classId: ClassID, fieldToken: mdFieldDef, threadId: ThreadID, ppAddress: *mut *mut c_void) -> HRESULT;
        // 17
        pub fn GetContextStaticAddress(&self, classId: ClassID, fieldToken: mdFieldDef, contextId: ContextID, ppAddress: *mut *mut c_void) -> HRESULT;
        // 18
        pub fn GetStaticFieldInfo(&self, classId: ClassID, fieldToken: mdFieldDef, pFieldInfo: *mut u32) -> HRESULT;
        // 19
        pub fn GetGenerationBounds(&self, cObjectRanges: ULONG, pcObjectRanges: *mut ULONG, ranges: *mut c_void) -> HRESULT;
        // 20
        pub fn GetObjectGeneration(&self, objectId: ObjectID, range: *mut c_void) -> HRESULT;
        // 21
        pub fn GetNotifiedExceptionClauseInfo(&self, pinfo: *mut c_void) -> HRESULT;
    }

    // ==================== ICorProfilerInfo3 ====================
    // 14 methods. Contains GetRuntimeInformation.

    #[uuid("B555ED4F-452A-4E54-8B39-B5360BAD32A0")]
    pub unsafe interface ICorProfilerInfo3: ICorProfilerInfo2 {
        // 1
        pub fn EnumJITedFunctions(&self, ppEnum: *mut *mut c_void) -> HRESULT;
        // 2
        pub fn RequestProfilerDetach(&self, dwExpectedCompletionMilliseconds: DWORD) -> HRESULT;
        // 3
        pub fn SetFunctionIDMapper2(&self, pFunc: *const c_void, clientData: *const c_void) -> HRESULT;
        // 4
        pub fn GetStringLayout2(&self, pStringLengthOffset: *mut ULONG, pBufferOffset: *mut ULONG) -> HRESULT;
        // 5
        pub fn SetEnterLeaveFunctionHooks3(&self, pFuncEnter3: *const c_void, pFuncLeave3: *const c_void, pFuncTailcall3: *const c_void) -> HRESULT;
        // 6
        pub fn SetEnterLeaveFunctionHooks3WithInfo(&self, pFuncEnter3WithInfo: *const c_void, pFuncLeave3WithInfo: *const c_void, pFuncTailcall3WithInfo: *const c_void) -> HRESULT;
        // 7
        pub fn GetFunctionEnter3Info(&self, functionId: FunctionID, eltInfo: COR_PRF_ELT_INFO, pFrameInfo: *mut COR_PRF_FRAME_INFO, pcbArgumentInfo: *mut ULONG, pArgumentInfo: *mut c_void) -> HRESULT;
        // 8
        pub fn GetFunctionLeave3Info(&self, functionId: FunctionID, eltInfo: COR_PRF_ELT_INFO, pFrameInfo: *mut COR_PRF_FRAME_INFO, pRetvalRange: *mut c_void) -> HRESULT;
        // 9
        pub fn GetFunctionTailcall3Info(&self, functionId: FunctionID, eltInfo: COR_PRF_ELT_INFO, pFrameInfo: *mut COR_PRF_FRAME_INFO) -> HRESULT;
        // 10
        pub fn EnumModules(&self, ppEnum: *mut *mut c_void) -> HRESULT;
        // 11 ** Used in Initialize **
        pub fn GetRuntimeInformation(&self, pClrInstanceId: *mut USHORT, pRuntimeType: *mut u32, pMajorVersion: *mut USHORT, pMinorVersion: *mut USHORT, pBuildNumber: *mut USHORT, pQFEVersion: *mut USHORT, cchVersionString: ULONG, pcchVersionString: *mut ULONG, szVersionString: *mut WCHAR) -> HRESULT;
        // 12
        pub fn GetThreadStaticAddress2(&self, classId: ClassID, fieldToken: mdFieldDef, appDomainId: AppDomainID, threadId: ThreadID, ppAddress: *mut *mut c_void) -> HRESULT;
        // 13
        pub fn GetAppDomainsContainingModule(&self, moduleId: ModuleID, cAppDomainIds: ULONG32, pcAppDomainIds: *mut ULONG32, appDomainIds: *mut AppDomainID) -> HRESULT;
        // 14
        pub fn GetModuleInfo2(&self, moduleId: ModuleID, ppBaseLoadAddress: *mut LPCBYTE, cchName: ULONG, pcchName: *mut ULONG, szName: *mut WCHAR, pAssemblyId: *mut AssemblyID, pdwModuleFlags: *mut DWORD) -> HRESULT;
    }

    // ==================== ICorProfilerInfo4 ====================
    // 10 methods. Contains RequestReJIT.

    #[uuid("0D8FDCAA-6257-47BF-B1BF-94DAC88466EE")]
    pub unsafe interface ICorProfilerInfo4: ICorProfilerInfo3 {
        // 1
        pub fn EnumThreads(&self, ppEnum: *mut *mut c_void) -> HRESULT;
        // 2
        pub fn InitializeCurrentThread(&self) -> HRESULT;
        // 3
        pub fn RequestReJIT(&self, cFunctions: ULONG, moduleIds: *const ModuleID, methodIds: *const mdMethodDef) -> HRESULT;
        // 4
        pub fn RequestRevert(&self, cFunctions: ULONG, moduleIds: *const ModuleID, methodIds: *const mdMethodDef, status: *mut HRESULT) -> HRESULT;
        // 5
        pub fn GetCodeInfo3(&self, functionID: FunctionID, reJitId: ReJITID, cCodeInfos: ULONG32, pcCodeInfos: *mut ULONG32, codeInfos: *mut c_void) -> HRESULT;
        // 6
        pub fn GetFunctionFromIP2(&self, ip: LPCBYTE, pFunctionId: *mut FunctionID, pReJitId: *mut ReJITID) -> HRESULT;
        // 7
        pub fn GetReJITIDs(&self, functionId: FunctionID, cReJitIds: ULONG, pcReJitIds: *mut ULONG, reJitIds: *mut ReJITID) -> HRESULT;
        // 8
        pub fn GetILToNativeMapping2(&self, functionId: FunctionID, reJitId: ReJITID, cMap: ULONG32, pcMap: *mut ULONG32, map: *mut c_void) -> HRESULT;
        // 9
        pub fn EnumJITedFunctions2(&self, ppEnum: *mut *mut c_void) -> HRESULT;
        // 10
        pub fn GetObjectSize2(&self, objectId: ObjectID, pcSize: *mut SIZE_T) -> HRESULT;
    }

    // ==================== ICorProfilerInfo5 ====================
    // 2 methods. Contains SetEventMask2 for disabling tiered compilation.

    #[uuid("07602928-CE38-4B83-81E7-74ADAF781214")]
    pub unsafe interface ICorProfilerInfo5: ICorProfilerInfo4 {
        // 1
        pub fn GetEventMask2(&self, pdwEventsLow: *mut DWORD, pdwEventsHigh: *mut DWORD) -> HRESULT;
        // 2 ** Used in Initialize **
        pub fn SetEventMask2(&self, dwEventsLow: DWORD, dwEventsHigh: DWORD) -> HRESULT;
    }
}
