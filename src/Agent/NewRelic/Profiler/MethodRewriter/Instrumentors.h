/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include "../Logging/Logger.h"
#include "../Common/Strings.h"
#include "IFunction.h"
#include "AgentCallStyle.h"
#include "FunctionManipulator.h"
#include "ApiFunctionManipulator.h"
#include "HelperFunctionManipulator.h"
#include "InstrumentFunctionManipulator.h"
#include "RuntimeAsyncReturnType.h"
#include "../Configuration/InstrumentationPoint.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../Common/CorStandIn.h"
#include "InstrumentationSettings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // Interface for different classes that can all instrument a function
    struct IInstrumentor
    {
        virtual bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) = 0;
    };

    // The default instrumentor, injects our usual set of bytes into the user's function
    struct DefaultInstrumentor : public IInstrumentor
    {
        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) override
        {
            auto instrumentationPoint = instrumentationSettings->GetInstrumentationConfiguration()->TryGetInstrumentationPoint(function);
            if (instrumentationPoint == nullptr)
            {
                if (!function->ShouldTrace())
                {
                    LogTrace(L"No instrumentation point for ", function->ToString());
                    return false;
                }

                instrumentationPoint = std::make_shared<Configuration::InstrumentationPoint>();
                instrumentationPoint->AssemblyName = function->GetAssemblyName();
                instrumentationPoint->ClassName = function->GetTypeName();
                instrumentationPoint->MethodName = function->GetFunctionName();
                instrumentationPoint->TracerFactoryName = _X("NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory");
                instrumentationPoint->TracerFactoryArgs = 0;
            }

            instrumentationPoint->TracerFactoryArgs |= function->GetTracerFlags();

            if (IsTdSequentialLayout(function->GetClassAttributes())) {
                LogError(L"Skipping sequential layout method: ", function->ToString());
                return false;
            }
            // some special name methods seem to give us trouble, but allow constructors to
            // be instrumented
            if (IsMdSpecialName(function->GetMethodAttributes()) &&
                    function->GetFunctionName() != _X(".ctor")) {
                LogError(L"Skipping SpecialName method: ", function->ToString());
                return false;
            }
            if (IsMdPinvokeImpl(function->GetMethodAttributes()) || IsMdUnmanagedExport(function->GetMethodAttributes())) {
                LogError(L"Skipping interop method: ", function->ToString());
                return false;
            }
            // A .NET 11 runtime-async method returns its unwrapped type rather than the task type
            // its signature declares. The rewriter handles that for the four return types the spec
            // permits (Task, ValueTask, Task<T>, ValueTask<T>) by substituting an effective return
            // type; see RuntimeAsyncReturnType.h.
            //
            // Any other shape is declined. MethodImplAttributes.Async can be set but inert -- the
            // spec says it "only has effect" on Task/ValueTask returns -- and applying the async
            // return convention to a method that actually uses the synchronous one would inject the
            // very InvalidProgramException this code prevents. Losing telemetry on an unknown shape
            // is the safe trade; the warning tells us if it ever actually happens.
            //
            // Must stay above the ShouldInjectMethodInstrumentation() call so we don't request a
            // rejit we won't honor. See NR-610232.
            if (function->IsRuntimeAsync() &&
                    RuntimeAsync::GetEffectiveReturnTypeFromSignature(function->GetSignature(), function->GetTokenResolver()) == nullptr) {
                LogWarn(L"Skipping runtime-async method with an unrecognized return type: ", function->ToString());
                return false;
            }

            // this call will have the side effect of triggering a rejit if this is the initial JIT in a rejit enabled environment
            if (function->ShouldInjectMethodInstrumentation())
            {
                return false;
            }

            LogInfo(L"Instrumenting method: ", function->ToString());

            InstrumentFunctionManipulator manipulator(function, instrumentationSettings, isCoreClr, agentCallStrategy);
            if (!function->IsValid()) {
                // we might have mucked the method up trying to re-write multiple RETs
                LogInfo(L"Skipping invalid method: ", function->ToString());
                return false;
            }
            else {
                manipulator.InstrumentDefault(instrumentationPoint);
                return true;
            }
        }
    };



    // An instrumentor for the New Relic API functions
    struct ApiInstrumentor : public IInstrumentor
    {
        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) override
        {
            if (function->GetTypeName() == _X("NewRelic.Api.Agent.NewRelic"))
            {
                auto functionName = function->GetFunctionName();
                if (functionName == _X(".cctor") || functionName == _X("GetAgent")) {
                    LogDebug(L"Skipping instrumenting API method: ", function->ToString());
                    return false;
                }

                LogInfo(L"Instrumenting API method: ", function->ToString());
                ApiFunctionManipulator manipulator(function, instrumentationSettings, isCoreClr, agentCallStrategy);
                manipulator.InstrumentApi();
                return true;
            }
            else
            {
                return false;
            }
        }
    };


    // An instrumentor for the methods we inject into mscorlib
    struct HelperInstrumentor : public IInstrumentor
    {
        // Proxy engagement counter: counts total HelperInstrumentor dispatch events.
        // A nonzero value proves the mscorlib helper injection code path is engaged.
        // Used by CorProfilerCallbackImpl to log AppDomainFallbackCache engagement.
        // See AppDomainFallbackCache design in HelperFunctionManipulator.h.
        // Note: this counter resets to 0 if MethodRewriter is re-instantiated during
        // an instrumentation refresh; it is a per-MethodRewriter-instance count,
        // not a process-lifetime count.
        uint64_t GetHelperFireCount() const { return _helperFireCount.load(); }

        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) override
        {
            const auto expectedHelperAssemblyName = isCoreClr ? _X("System.Private.CoreLib.dll") : _X("mscorlib.dll");
            if (!Strings::EndsWith(function->GetModuleName(), expectedHelperAssemblyName))
                return false;

            if (function->GetTypeName() != _X("System.CannotUnloadAppDomainException"))
                return false;

            if (function->GetFunctionName() != _X("GetThreadLocalBoolean") &&
                function->GetFunctionName() != _X("SetThreadLocalBoolean") &&
                function->GetFunctionName() != _X("GetAppDomainBoolean") &&
                function->GetFunctionName() != _X("SetAppDomainBoolean") &&
                function->GetFunctionName() != _X("LoadAssemblyOrThrow") &&
                function->GetFunctionName() != _X("GetTypeViaReflectionOrThrow") &&
                function->GetFunctionName() != _X("GetMethodViaReflectionOrThrow") &&
                function->GetFunctionName() != _X("StoreMethodInAppDomainStorageOrThrow") &&
                function->GetFunctionName() != _X("GetAgentShimFinishTracerDelegateFunc") &&
                function->GetFunctionName() != _X("StoreAgentShimFinishTracerDelegateFunc") &&
                function->GetFunctionName() != _X("InvokeAgentShimFinishTracerDelegateFunc") &&
                function->GetFunctionName() != _X("InvokeAgentMethodInvokerFunc") &&
                function->GetFunctionName() != _X("GetAgentMethodInvokerObject") &&
                function->GetFunctionName() != _X("StoreAgentMethodInvokerFunc") &&
                function->GetFunctionName() != _X("EnsureInitialized"))
                return false;

            ++_helperFireCount;
            LogInfo(L"Instrumenting helper method: ", function->ToString());
            HelperFunctionManipulator manipulator(function, isCoreClr, agentCallStrategy);
            manipulator.InstrumentHelper();
            return false;
        }

    private:
        std::atomic<uint64_t> _helperFireCount{0};
    };
}}}
