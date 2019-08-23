#pragma once

#include "ICorProfilerCallbackBase.h"

#ifdef PAL_STDCPP_COMPAT
#include "UnixSystemCalls.h"
#else
#include "SystemCalls.h"
#endif

namespace NewRelic { namespace Profiler {

	class CoreCLRCorProfilerCallbackImpl : public ICorProfilerCallbackBase {

	public:
		CoreCLRCorProfilerCallbackImpl()
			: ICorProfilerCallbackBase(
#ifdef PAL_STDCPP_COMPAT
				  std::make_shared<SystemCalls>()
#else
				  std::make_shared<SystemCalls>(_X("CORECLR_NEWRELIC_HOME"), _X("CORECLR_NEWRELIC_INSTALL_PATH"))
#endif
			  )
		{
			GetSingletonish() = this;
			_productName = _X("New Relic .NET CoreCLR Agent");
		}

		~CoreCLRCorProfilerCallbackImpl()
		{
			if (GetSingletonish() == this)
				GetSingletonish() = nullptr;
		}

		virtual void ConfigureEventMask(IUnknown* pICorProfilerInfoUnk) override
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
			} else {
				LogDebug(L"Calling SetEventMask2().");
				ThrowOnError(_corProfilerInfo5->SetEventMask2, _eventMask, COR_PRF_HIGH_DISABLE_TIERED_COMPILATION);
			}
		}

		virtual bool ShouldInstrument() override
		{
			auto commandLine = _systemCalls->GetProgramCommandLine();

			LogInfo(L"Command line: ", commandLine);
			
			auto shouldNotInstrument = _methodRewriter->ShouldNotInstrumentCommandNetCore(commandLine);

			if (shouldNotInstrument) {

				LogInfo(L"Unloading Profiler - Command line not identified as valid invocation for instrumentation.");
				return false;
			}

			return true;
		}

		virtual HRESULT
		MinimumDotnetVersionCheck(IUnknown* pICorProfilerInfoUnk) override
		{
			CComPtr<ICorProfilerInfo8> temp;
			HRESULT result = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo8), (void**)&temp);
			if (FAILED(result)) {
				LogError(_X(".NET Core 2.0 or greater required. Profiler not attaching."));
				return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
			}
			return S_OK;
		}

		//
		// On CoreCLR we aren't seeing JITs for all of the methods that we're interested in.
		// To work around that, we rejit all of the methods of interest in an assembly when
		// its module loads.
		//
		// ICorProfilerCallback
		virtual HRESULT __stdcall ModuleLoadFinished(ModuleID moduleId, HRESULT status) override
		{
			if (SUCCEEDED(status)) {
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
				} catch (...) {
				}
			}
			return S_OK;
		}
	};
}
}