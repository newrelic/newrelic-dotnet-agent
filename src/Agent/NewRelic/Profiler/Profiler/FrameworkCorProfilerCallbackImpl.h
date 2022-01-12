// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include "ICorProfilerCallbackBase.h"
#include "../ModuleInjector/ModuleInjector.h"
#include "Module.h"

namespace NewRelic { namespace Profiler
{
    class FrameworkCorProfilerCallbackImpl : public ICorProfilerCallbackBase
    {

    private:
        std::shared_ptr<ModuleInjector::ModuleInjector> _moduleInjector;

    public:
        FrameworkCorProfilerCallbackImpl()
            : ICorProfilerCallbackBase()
        {
            GetSingletonish() = this;
        }

        ~FrameworkCorProfilerCallbackImpl()
        {
            if (GetSingletonish() == this)
                GetSingletonish() = nullptr;
        }

        // The Framework version of ConfigureEventMask doesn't need the pICorProfilerInfoUnk
        // but the Core version does, hence disabling the warning about 'unreferenced formal parameter'
#pragma warning (push)
#pragma warning (disable : 4100)
        virtual void ConfigureEventMask(IUnknown* pICorProfilerInfoUnk) override
        {
            // register for events that we are interested in getting callbacks for
            LogDebug(L"Calling SetEventMask().");
            ThrowOnError(_corProfilerInfo4->SetEventMask, _eventMask);
        }
#pragma warning (pop)

        virtual xstring_t GetRuntimeExtensionsDirectoryName() override
        {
            return _X("netframework");
        }

        virtual HRESULT MinimumDotnetVersionCheck(IUnknown* pICorProfilerInfoUnk) override
        {
            CComPtr<ICorProfilerInfo4> temp;
            HRESULT interfaceCheckResult = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**)&temp);
            if (FAILED(interfaceCheckResult)) {
                LogError(_X(".NET Framework 4.5 is required.  Detaching New Relic profiler."));
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }

            return S_OK;
        }

        // ICorProfilerCallback
        virtual HRESULT __stdcall ModuleLoadFinished(ModuleID moduleId, HRESULT status) override
        {
            // if the module did not load correctly then we don't want to mess with it
            if (FAILED(status))
            {
                return status;
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
            return S_OK;
        }

        virtual DWORD OverrideEventMask(DWORD eventMask) override
        {
            _moduleInjector.reset(new ModuleInjector::ModuleInjector());
            return eventMask;
        }

    };
}}
