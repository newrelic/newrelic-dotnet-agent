// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <atomic>
#include <cor.h>
#include <corprof.h>

#include "../Configuration/Configuration.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../Logging/Logger.h"
#include "../MethodRewriter/CustomInstrumentation.h"
#include "../MethodRewriter/MethodRewriter.h"
#include "../SignatureParser/Exceptions.h"
#include "../ThreadProfiler/ThreadProfiler.h"
#include "Function.h"
#include "FunctionResolver.h"
#include "Win32Helpers.h"
#include "guids.h"
#include <fstream>
#include <map>
#include <memory>
#include <string>
#include <thread>
#include <utility>

#ifdef PAL_STDCPP_COMPAT
#include "UnixSystemCalls.h"
#else
#include "../ModuleInjector/ModuleInjector.h"
#include "Module.h"
#include "SystemCalls.h"
#include <shellapi.h>
#endif

namespace NewRelic { namespace Profiler {
// disable unused parameter warnings for this class
#pragma warning(push)
#pragma warning(disable : 4100)

    const ULONG METHOD_ENUM_BATCH_SIZE = 5;
    const ULONG MODULE_ENUM_BATCH_SIZE = 100;
    using NewRelic::Profiler::MethodRewriter::FilePaths;

    class ClassAndMethodName {
    public:
        ClassAndMethodName(xstring_t className, xstring_t methodName)
        {
            _className = className;
            _methodName = methodName;
        }

        xstring_t _className;
        xstring_t _methodName;
    };

    struct RuntimeInfo {
        USHORT clrInstanceId;
        COR_PRF_RUNTIME_TYPE runtimeType;
        USHORT majorVersion;
        USHORT minorVersion;
        USHORT buildNumber;
        USHORT qfeVersion;
    };

    typedef std::set<xstring_t> FilePaths;

    class CorProfilerCallbackImpl : public ICorProfilerCallback4 {

    private:
        std::atomic<int> _referenceCount;

#ifndef PAL_STDCPP_COMPAT
        std::shared_ptr<ModuleInjector::ModuleInjector> _moduleInjector;
#endif

    public:
        CorProfilerCallbackImpl()
            : _referenceCount(0)
        {
            _systemCalls = std::make_shared<SystemCalls>();
            GetSingletonish() = this;
        }

        ~CorProfilerCallbackImpl()
        {
            if (GetSingletonish() == this)
                GetSingletonish() = nullptr;
        }

