/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include "../Logging/Logger.h"
#include "../Common/Strings.h"
#include "IFunction.h"
#include "FunctionManipulator.h"
#include "ApiFunctionManipulator.h"
#include "HelperFunctionManipulator.h"
#include "InstrumentFunctionManipulator.h"
#include "../Configuration/InstrumentationPoint.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../Common/CorStandIn.h"
#include "InstrumentationSettings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // Interface for different classes that can all instrument a function
    struct IInstrumentor
    {
        virtual bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings) = 0;
    };

    // The default instrumentor, injects our usual set of bytes into the user's function
    struct DefaultInstrumentor : public IInstrumentor
    {
        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings) override
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
                    !wcscmp(function->GetFunctionName().c_str(), _X(".ctor"))) {
                LogError(L"Skipping SpecialName method: ", function->ToString());
                return false;
            }
            if (IsMdPinvokeImpl(function->GetMethodAttributes()) || IsMdUnmanagedExport(function->GetMethodAttributes())) {
                LogError(L"Skipping interop method: ", function->ToString());
                return false;
            }

            // this call will have the side effect of triggering a rejit if this is the initial JIT in a rejit enabled environment
            if (function->ShouldInjectMethodInstrumentation())
            {
                return false;
            }

            LogInfo(L"Instrumenting method: ", function->ToString());

            InstrumentFunctionManipulator manipulator(function, instrumentationSettings);
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
        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings) override
        {
            if (function->GetTypeName() == _X("NewRelic.Api.Agent.NewRelic"))
            {
                auto functionName = function->GetFunctionName();
                if (functionName == _X(".cctor") || functionName == _X("GetAgent")) {
                    LogDebug(L"Skipping instrumenting API method: ", function->ToString());
                    return false;
                }

                LogInfo(L"Instrumenting API method: ", function->ToString());
                ApiFunctionManipulator manipulator(function, instrumentationSettings);
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
        bool Instrument(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings) override
        {
            if (!Strings::EndsWith(function->GetModuleName(), _X("mscorlib.dll")))
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
                function->GetFunctionName() != _X("GetMethodFromAppDomainStorage") &&
                function->GetFunctionName() != _X("GetMethodFromAppDomainStorageOrReflectionOrThrow") &&
                function->GetFunctionName() != _X("StoreMethodInAppDomainStorageOrThrow"))
                return false;

            LogInfo(L"Instrumenting helper method: ", function->ToString());
            HelperFunctionManipulator manipulator(function);
            manipulator.InstrumentHelper();
            return false;
        }
    };
}}}
