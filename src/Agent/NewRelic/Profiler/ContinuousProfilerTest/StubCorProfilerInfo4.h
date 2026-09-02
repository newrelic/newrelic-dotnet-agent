/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <cor.h>
#include <corprof.h>

// Minimal ICorProfilerInfo4 stand-in for tests that only need ContinuousProfiler::Init() to have been
// handed a non-null interface -- e.g. the lifecycle tests, which exercise Start/Stop/Shutdown thread
// management with no CLR in the process. Every method fails with E_NOTIMPL except the two the profiler
// actually calls on a non-sampling path: QueryInterface (ICorProfilerInfo10 is refused, so the profiler
// takes its "runtime cannot suspend" branch and never samples) and InitializeCurrentThread.
//
// Reference counting is a no-op: instances are stack-allocated by the test and MUST outlive the
// ContinuousProfiler that holds them, since Release never destroys anything.
//
// The method list is mechanically derived from the vendored corprof.h under
// externals/coreclr-headers -- if that header is ever bumped and gains interface methods, the compiler
// reports this class as abstract, and the new methods get the same E_NOTIMPL treatment.
// Every stubbed method ignores its arguments, which trips C4100 (unreferenced formal parameter) at the
// /W4 + warnings-as-errors this project builds with. Suppressed for this header only, rather than
// name-stripping 80 signatures that must otherwise stay verbatim copies of the interface.
#pragma warning(push)
#pragma warning(disable: 4100)

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class StubCorProfilerInfo4 : public ICorProfilerInfo4
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (ppvObject == nullptr)
            {
                return E_POINTER;
            }

            if (riid == __uuidof(ICorProfilerInfo4) || riid == __uuidof(ICorProfilerInfo3) ||
                riid == __uuidof(ICorProfilerInfo2) || riid == __uuidof(ICorProfilerInfo) ||
                riid == __uuidof(IUnknown))
            {
                *ppvObject = static_cast<ICorProfilerInfo4*>(this);
                return S_OK;
            }

            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        virtual ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
        virtual ULONG STDMETHODCALLTYPE Release() override { return 1; }

        // ICorProfilerInfo
        virtual HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID *pClassId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID *pClassId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID functionId, LPCBYTE *pStart, ULONG *pcSize) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetEventMask(DWORD *pdwEvents) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE ip, FunctionID *pFunctionId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID *pFunctionId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID threadId, HANDLE *phThread) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID objectId, ULONG *pcSize) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID classId, CorElementType *pBaseElemType, ClassID *pBaseClassId, ULONG *pcRank) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD *pdwWin32ThreadId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID *pThreadId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID classId, ModuleID *pModuleId, mdTypeDef *pTypeDefToken) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID functionId, ClassID *pClassId, ModuleID *pModuleId, mdToken *pToken) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetEventMask(DWORD dwEvents) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter *pFuncEnter, FunctionLeave *pFuncLeave, FunctionTailcall *pFuncTailcall) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(FunctionIDMapper *pFunc) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID functionId, REFIID riid, IUnknown **ppImport, mdToken *pToken) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID moduleId, LPCBYTE *ppBaseLoadAddress, ULONG cchName, ULONG *pcchName, _Out_writes_to_(cchName, *pcchName) WCHAR szName[ ], AssemblyID *pAssemblyId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, REFIID riid, IUnknown **ppOut) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE *ppMethodHeader, ULONG *pcbMethodSize) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc **ppMalloc) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG *pcchName, _Out_writes_to_(cchName, *pcchName) WCHAR szName[ ], ProcessID *pProcessId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG *pcchName, _Out_writes_to_(cchName, *pcchName) WCHAR szName[ ], AppDomainID *pAppDomainId, ModuleID *pModuleId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID functionId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE ForceGC(void) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown **ppicd) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown **ppicd) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID threadId, ContextID *pContextId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL fThisThreadOnly, DWORD *pdwProfilerContext) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD dwProfilerContext) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32 *pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[ ]) override { return E_NOTIMPL; }

        // ICorProfilerInfo2
        virtual HRESULT STDMETHODCALLTYPE DoStackSnapshot(ThreadID thread, StackSnapshotCallback *callback, ULONG32 infoFlags, void *clientData, BYTE context[ ], ULONG32 contextSize) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks2(FunctionEnter2 *pFuncEnter, FunctionLeave2 *pFuncLeave, FunctionTailcall2 *pFuncTailcall) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionInfo2(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo, ClassID *pClassId, ModuleID *pModuleId, mdToken *pToken, ULONG32 cTypeArgs, ULONG32 *pcTypeArgs, ClassID typeArgs[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetStringLayout(ULONG *pBufferLengthOffset, ULONG *pStringLengthOffset, ULONG *pBufferOffset) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetClassLayout(ClassID classID, COR_FIELD_OFFSET rFieldOffset[ ], ULONG cFieldOffset, ULONG *pcFieldOffset, ULONG *pulClassSize) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetClassIDInfo2(ClassID classId, ModuleID *pModuleId, mdTypeDef *pTypeDefToken, ClassID *pParentClassId, ULONG32 cNumTypeArgs, ULONG32 *pcNumTypeArgs, ClassID typeArgs[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetCodeInfo2(FunctionID functionID, ULONG32 cCodeInfos, ULONG32 *pcCodeInfos, COR_PRF_CODE_INFO codeInfos[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetClassFromTokenAndTypeArgs(ModuleID moduleID, mdTypeDef typeDef, ULONG32 cTypeArgs, ClassID typeArgs[ ], ClassID *pClassID) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionFromTokenAndTypeArgs(ModuleID moduleID, mdMethodDef funcDef, ClassID classId, ULONG32 cTypeArgs, ClassID typeArgs[ ], FunctionID *pFunctionID) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE EnumModuleFrozenObjects(ModuleID moduleID, ICorProfilerObjectEnum **ppEnum) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetArrayObjectInfo(ObjectID objectId, ULONG32 cDimensions, ULONG32 pDimensionSizes[ ], int pDimensionLowerBounds[ ], BYTE **ppData) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetBoxClassLayout(ClassID classId, ULONG32 *pBufferOffset) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetThreadAppDomain(ThreadID threadId, AppDomainID *pAppDomainId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetRVAStaticAddress(ClassID classId, mdFieldDef fieldToken, void **ppAddress) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainStaticAddress(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId, void **ppAddress) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetThreadStaticAddress(ClassID classId, mdFieldDef fieldToken, ThreadID threadId, void **ppAddress) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetContextStaticAddress(ClassID classId, mdFieldDef fieldToken, ContextID contextId, void **ppAddress) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetStaticFieldInfo(ClassID classId, mdFieldDef fieldToken, COR_PRF_STATIC_TYPE *pFieldInfo) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetGenerationBounds(ULONG cObjectRanges, ULONG *pcObjectRanges, COR_PRF_GC_GENERATION_RANGE ranges[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetObjectGeneration(ObjectID objectId, COR_PRF_GC_GENERATION_RANGE *range) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetNotifiedExceptionClauseInfo(COR_PRF_EX_CLAUSE_INFO *pinfo) override { return E_NOTIMPL; }

        // ICorProfilerInfo3
        virtual HRESULT STDMETHODCALLTYPE EnumJITedFunctions(ICorProfilerFunctionEnum **ppEnum) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE RequestProfilerDetach(DWORD dwExpectedCompletionMilliseconds) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetFunctionIDMapper2(FunctionIDMapper2 *pFunc, void *clientData) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetStringLayout2(ULONG *pStringLengthOffset, ULONG *pBufferOffset) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3(FunctionEnter3 *pFuncEnter3, FunctionLeave3 *pFuncLeave3, FunctionTailcall3 *pFuncTailcall3) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3WithInfo(FunctionEnter3WithInfo *pFuncEnter3WithInfo, FunctionLeave3WithInfo *pFuncLeave3WithInfo, FunctionTailcall3WithInfo *pFuncTailcall3WithInfo) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionEnter3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO *pFrameInfo, ULONG *pcbArgumentInfo, COR_PRF_FUNCTION_ARGUMENT_INFO *pArgumentInfo) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionLeave3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO *pFrameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE *pRetvalRange) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionTailcall3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO *pFrameInfo) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE EnumModules(ICorProfilerModuleEnum **ppEnum) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetRuntimeInformation(USHORT *pClrInstanceId, COR_PRF_RUNTIME_TYPE *pRuntimeType, USHORT *pMajorVersion, USHORT *pMinorVersion, USHORT *pBuildNumber, USHORT *pQFEVersion, ULONG cchVersionString, ULONG *pcchVersionString, _Out_writes_to_(cchVersionString, *pcchVersionString) WCHAR szVersionString[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetThreadStaticAddress2(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId, ThreadID threadId, void **ppAddress) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainsContainingModule(ModuleID moduleId, ULONG32 cAppDomainIds, ULONG32 *pcAppDomainIds, AppDomainID appDomainIds[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfo2(ModuleID moduleId, LPCBYTE *ppBaseLoadAddress, ULONG cchName, ULONG *pcchName, _Out_writes_to_(cchName, *pcchName) WCHAR szName[ ], AssemblyID *pAssemblyId, DWORD *pdwModuleFlags) override { return E_NOTIMPL; }

        // ICorProfilerInfo4
        virtual HRESULT STDMETHODCALLTYPE EnumThreads(ICorProfilerThreadEnum **ppEnum) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE InitializeCurrentThread(void) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[ ], mdMethodDef methodIds[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE RequestRevert(ULONG cFunctions, ModuleID moduleIds[ ], mdMethodDef methodIds[ ], HRESULT status[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetCodeInfo3(FunctionID functionID, ReJITID reJitId, ULONG32 cCodeInfos, ULONG32 *pcCodeInfos, COR_PRF_CODE_INFO codeInfos[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetFunctionFromIP2(LPCBYTE ip, FunctionID *pFunctionId, ReJITID *pReJitId) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetReJITIDs(FunctionID functionId, ULONG cReJitIds, ULONG *pcReJitIds, ReJITID reJitIds[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetILToNativeMapping2(FunctionID functionId, ReJITID reJitId, ULONG32 cMap, ULONG32 *pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[ ]) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE EnumJITedFunctions2(ICorProfilerFunctionEnum **ppEnum) override { return E_NOTIMPL; }
        virtual HRESULT STDMETHODCALLTYPE GetObjectSize2(ObjectID objectId, SIZE_T *pcSize) override { return E_NOTIMPL; }
    };
}}}

#pragma warning(pop)