        // Unimplemented ICorProfilerCallback
        virtual HRESULT __stdcall AppDomainCreationStarted(AppDomainID appDomainId) override { return S_OK; }
        virtual HRESULT __stdcall AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall AppDomainShutdownStarted(AppDomainID appDomainId) override { return S_OK; }
        virtual HRESULT __stdcall AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall AssemblyLoadStarted(AssemblyID assemblyId) override { return S_OK; }
        virtual HRESULT __stdcall AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall AssemblyUnloadStarted(AssemblyID assemblyId) override { return S_OK; }
        virtual HRESULT __stdcall AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall ModuleLoadStarted(ModuleID moduleId) override { return S_OK; }
        virtual HRESULT __stdcall ModuleUnloadStarted(ModuleID moduleId) override { return S_OK; }
        virtual HRESULT __stdcall ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId) override { return S_OK; }
        virtual HRESULT __stdcall ClassLoadStarted(ClassID classId) override { return S_OK; }
        virtual HRESULT __stdcall ClassLoadFinished(ClassID classId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall ClassUnloadStarted(ClassID classId) override { return S_OK; }
        virtual HRESULT __stdcall ClassUnloadFinished(ClassID classId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall FunctionUnloadStarted(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) override { return S_OK; }
        virtual HRESULT __stdcall JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) override { return S_OK; }
        virtual HRESULT __stdcall JITCachedFunctionSearchFinished(FunctionID functionId, COR_PRF_JIT_CACHE result) override { return S_OK; }
        virtual HRESULT __stdcall JITFunctionPitched(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) override { return S_OK; }
        virtual HRESULT __stdcall ThreadCreated(ThreadID threadId) override { return S_OK; }
        virtual HRESULT __stdcall ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId) override { return S_OK; }
        virtual HRESULT __stdcall RemotingClientInvocationStarted() override { return S_OK; }
        virtual HRESULT __stdcall RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
        virtual HRESULT __stdcall RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
        virtual HRESULT __stdcall RemotingClientInvocationFinished(void) override { return S_OK; }
        virtual HRESULT __stdcall RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
        virtual HRESULT __stdcall RemotingServerInvocationStarted() override { return S_OK; }
        virtual HRESULT __stdcall RemotingServerInvocationReturned() override { return S_OK; }
        virtual HRESULT __stdcall RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
        virtual HRESULT __stdcall UnmanagedToManagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override { return S_OK; }
        virtual HRESULT __stdcall ManagedToUnmanagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override { return S_OK; }
        virtual HRESULT __stdcall RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) override { return S_OK; }
        virtual HRESULT __stdcall RuntimeSuspendFinished() override { return S_OK; }
        virtual HRESULT __stdcall RuntimeSuspendAborted() override { return S_OK; }
        virtual HRESULT __stdcall RuntimeResumeStarted() override { return S_OK; }
        virtual HRESULT __stdcall RuntimeResumeFinished() override { return S_OK; }
        virtual HRESULT __stdcall RuntimeThreadSuspended(ThreadID threadId) override { return S_OK; }
        virtual HRESULT __stdcall RuntimeThreadResumed(ThreadID threadId) override { return S_OK; }
        virtual HRESULT __stdcall MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) override { return S_OK; }
        virtual HRESULT __stdcall ObjectAllocated(ObjectID objectId, ClassID classId) override { return S_OK; }
        virtual HRESULT __stdcall ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]) override { return S_OK; }
        virtual HRESULT __stdcall ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) override { return S_OK; }
        virtual HRESULT __stdcall RootReferences(ULONG cRootRefs, ObjectID rootRefIds[]) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionThrown(ObjectID thrownObjectId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionSearchFunctionEnter(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionSearchFunctionLeave() override { return S_OK; }
        virtual HRESULT __stdcall ExceptionSearchFilterEnter(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionSearchFilterLeave() override { return S_OK; }
        virtual HRESULT __stdcall ExceptionSearchCatcherFound(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionOSHandlerEnter(UINT_PTR __unused) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionOSHandlerLeave(UINT_PTR __unused) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionUnwindFunctionEnter(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionUnwindFunctionLeave() override { return S_OK; }
        virtual HRESULT __stdcall ExceptionUnwindFinallyEnter(FunctionID functionId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionUnwindFinallyLeave() override { return S_OK; }
        virtual HRESULT __stdcall ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionCatcherLeave() override { return S_OK; }
        virtual HRESULT __stdcall COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable, ULONG cSlots) override { return S_OK; }
        virtual HRESULT __stdcall COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable) override { return S_OK; }
        virtual HRESULT __stdcall ExceptionCLRCatcherFound() override { return S_OK; }
        virtual HRESULT __stdcall ExceptionCLRCatcherExecute() override { return S_OK; }

        // Unimplemented ICorProfilerCallback2
        virtual HRESULT __stdcall FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID) override { return S_OK; }
        virtual HRESULT __stdcall GarbageCollectionFinished(void) override { return S_OK; }
        virtual HRESULT __stdcall GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason) override { return S_OK; }
        virtual HRESULT __stdcall HandleCreated(GCHandleID handleId, ObjectID initialObjectId) override { return S_OK; }
        virtual HRESULT __stdcall HandleDestroyed(GCHandleID handleId) override { return S_OK; }
        virtual HRESULT __stdcall RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override { return S_OK; }
        virtual HRESULT __stdcall SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]) override { return S_OK; }
        virtual HRESULT __stdcall ThreadNameChanged(ThreadID threadId, ULONG cchName, _In_reads_opt_(cchName) WCHAR name[]) override { return S_OK; }

        // Unimplemented ICorProfilerCallback3
        virtual HRESULT __stdcall InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData) override { return S_OK; }
        virtual HRESULT __stdcall ProfilerAttachComplete(void) override { return S_OK; }
        virtual HRESULT __stdcall ProfilerDetachSucceeded(void) override { return S_OK; }

        // Unimplemented ICorProfilerCallback4
        virtual HRESULT __stdcall ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock) override { return S_OK; }
        virtual HRESULT __stdcall ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus) override { return S_OK; }
        virtual HRESULT __stdcall MovedReferences2(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override { return S_OK; }
        virtual HRESULT __stdcall SurvivingReferences2(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override { return S_OK; }

        // Base profiler initialization method
        virtual HRESULT __stdcall Initialize(IUnknown* pICorProfilerInfoUnk) override
        {
#ifdef DEBUG
            DelayProfilerAttach();
#endif

            // initialization stuff, they should be logging their own errors and only throwing up if they want to cancel activation
            try
            {
                HRESULT corProfilerInfoInitResult = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**)&_corProfilerInfo4);
                if (FAILED(corProfilerInfoInitResult)) {
                    // Since MinimumDotnetVersionCheck already queried for minimum required interface, this check is just for safety
                    LogError(_X("Error initializing CLR profiler info: "), corProfilerInfoInitResult);
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                // Need runtime information to determine CLR type
                auto runtimeInfo = std::make_shared<RuntimeInfo>();
                auto runtimeInfoResult = _corProfilerInfo4->GetRuntimeInformation(nullptr,
                    &runtimeInfo->runtimeType, &runtimeInfo->majorVersion, &runtimeInfo->minorVersion, nullptr, nullptr, 0, nullptr, nullptr);

                if (FAILED(runtimeInfoResult)) {
                    LogError(_X("Error retrieving runtime information: "), runtimeInfoResult);
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                if (!SetClrType(runtimeInfo)) {
                    LogError(_X("Unknown Runtime Type found: "), runtimeInfo->runtimeType);
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                // A bit of a catch-22: we want to load the config file first in case there are logging
                // settings, but we also want to log if there are issues with the config file.
                // So first, we try to load the config file...
                NewRelic::Profiler::Configuration::ConfigurationPtr configuration;
                bool configFailed = false;
                try
                {
                    configuration = InitializeConfigAndSetLogLevel();
                }
                catch (...)
                {
                    configFailed = true;
                }

                // ...then we initialize the log
                InitializeLogging();

                // If we failed to load the config file, try again. Not because we expect it
                // to succeed, but because we want to log the error. We couldn't do that the
                // first time because the logger hadn't been initialized yet
                if (configFailed)
                {
                    configuration = InitializeConfigAndSetLogLevel();
                }

                LogTrace(_productName);

                if (FAILED(MinimumDotnetVersionCheck(pICorProfilerInfoUnk))) {
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                //Init does not start threads or requires cleanup. RequestProfile will create the threads for the TP.
                _threadProfiler.Init(_corProfilerInfo4);


                HRESULT corePathInitResult = InitializeAndSetAgentCoreDllPath(_productName);
                if (FAILED(corePathInitResult)) {
                    return corePathInitResult;
                }

                auto instrumentationConfiguration = InitializeInstrumentationConfig();
                auto methodRewriter = std::make_shared<MethodRewriter::MethodRewriter>(instrumentationConfiguration, _agentCoreDllPath);
                this->SetMethodRewriter(methodRewriter);

                LogTrace("Checking to see if we should instrument this process.");
                auto forceProfiling = _systemCalls->GetForceProfiling();
                auto processPath = Strings::ToUpper(_systemCalls->GetProcessPath());
                auto commandLine = _systemCalls->GetProgramCommandLine();
                auto parentProcessPath = Strings::ToUpper(_systemCalls->GetParentProcessPath());
                auto appPoolId = GetAppPoolId(_systemCalls);
                LogInfo(L"Command line: ", commandLine);
                if (!forceProfiling && !configuration->ShouldInstrument(processPath, parentProcessPath, appPoolId, commandLine, _isCoreClr)) {
                    LogInfo("This process should not be instrumented, unloading profiler.");
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                _functionResolver = std::make_shared<FunctionResolver>(_corProfilerInfo4);

                ConfigureEventMask(pICorProfilerInfoUnk);

                LogRuntimeInfo(runtimeInfo);

                LogMessageIfAppDomainCachingIsDisabled();

                LogInfo(L"Profiler initialized");
                return S_OK;
            }
            catch (...) {
                LogError(L"An exception was thrown while initializing the profiler.");
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }
        }

        virtual HRESULT __stdcall ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) override
        {
            if (_isCoreClr)
            {
                if (SUCCEEDED(hrStatus)) {
                    try {
                        auto assemblyName = GetAssemblyName(moduleId);

                        if (GetMethodRewriter()->ShouldInstrumentAssembly(assemblyName)) {
                            LogTrace("Assembly module loaded: ", assemblyName);

                            auto instrumentationPoints = std::make_shared<Configuration::InstrumentationPointSet>(GetMethodRewriter()->GetAssemblyInstrumentation(assemblyName));
                            auto methodDefs = GetMethodDefs(moduleId, instrumentationPoints);

                            if (methodDefs != nullptr) {
                                RejitModuleFunctions(moduleId, methodDefs);
                            }
                        }
                    }
                    catch (...) {
                    }
                }
                return S_OK;
            }
            else
            {
#ifndef PAL_STDCPP_COMPAT

                // if the module did not load correctly then we don't want to mess with it
                if (FAILED(hrStatus))
                {
                    return hrStatus;
                }

                LogTrace("Module Injection Started. ", moduleId);

                ModuleInjector::IModulePtr module;
                try
                {
                    module = std::make_shared<Module>(_corProfilerInfo4, moduleId);
                }
                catch (const NewRelic::Profiler::MessageException& exception)
                {
                    (void)exception;
                    return S_OK;
                }
                catch (...)
                {
                    LogError(L"An exception was thrown while getting details about a module.");
                    return E_FAIL;
                }

                try
                {
                    _moduleInjector->InjectIntoModule(*module);
                }
                catch (...)
                {
                    LogError(L"An exception was thrown while attempting to inject into a module.");
                    return E_FAIL;
                }

                LogTrace("Module Injection Finished. ", moduleId, " : ", module->GetModuleName());
#endif
                return S_OK;
            }
        }


        virtual DWORD OverrideEventMask(DWORD eventMask)
        {
#ifndef PAL_STDCPP_COMPAT
            if (!_isCoreClr)
            {
                _moduleInjector.reset(new ModuleInjector::ModuleInjector());
            }
#endif
            return eventMask;
        }

        virtual void ConfigureEventMask(IUnknown* pICorProfilerInfoUnk)
        {
            if (_isCoreClr)
            {
                // register for events that we are interested in getting callbacks for
// SetEventMask2 requires ICorProfilerInfo5. It allows setting the high-order bits of the profiler event mask.
// 0x8 = COR_PRF_HIGH_DISABLE_TIERED_COMPILATION <- this was introduced in ICorProfilerCallback9 which we're not currently implementing
// see this PR: https://github.com/dotnet/coreclr/pull/14643/files#diff-e7d550d94de30cdf5e7f3a25647a2ae1R626
// Just passing in the hardcoded 0x8 seems to actually disable tiered compilation,
// but we should see about actually referencing and implementing ICorProfilerCallback9

                CComPtr<ICorProfilerInfo5> _corProfilerInfo5;
                const DWORD COR_PRF_HIGH_DISABLE_TIERED_COMPILATION = 0x8;

                if (FAILED(pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo5), (void**)&_corProfilerInfo5))) {
                    LogDebug(L"Calling SetEventMask().");
                    ThrowOnError(_corProfilerInfo4->SetEventMask, _eventMask);
                }
                else {
                    LogDebug(L"Calling SetEventMask2().");
                    ThrowOnError(_corProfilerInfo5->SetEventMask2, _eventMask, COR_PRF_HIGH_DISABLE_TIERED_COMPILATION);
                }
            }
            else
            {
                // register for events that we are interested in getting callbacks for
                LogDebug(L"Calling SetEventMask().");
                ThrowOnError(_corProfilerInfo4->SetEventMask, _eventMask);
            }
        }

        virtual xstring_t GetRuntimeExtensionsDirectoryName()
        {
            if (_isCoreClr)
            {
                return _X("netcore");
            }
            else
            {
                return _X("netframework");
            }
        }

        virtual HRESULT MinimumDotnetVersionCheck(IUnknown* pICorProfilerInfoUnk)
        {
            if (_isCoreClr)
            {
                CComPtr<ICorProfilerInfo8> temp;
                HRESULT result = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo11), (void**)&temp);
                if (FAILED(result)) {
                    LogError(_X(".NET Core 3.1 or greater required. Profiler not attaching."));
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }
                return S_OK;
            }
            else
            {
                CComPtr<ICorProfilerInfo4> temp;
                HRESULT interfaceCheckResult = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo7), (void**)&temp);
                if (FAILED(interfaceCheckResult)) {
                    LogError(_X(".NET Framework 4.6.1 is required.  Detaching New Relic profiler."));
                    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
                }

                return S_OK;
            }
        }

        virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (
                riid == __uuidof(ICorProfilerCallback4) || riid == __uuidof(ICorProfilerCallback3) || riid == __uuidof(ICorProfilerCallback2) || riid == __uuidof(ICorProfilerCallback) || riid == IID_IUnknown) {
                *ppvObject = this;
                this->AddRef();
                return S_OK;
            }

            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        virtual ULONG STDMETHODCALLTYPE AddRef() override
        {
            return std::atomic_fetch_add(&this->_referenceCount, 1) + 1;
        }

        virtual ULONG STDMETHODCALLTYPE Release() override
        {
            int count = std::atomic_fetch_sub(&this->_referenceCount, 1) - 1;

            if (count <= 0) {
                delete this;
            }

            return count;
        }

        // ICorProfilerCallback
        virtual HRESULT __stdcall JITCompilationStarted(FunctionID functionId, BOOL /*fIsSafeToBlock*/) override
        {
            LogTrace(__func__, L". ", functionId);

            auto setILFunctionBody = [&](Function& function, LPCBYTE pHeader, ULONG) {
                return _corProfilerInfo4->SetILFunctionBody(function.GetModuleID(), function.GetMethodToken(), pHeader);
            };

            // on JIT just ask for a rejit for instrumented methods
            HRESULT hr = ProcessMethodJit(functionId, false, setILFunctionBody);
            //HRESULT hr = ProcessMethodJit(functionId, false);

            LogTrace(__func__, L"Finished. ", functionId);
            return hr;
        }

        // Requests a function ReJIT.
        HRESULT RejitFunction(Function& function)
        {
            LogDebug(L"Request reJIT: [", function.GetFunctionId(), "] ", function.ToString());
            _functionResolver->AddFunctionIfGeneric(function);

            ModuleID moduleIds = { function.GetModuleID() };
            mdMethodDef methodIds = { function.GetMethodToken() };
            return _corProfilerInfo4->RequestReJIT(1, &moduleIds, &methodIds);
        }

        virtual HRESULT __stdcall ReJITCompilationStarted(FunctionID functionId, ReJITID /*rejitId*/, BOOL /*fIsSafeToBlock*/) override
        {
            LogTrace(__func__, L". ", functionId);
            _functionResolver->RequestGenericMethodReJIT(functionId);
            LogTrace(__func__, L"Finished. ", functionId);
            return S_OK;
        }

        virtual HRESULT __stdcall GetReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl) override
        {
            LogTrace(__func__, L" called");

            HRESULT hr = S_FALSE;
            auto functionId = _functionResolver->GetFunctionId(moduleId, methodId);
            if (_functionResolver->IsValid(functionId)) {
                LogTrace(L"ReJIT ", functionId);

                auto setILFunctionBody = [&](Function&, LPCBYTE pHeader, ULONG size)
                {
                    return pFunctionControl->SetILFunctionBody(size, pHeader);
                };
                hr = ProcessMethodJit(functionId, true, setILFunctionBody);
            }

            LogTrace(__func__, L" finished");
            return hr;
        }

        HRESULT __stdcall ProcessMethodJit(FunctionID functionId, bool injectMethodInstrumentation,
            std::function<HRESULT(Function&, LPCBYTE, ULONG)> setILFunctionBody)
        {
            auto methodRewriter = GetMethodRewriter();
            MethodRewriter::IFunctionPtr function;
            try {
                // create the Function object for this method
                function = Function::Create(_corProfilerInfo4, functionId, methodRewriter, injectMethodInstrumentation,
          setILFunctionBody,
                    [&](Function& function) { return RejitFunction(function); });
                if (function == nullptr) {
                    LogTrace("JITCompilationStarted Finished. Function Skipped. ", functionId);
                    return S_OK;
                }
            } catch (...) {
                LogError(L"An exception was thrown while getting details about a function.");
                return E_FAIL;
            }

            try {
                // instrument the method
                methodRewriter->Instrument(function);
            } catch (NewRelic::Profiler::SignatureParser::SignatureParserException exception) {
                // Dont call function->ToString() since that might cause Signature to be parsed again
                // Printing the FunctionName should be enough to provide enough debugging context
                // based on other logging preceeding this log output
                LogError(L"An SignatureParserException was thrown while possibly instrumenting function: ", function->GetFunctionName());
                return E_FAIL;
            } catch (NewRelic::Profiler::MessageException exception) {
                LogError(L"An exception was thrown while possibly instrumenting function: ", function->ToString());
                LogError(exception._message);
                return E_FAIL;
            } catch (...) {
                // This is a fatal exception block, so dont call any other functions other than
                // Logging to keep the exception from bleeding out.
                LogError(L"An unexpected exception was thrown while possibly instrumenting a function.");
                return E_FAIL;
            }
            return S_OK;
        }

        virtual HRESULT __stdcall Shutdown() override
        {
            LogInfo(L"Profiler shutting down");
            _threadProfiler.Shutdown();
            LogInfo(L"Profiler shutdown");
            return S_OK;
        }

        // ICorProfilerCallback
        virtual HRESULT __stdcall ThreadDestroyed(ThreadID threadId) override
        {
            return _threadProfiler.ThreadDestroyed(threadId);
        }

        // Returns a map of assembly name to instrumentation points.
        std::shared_ptr<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>> GroupByAssemblyName(Configuration::InstrumentationPointSetPtr allInstrumentationPoints)
        {
            std::shared_ptr<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>> instrumentationPointsByAssembly = std::make_shared<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>>();
            for (auto point : *allInstrumentationPoints) {
                Configuration::InstrumentationPointSetPtr instrumentationPoints;
                auto it = instrumentationPointsByAssembly->find(point->AssemblyName);

                if (it != instrumentationPointsByAssembly->end()) {
                    instrumentationPoints = it->second;
                } else {
                    instrumentationPoints = std::make_shared<Configuration::InstrumentationPointSet>();
                    (*instrumentationPointsByAssembly)[point->AssemblyName] = instrumentationPoints;
                }

                instrumentationPoints->emplace(point);
            }

            return instrumentationPointsByAssembly;
        }

        HRESULT AddCustomInstrumentation(const xstring_t fileName, const xstring_t xml)
        {
            _customInstrumentationBuilder.AddCustomInstrumentationXml(fileName, xml);
            return S_OK;
        }

        HRESULT ApplyCustomInstrumentation()
        {
            auto instrumentation = _customInstrumentationBuilder.Build();
            if (instrumentation->size() > 0) {
                LogInfo("Applying live instrumentation");
            }
            auto currentCustomInstrumentation = _customInstrumentation.GetCustomInstrumentationXml();
            _customInstrumentation.ReplaceCustomInstrumentationXml(instrumentation);

            // if either is > 0 refresh, which covers instrumentation being added and later removed
            if (currentCustomInstrumentation->size() > 0 || instrumentation->size() > 0) {
                return InstrumentationRefresh();
            } else {
                return S_OK;
            }
        }

        HRESULT InstrumentationRefresh()
        {
            LogTrace("Enter: ", __func__);

            auto instrumentationXmls = GetInstrumentationXmlsFromDisk(_systemCalls);
            auto customXml = _customInstrumentation.GetCustomInstrumentationXml();
            for (auto xmlPair : *customXml) {
                (*instrumentationXmls)[xmlPair.first] = xmlPair.second;
            }

            auto instrumentationConfiguration = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationXmls);
            if (instrumentationConfiguration->GetInvalidFileCount() > 0) {
                LogError(L"Unable to parse one or more instrumentation files.  Instrumentation will not be refreshed.");
                return S_FALSE;
            }

            auto oldMethodRewriter = GetMethodRewriter();
            auto oldInstrumentationPoints = oldMethodRewriter->GetInstrumentationConfiguration()->GetInstrumentationPoints();

            SetMethodRewriter(std::make_shared<MethodRewriter::MethodRewriter>(instrumentationConfiguration, _agentCoreDllPath));

            auto oldInstrumentationByAssembly = GroupByAssemblyName(oldInstrumentationPoints);
            auto newInstrumentationByAssembly = GroupByAssemblyName(instrumentationConfiguration->GetInstrumentationPoints());

            std::thread t1(&NewRelic::Profiler::CorProfilerCallbackImpl::RejitInstrumentationPoints, this, oldInstrumentationByAssembly, newInstrumentationByAssembly);

            // block the calling managed thread until the worker thread has finished
            t1.join();

            LogTrace("Leave: ", __func__);

            return S_OK;
        }

        std::shared_ptr<std::set<mdMethodDef>> GetMethodDefsForAssembly(
            ModuleID moduleId,
            xstring_t assemblyName,
            std::shared_ptr<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>> instrumentationByAssembly)
        {
            auto oldIter = instrumentationByAssembly->find(assemblyName);
            if (oldIter != instrumentationByAssembly->end()) {
                auto points = oldIter->second;
                return GetMethodDefs(moduleId, points);
            }

            return nullptr;
        }

        HRESULT RejitInstrumentationPoints(
            std::shared_ptr<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>> oldInstrumentationByAssembly,
            std::shared_ptr<std::map<xstring_t, Configuration::InstrumentationPointSetPtr>> newInstrumentationByAssembly)
        {
            auto f = __func__;
            auto TOE = [f](HRESULT hr) { if (FAILED(hr)) { LogError("Function '", f, "' failed.  HRESULT: ", hr); throw Win32Exception(hr); } };

            CComPtr<ICorProfilerModuleEnum> moduleEnum;
            TOE(_corProfilerInfo4->EnumModules(&moduleEnum));

            ModuleID moduleIds[MODULE_ENUM_BATCH_SIZE];
            for (ULONG elementsFetched; SUCCEEDED(moduleEnum->Next(MODULE_ENUM_BATCH_SIZE, moduleIds, &elementsFetched)) && elementsFetched;) {
                for (ULONG i = 0; i < elementsFetched; i++) {
                    try {
                        auto assemblyName = GetAssemblyName(moduleIds[i]);

                        std::shared_ptr<std::set<mdMethodDef>> oldMethodDefs = GetMethodDefsForAssembly(moduleIds[i], assemblyName, oldInstrumentationByAssembly);
                        std::shared_ptr<std::set<mdMethodDef>> newMethodDefs = GetMethodDefsForAssembly(moduleIds[i], assemblyName, newInstrumentationByAssembly);

                        // remove new (to be instrumented) methods from old methods
                        if (newMethodDefs != nullptr && oldMethodDefs != nullptr) {
                            for (auto method : *newMethodDefs) {
                                oldMethodDefs->erase(method);
                            }
                        }

                        RevertModuleFunctions(moduleIds[i], oldMethodDefs);
                        RejitModuleFunctions(moduleIds[i], newMethodDefs);
                    } catch (...) {
                    }
                }
            }

            return S_OK;
        }

        std::shared_ptr<std::set<mdMethodDef>> GetMethodDefs(ModuleID moduleId, NewRelic::Profiler::Configuration::InstrumentationPointSetPtr instrumentationPoints)
        {
            CComPtr<IMetaDataImport> pImport = nullptr;
            CComPtr<IUnknown> pUnk = nullptr;

            if (FAILED(_corProfilerInfo4->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport, &pUnk)) || FAILED(pUnk->QueryInterface(IID_IMetaDataImport, (LPVOID*)&pImport))) {
                // Without a handle to the import api we can't do anything so just bail
                return nullptr;
            }

            std::shared_ptr<std::set<mdMethodDef>> methodDefs = std::make_shared<std::set<mdMethodDef>>();
            for (const auto& instrumentationPoint : *instrumentationPoints) {
                LogTrace("Fetching ", instrumentationPoint->ClassName, " methods");

                mdTypeDef typeDef{};
                HRESULT hr = pImport->FindTypeDefByName(instrumentationPoint->ClassName.c_str(), mdTypeDefNil, &typeDef);
                if (FAILED(hr)) {
                    LogInfo("Unable to find ", instrumentationPoint->ClassName, " for rejit. HR:", hr);
                } else {
                    HCORENUM enumerator = nullptr;
                    OnDestruction Conan([&] {if (enumerator) pImport->CloseEnum(enumerator); });
                    mdMethodDef methodIds[METHOD_ENUM_BATCH_SIZE];
                    for (ULONG fetchSize = 0; SUCCEEDED(pImport->EnumMethodsWithName(&enumerator, typeDef, instrumentationPoint->MethodName.c_str(), methodIds, METHOD_ENUM_BATCH_SIZE, &fetchSize)) && fetchSize;) {
                        LogDebug("Found ", fetchSize, " method(s) matching ", instrumentationPoint->MethodName);
                        for (ULONG i = 0; i < fetchSize; i++) {
                            methodDefs->emplace(methodIds[i]);
                        }
                    }
                }
            }

            return methodDefs;
        }

        void RejitModuleFunctions(ModuleID moduleId, std::shared_ptr<std::set<mdMethodDef>> methodsToRejit)
        {
            auto rejit =
                [&](ULONG numberMethods, ModuleID* moduleIds, mdMethodDef* methodIds) {
                    HRESULT hr = _corProfilerInfo4->RequestReJIT(numberMethods, moduleIds, methodIds);
                    LogDebug("ReJit ", (SUCCEEDED(hr) ? "success" : "failed"));
                };
            PerformOnMethods(moduleId, methodsToRejit, rejit);
        }

        void RevertModuleFunctions(ModuleID moduleId, std::shared_ptr<std::set<mdMethodDef>> methodsToRevert)
        {
            auto revert =
                [&](ULONG numberMethods, ModuleID* moduleIds, mdMethodDef* methodIds) {
                    HRESULT* success = nullptr;
                    HRESULT hr = _corProfilerInfo4->RequestRevert(numberMethods, moduleIds, methodIds, success);
                    LogDebug("Revert ", (SUCCEEDED(hr) ? "success" : "failed"));
                };
            PerformOnMethods(moduleId, methodsToRevert, revert);
        }

        void PerformOnMethods(ModuleID moduleId, std::shared_ptr<std::set<mdMethodDef>> methodSet,
            std::function<void(ULONG, ModuleID*, mdMethodDef*)> func)
        {
            if (methodSet != nullptr && !methodSet->empty()) {
                ULONG numberMethods = (ULONG)methodSet->size();
                ModuleID* moduleIds = new ModuleID[numberMethods];
                mdMethodDef* methodIds = new mdMethodDef[numberMethods];

                int i = 0;
                for (auto methodDef : *methodSet) {
                    moduleIds[i] = moduleId;
                    methodIds[i] = methodDef;
                    i++;
                }

                func(numberMethods, moduleIds, methodIds);

                delete[] moduleIds;
                delete[] methodIds;
            }
        }

        xstring_t GetAssemblyName(ModuleID& moduleId)
        {
            auto f = __func__;
            auto TOE = [f](HRESULT hr) { if (FAILED(hr)) { LogError("Function '", f, "' failed.  HRESULT: ", hr); throw Win32Exception(hr); } };

            AssemblyID assemblyId = 0;

            // get the assembly id
            TOE(_corProfilerInfo4->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId));

            xstring_t assemblyName;
            ULONG assemblyNameLength{};
            TOE(_corProfilerInfo4->GetAssemblyInfo(assemblyId, 0, &assemblyNameLength, nullptr, nullptr, nullptr));
            assemblyName.resize(assemblyNameLength);
            TOE(_corProfilerInfo4->GetAssemblyInfo(assemblyId, (ULONG)assemblyName.size(), nullptr, &assemblyName.front(), nullptr, nullptr));
            assemblyName.pop_back();
            assemblyName.shrink_to_fit();
            return assemblyName;
        }

        HRESULT RequestProfile(void** snapshot, int* length) noexcept
        {
            return _threadProfiler.RequestProfile(snapshot, length);
        }

        // Fires up a new thread to fetch a number of functions.  This is called from a managed thread, and as a result direct calls to GetTokenAndMetaDataFromFunction
        // will result in a CORPROF_E_UNSUPPORTED_CALL_SEQUENCE.  Moving the work to another thread makes the profiler API happy.
        HRESULT RequestFunctionNames(const UINT_PTR* functionIds, int length, void** results) noexcept
        {
            return _threadProfiler.GetTypeAndMethodNames(functionIds, length, results);
        }

        void ShutdownThreadProfiler() noexcept
        {
            _threadProfiler.Shutdown();
        }

        void ReleaseProfile() noexcept
        {
            _threadProfiler.ReleaseProfile();
        }

        uintptr_t GetCurrentThreadId() noexcept
        {
            ThreadID tid;
            if (FAILED(_corProfilerInfo4->GetCurrentThreadID(&tid))) {
                tid = 0;
            }
            return tid;
        }

        // there is only one by convention, but that is not guaranteed anywhere
        static CorProfilerCallbackImpl*& GetSingletonish()
        {
            static CorProfilerCallbackImpl* s_profiler = nullptr;
            return s_profiler;
        }

    protected:
        MethodRewriter::MethodRewriterPtr _methodRewriter;
        CComPtr<ICorProfilerInfo4> _corProfilerInfo4;
        ThreadProfiler::ThreadProfiler _threadProfiler;
        std::shared_ptr<SystemCalls> _systemCalls;
        std::shared_ptr<FunctionResolver> _functionResolver;
        MethodRewriter::CustomInstrumentationBuilder _customInstrumentationBuilder;
        MethodRewriter::CustomInstrumentation _customInstrumentation;

        DWORD _eventMask = OverrideEventMask(
            COR_PRF_MONITOR_JIT_COMPILATION | COR_PRF_MONITOR_MODULE_LOADS | COR_PRF_USE_PROFILE_IMAGES | COR_PRF_MONITOR_THREADS | COR_PRF_ENABLE_STACK_SNAPSHOT | COR_PRF_ENABLE_REJIT | (DWORD)COR_PRF_DISABLE_ALL_NGEN_IMAGES);

        xstring_t _productName = _X("");
        xstring_t _agentCoreDllPath = _X("");

        bool _isCoreClr = false;

        MethodRewriter::MethodRewriterPtr GetMethodRewriter()
        {
            return std::atomic_load(&_methodRewriter);
        }

        void SetMethodRewriter(MethodRewriter::MethodRewriterPtr methodRewriter)
        {
            std::atomic_store(&_methodRewriter, methodRewriter);
        }

        static xstring_t GetGlobalConfigurationFromDisk(std::shared_ptr<SystemCalls> systemCalls)
        {
            auto newRelicHome = GetNewRelicHomePath(systemCalls);
            auto configuration = GetConfigurationFromDisk(systemCalls, newRelicHome);

            if (!configuration.second) {
                LogError(L"The global newrelic.config file was not found at: ", newRelicHome);
                throw ProfilerException(_X("The global newrelic.config file was not found."));
            }

            return configuration.first;
        }

        static std::pair<xstring_t, bool> GetLocalConfigurationFromDisk(std::shared_ptr<SystemCalls> systemCalls)
        {
            try {
                auto applicationPath = systemCalls->GetProcessDirectoryPath();
                return GetConfigurationFromDisk(systemCalls, applicationPath);
            } catch (...) {
                LogWarn(L"There was an error reading the local newrelic.config file. Using the settings in the global newrelic.config file instead.");
                return std::make_pair(_X(""), false);
            }
        }

        static std::pair<xstring_t, bool> GetConfigurationFromDisk(std::shared_ptr<SystemCalls> systemCalls, xstring_t path)
        {
            auto configPath = path + PATH_SEPARATOR + _X("newrelic.config");

            if (systemCalls->FileExists(configPath)) {
                LogInfo(L"Found newrelic.config at: ", configPath);
                return std::make_pair(ReadFile(configPath), true);
            }

            return std::make_pair(_X(""), false);
        }

        static xstring_t GetApplicationConfigurationFromDisk(std::shared_ptr<SystemCalls> systemCalls)
        {
            auto applicationPath = systemCalls->GetProcessPath();
            auto applicationConfigPath = applicationPath + _X(".config");

            if (!systemCalls->FileExists(applicationConfigPath))
                return _X("");

            return ReadFile(applicationConfigPath);
        }

        Configuration::InstrumentationXmlSetPtr GetInstrumentationXmlsFromDisk(std::shared_ptr<SystemCalls> systemCalls)
        {
            Configuration::InstrumentationXmlSetPtr instrumentationXmls(new Configuration::InstrumentationXmlSet());

            auto filePaths = GetXmlFilesInExtensionsDirectory(systemCalls);

            for (auto filePath : filePaths) {
                instrumentationXmls->emplace(filePath, ReadFile(filePath));
            }

            return instrumentationXmls;
        }

        static xstring_t GetAppPoolId(std::shared_ptr<MethodRewriter::ISystemCalls> systemCalls)
        {
            auto appPoolId = TryGetAppPoolIdFromEnvironmentVariable(systemCalls);
            if (appPoolId.empty()) {
                appPoolId = TryGetAppPoolIdFromCommandLine();
            }
            return appPoolId;
        }

        static xstring_t TryGetAppPoolIdFromEnvironmentVariable(std::shared_ptr<MethodRewriter::ISystemCalls> systemCalls)
        {
            //This will pull the app pool name out of instances of IIS > 5.1
            //For more information see http://msdn.microsoft.com/en-us/library/ms524602(v=vs.90).aspx
            auto appPoolId = systemCalls->GetAppPoolId();
            if (appPoolId == nullptr) {
                return _X("");
            } else {
                LogInfo("The Profiler will use the environment variable APP_POOL_ID as the application name when white/black listing applications pools for profiling.");
                return *appPoolId;
            }
        }

        static xstring_t TryGetAppPoolIdFromCommandLine()
        {
#ifdef PAL_STDCPP_COMPAT
            return _X("");
#else
            int argc = 0;
            auto commandLineArgv = CommandLineToArgvW(GetCommandLineW(), &argc);

            if (argc < 3) {
                if (commandLineArgv != nullptr) LocalFree(commandLineArgv);
                return _X("");
            }

            xstring_t appPoolId = _X("");
            auto appPoolCommandLineArg = _X("-ap");
            for (int i = 0; i < argc; ++i) {
                xstring_t arg = commandLineArgv[i];
                if (arg.compare(appPoolCommandLineArg) == 0) {
                    appPoolId = commandLineArgv[i + 1];
                    if (appPoolId.length() >= 3 && appPoolId [0] == '"') {
                        appPoolId = appPoolId.substr(1, appPoolId.length() - 2);
                    }
                    LogInfo("The Profiler will use the application pool ID from the command line as the application name when white/black listing applications pools for profiling.");
                    return appPoolId;
                }
            }

            LocalFree(commandLineArgv);
            return appPoolId;
#endif
        }

        static xstring_t ReadFile(xstring_t filePath)
        {
// disabling: 'argument' : conversion from 'type1' to 'type2', possible loss of data
// Ideally we need to be able to handle file paths with multibyte characters in them. Currently, we do not.
#pragma warning(push)
#pragma warning(disable : 4244)

            // Open file to read as a binary byte stream.  This lets us reliably detect and
            // remove a UTF-8 BOM at the beginning of the file.
            // See https://github.com/newrelic/newrelic-dotnet-agent/issues/267 for context
            ifstream inFile(std::string(filePath.begin(), filePath.end()), std::ios::binary);
#pragma warning(pop)

            if (!inFile) {
                LogError(L"Unable to open file. File path: ", filePath);
                throw ProfilerException();
            }

            ostringstream charResult;
            charResult << inFile.rdbuf();
            inFile.close();

            // Detect and remove UTF-8 BOM
            auto resultCharStr = charResult.str();
            auto char1 = static_cast<unsigned char>(resultCharStr[0]);
            auto char2 = static_cast<unsigned char>(resultCharStr[1]);
            auto char3 = static_cast<unsigned char>(resultCharStr[2]);

            if (char1 == 0xEF && char2 == 0xBB && char3 == 0xBF) {
                LogDebug(L"ReadFile (", filePath, L") detected UTF-8 BOM, skipping.");
                resultCharStr.erase(0, 3);
            }

            return ToWideString(resultCharStr.c_str());
        }

        static std::unique_ptr<xstring_t> TryGetNewRelicHomeFromRegistry()
        {
            return SystemCalls::TryGetRegistryStringValue(HKEY_LOCAL_MACHINE, _X("Software\\New Relic\\.NET Agent"), _X("NewRelicHome"));
        }

        static std::unique_ptr<xstring_t> TryGetNewRelicHomeFromEnvironment(std::shared_ptr<MethodRewriter::ISystemCalls> systemCalls)
        {
            return systemCalls->GetNewRelicHomePath();
        }

        static xstring_t GetNewRelicHomePath(std::shared_ptr<MethodRewriter::ISystemCalls> systemCalls)
        {
            auto newRelicHome = TryGetNewRelicHomeFromEnvironment(systemCalls);
            if (newRelicHome != nullptr)
                return *newRelicHome;

            newRelicHome = TryGetNewRelicHomeFromRegistry();
            if (newRelicHome != nullptr)
                return *newRelicHome;

            LogError(L"Unable to find New Relic Home directory in registry or environment.");
            throw ProfilerException();
        }

        FilePaths GetXmlFilesInExtensionsDirectory(std::shared_ptr<SystemCalls> systemCalls)
        {
            auto rootExtensionsDirectory = GetNewRelicHomePath(systemCalls) + PATH_SEPARATOR + _X("extensions");
            if (!systemCalls->DirectoryExists(rootExtensionsDirectory)) {
                LogWarn(L"Unable to find the New Relic Agent extensions directory (", rootExtensionsDirectory, L").  No methods will be instrumented except those decorated with [Transaction] or [Trace] attributes in conjunction with the New Relic agent API.");
            } else {
                LogInfo(L"Loading instrumentation from ", rootExtensionsDirectory);
            }

            auto xmlFiles = GetXmlFilesInDirectory(systemCalls, rootExtensionsDirectory);

            auto runtimeExtensionsDirectory = rootExtensionsDirectory + PATH_SEPARATOR + GetRuntimeExtensionsDirectoryName();
            if (systemCalls->DirectoryExists(runtimeExtensionsDirectory)) {
                LogInfo(L"Loading instrumentation from ", runtimeExtensionsDirectory);
                auto runtimeExtensionsDirectoryXmlFiles = GetXmlFilesInDirectory(systemCalls, runtimeExtensionsDirectory);
                xmlFiles.insert(runtimeExtensionsDirectoryXmlFiles.begin(), runtimeExtensionsDirectoryXmlFiles.end());
            }

            return xmlFiles;
        }

        static FilePaths GetXmlFilesInDirectory(std::shared_ptr<SystemCalls> systemCalls, xstring_t directoryPath)
        {
            return systemCalls->GetFilesInDirectory(directoryPath, _X("xml"));
        }

        static LARGE_INTEGER GetFrequency()
        {
            LARGE_INTEGER frequency;
            ::QueryPerformanceFrequency(&frequency);
            return frequency;
        }

        static LARGE_INTEGER GetCounter()
        {
            LARGE_INTEGER timer;
            ::QueryPerformanceCounter(&timer);
            return timer;
        }

        static uint64_t GetHighResolutionTimeInMilliseconds()
        {
            static auto frequency = GetFrequency();

            double millisecondsDouble = (double)GetCounter().QuadPart * 1000 / (double)frequency.QuadPart;
            uint64_t millisecondsInteger = (uint64_t)millisecondsDouble;

            return millisecondsInteger;
        }

        void InitializeLogging()
        {
            // Only need to initialize the file if we're doing file logging
            if (nrlog::StdLog.GetEnabled() && !nrlog::StdLog.GetConsoleLogging())
            {
                try {
                    xstring_t logfilename(nrlog::DefaultFileLogLocation(_systemCalls).GetPathAndFileName());
                    std::string wlogfilename(std::begin(logfilename), std::end(logfilename));
                    nrlog::StdLog.get_dest().open(wlogfilename);
                    nrlog::StdLog.get_dest().exceptions(std::wostream::failbit | std::wostream::badbit);
                    LogInfo("Logger initialized.");
                }
                catch (...) {
                    // If we fail to create a log file, there's no sense in trying to log going forward.
                    // We want the Profiler continue to run, though, so swallow the exception
                    nrlog::StdLog.SetEnabled(false);
                }
            }
        }

        std::shared_ptr<Configuration::Configuration> InitializeConfigAndSetLogLevel()
        {
            auto globalNewRelicConfigurationXml = GetGlobalConfigurationFromDisk(_systemCalls);
            auto localNewRelicConfigurationXml = GetLocalConfigurationFromDisk(_systemCalls);
            auto applicationConfigurationXml = GetApplicationConfigurationFromDisk(_systemCalls);

            auto configuration = std::make_shared<Configuration::Configuration>(globalNewRelicConfigurationXml, localNewRelicConfigurationXml, applicationConfigurationXml, _systemCalls);
            nrlog::StdLog.SetLevel(configuration->GetLoggingLevel());
            nrlog::StdLog.SetConsoleLogging(_systemCalls->GetConsoleLoggingEnabled(configuration->GetConsoleLogging()));
            nrlog::StdLog.SetEnabled(_systemCalls->GetLoggingEnabled(configuration->GetLoggingEnabled()));
            nrlog::StdLog.SetInitalized();

            if (nrlog::StdLog.GetEnabled())
            {
                LogInfo(L"<-- New logging level set: ", nrlog::GetLevelString(nrlog::StdLog.GetLevel()));
                if (nrlog::StdLog.GetConsoleLogging())
                {
                    LogInfo(L"Console logging enabled");
                }
            }
            // While we would like to indicate that logging is disabled somehow, there's of
            // course no log to write to

            return configuration;
        }

        std::shared_ptr<Configuration::InstrumentationConfiguration> InitializeInstrumentationConfig()
        {
            auto instrumentationXmls = GetInstrumentationXmlsFromDisk(_systemCalls);
            auto instrumentationConfiguration = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationXmls);
            if (instrumentationConfiguration->GetInvalidFileCount() > 0) {
                LogWarn(L"Unable to parse one or more instrumentation files.  Live instrumentation reloading will not work until the unparsable file(s) are corrected or removed.");
            }
            LogTrace(L"Read ", instrumentationXmls->size(), " instrumentation files");
            return instrumentationConfiguration;
        }

        HRESULT InitializeAndSetAgentCoreDllPath(xstring_t expectedProductName)
        {
            auto agentCoreDllPath = GetAgentCoreDllPath();
            if (agentCoreDllPath == nullptr) {
                LogError(L"The New Relic Agent DLL is missing.");
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }
            
            if (!IsCorrectAgentProduct(*agentCoreDllPath, expectedProductName)) {
                LogError(L"Incorrect agent product");
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }

            _agentCoreDllPath = *agentCoreDllPath;

            return S_OK;
        }

        void LogMessageIfAppDomainCachingIsDisabled()
        {
            if (_systemCalls->GetIsAppDomainCachingDisabled())
            {
                LogInfo("The use of AppDomain for method information caching is disabled via the 'NEW_RELIC_DISABLE_APPDOMAIN_CACHING' environment variable.");
            }
        }

        std::unique_ptr<xstring_t> GetAgentCoreDllPath()
        {
            auto runtimeDirectoryName = GetRuntimeExtensionsDirectoryName();

            auto maybeCorePath = TryGetCorePathFromBasePath(_systemCalls->GetNewRelicInstallPath(), runtimeDirectoryName);
            if (maybeCorePath != nullptr)
                return maybeCorePath;

            maybeCorePath = TryGetCorePathFromBasePath(_systemCalls->GetNewRelicHomePath(), runtimeDirectoryName);
            if (maybeCorePath != nullptr)
                return maybeCorePath;

            auto homeEnvVar = _systemCalls->GetNewRelicHomePathVariable();
            auto installEnvVar = _systemCalls->GetNewRelicInstallPathVariable();

            LogError(L"Unable to find ", homeEnvVar, L" or ", installEnvVar, L" environment variables.  Aborting instrumentation.");
            return nullptr;
        }

        std::unique_ptr<xstring_t> TryGetCorePathFromBasePath(const std::unique_ptr<xstring_t> basePath, const xstring_t runtimeDirectoryName)
        {
            if (basePath == nullptr)
                return nullptr;

            auto installPath = *basePath + PATH_SEPARATOR + _X("NewRelic.Agent.Core.dll");
            LogDebug(L"Searching for New Relic Agent DLL at: ", installPath);
            if (_systemCalls->FileExists(installPath)) {
                return std::unique_ptr<xstring_t>(new xstring_t(installPath));
            }

            installPath = *basePath + PATH_SEPARATOR + runtimeDirectoryName + PATH_SEPARATOR + _X("NewRelic.Agent.Core.dll");
            LogDebug(L"Searching for New Relic Agent DLL at: ", installPath);
            if (_systemCalls->FileExists(installPath)) {
                return std::unique_ptr<xstring_t>(new xstring_t(installPath));
            }

            return nullptr;
        }

        // This method verifies that the dll specified by filePath has the correct Product Name.
        static bool IsCorrectAgentProduct(xstring_t filePath, xstring_t expectedProductName)
        {
#ifdef PAL_STDCPP_COMPAT
            return true;
#else
            auto size = GetFileVersionInfoSize(filePath.c_str(), NULL);
            if (size == 0) {
                return false;
            }

            auto versionInfo = new BYTE[size];

            if (!GetFileVersionInfo(filePath.c_str(), 0, size, versionInfo)) {
                delete[] versionInfo;
                return false;
            }

            struct LANGANDCODEPAGE {
                WORD wLanguage;
                WORD wCodePage;
            } * lpTranslate;

            //xstring_t expectedProductName = _X("New Relic .NET CoreCLR Agent");

            UINT languageCount = 0;
            if (VerQueryValue(versionInfo,
                    TEXT("\\VarFileInfo\\Translation"),
                    (LPVOID*)&lpTranslate,
                    &languageCount)) {
                for (UINT i = 0; i < (languageCount / sizeof(struct LANGANDCODEPAGE)); i++) {
                    TCHAR szSFI[MAX_PATH] = { 0 };
                    ::wsprintf(szSFI, _T("\\StringFileInfo\\%04X%04X\\ProductName"),
                        lpTranslate[i].wLanguage, lpTranslate[i].wCodePage);

                    UINT uLen = 0;
                    LPTSTR lpszBuf = NULL;

                    if (VerQueryValue(versionInfo, (LPTSTR)szSFI, (LPVOID*)&lpszBuf, &uLen)) {
                        if (expectedProductName == lpszBuf) {
                            void* block;
                            UINT blockSize;
                            if (VerQueryValue(versionInfo, L"\\", (LPVOID*)&block, &blockSize)) {
                                auto fileInfo = (VS_FIXEDFILEINFO*)block;

                                LogInfo(lpszBuf, L" v",
                                    std::to_wstring(HIWORD(fileInfo->dwFileVersionMS)), '.',
                                    std::to_wstring(LOWORD(fileInfo->dwFileVersionMS)), '.',
                                    std::to_wstring(HIWORD(fileInfo->dwFileVersionLS)), '.',
                                    std::to_wstring(LOWORD(fileInfo->dwFileVersionLS)));
                            } else {
                                LogInfo(lpszBuf, L" vUNKNOWN");
                            }

                            delete[] versionInfo;
                            return true;
                        } else {
                            LogError(L"Expected Agent product to be ", expectedProductName, L", but it was ", lpszBuf);

                            delete[] versionInfo;
                            return false;
                        }
                    }
                }
            }
            LogError(L"Expected Agent product to be ", expectedProductName, L", but none was found.");
            return false;
#endif
        }

        bool SetClrType(std::shared_ptr<RuntimeInfo> runtimeInfo)
        {
            if (runtimeInfo->runtimeType == COR_PRF_DESKTOP_CLR) {
                _isCoreClr = false;
            }
            else if (runtimeInfo->runtimeType == COR_PRF_CORE_CLR) {
                _isCoreClr = true;
            }
            else {
                return false;
            }

            // set systemCalls and productName
            _systemCalls->SetCoreAgent(_isCoreClr);
            _productName = _isCoreClr ? _X("New Relic .NET CoreCLR Agent") : _X("New Relic .NET Agent");

            return true;
        }

        void LogRuntimeInfo(std::shared_ptr<RuntimeInfo> runtimeInfo)
        {
            if (runtimeInfo != nullptr) {
                LogTrace(L"CLR version: ", runtimeInfo->majorVersion, L".", runtimeInfo->minorVersion);
            }
        }

        void DelayProfilerAttach()
        {
            auto profilerDelay = _systemCalls->GetProfilerDelay();
            if (profilerDelay != nullptr) {
                auto seconds = xstoi(*profilerDelay);
                std::this_thread::sleep_for(std::chrono::seconds(seconds));
            }
        }
    };

    // called by managed code to get function information from function IDs
    extern "C" __declspec(dllexport) HRESULT __cdecl InstrumentationRefresh()
    {
        LogInfo("Refreshing instrumentation");
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError("Unable to refresh instrumentation because the profiler reference is invalid.");
            return E_FAIL;
        }
        return profiler->InstrumentationRefresh();
    }

    extern "C" __declspec(dllexport) HRESULT __cdecl AddCustomInstrumentation(const char* fileName, const char* xml)
    {
        LogTrace("Adding custom instrumentation");
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError("Unable to add custom instrumentation because the profiler reference is invalid.");
            return E_FAIL;
        }
        return profiler->AddCustomInstrumentation(ToWideString(fileName), ToWideString(xml));
    }

    extern "C" __declspec(dllexport) HRESULT __cdecl ApplyCustomInstrumentation()
    {
        LogTrace("Applying custom instrumentation");
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError("Unable to apply custom instrumentation because the profiler reference is invalid.");
            return E_FAIL;
        }
        return profiler->ApplyCustomInstrumentation();
    }

    extern "C" __declspec(dllexport) void __cdecl ReleaseProfile() noexcept
    {
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError(L"ReleaseProfile: entry point called before the profiler has been initialized");
            return;
        }
        profiler->ReleaseProfile();
    }

    // called by managed code to request a thread profile
    // failureCallback error codes are 1 - stack too deep, 2 - no Stack Snapshooter supplied, or error codes returned by DoStackSnapshot
    extern "C" __declspec(dllexport) HRESULT __cdecl RequestProfile(void** snapshots, int* length) noexcept
    {
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError(L"RequestProfile: entry point called before the profiler has been initialized");
            return E_UNEXPECTED;
        }
        // call into the ThreadProfiler singleton
        return profiler->RequestProfile(snapshots, length);
    }

    // called by managed code to get function information from function IDs
    extern "C" __declspec(dllexport) HRESULT __cdecl RequestFunctionNames(UINT_PTR* functionIds, int length, void** results) noexcept
    {
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError(L"RequestFunctionNames: entry point called before the profiler has been initialized");
            return E_UNEXPECTED;
        }
        return profiler->RequestFunctionNames(functionIds, length, results);
    }

    extern "C" __declspec(dllexport) void __cdecl ShutdownThreadProfiler() noexcept
    {
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError(L"ShutdownThreadProfiler: entry point called before the profiler has been initialized");
            return;
        }
        profiler->ShutdownThreadProfiler();
    }

    //This method is used only to verify thread profiling.  It is only used by tests in ProfiledMethod project.
    extern "C" __declspec(dllexport) uintptr_t __cdecl GetCurrentExecutionEngineThreadId()
    {
        auto profiler = CorProfilerCallbackImpl::GetSingletonish();
        if (profiler == nullptr) {
            LogError(L"GetCurrentExecutionEngineThreadId: entry point called before the profiler has been initialized");
            return 0;
        }
        return profiler->GetCurrentThreadId();
    }

#pragma warning(pop)
}}
