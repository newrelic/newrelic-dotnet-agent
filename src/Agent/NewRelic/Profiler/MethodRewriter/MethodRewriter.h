/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Common/Macros.h"
#include "../Common/xplat.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../Logging/Logger.h"
#include "Exceptions.h"
#include "FunctionManipulator.h"
#include "IFunction.h"
#include "Instrumentors.h"
#include <iomanip>
#include <memory>
#include <stdint.h>
#include <string>
#include <unordered_map>

#include "../Configuration/Strings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter {

    class MethodRewriter {
    public:
        MethodRewriter(Configuration::InstrumentationConfigurationPtr instrumentationConfiguration, const xstring_t& corePath)
            : _instrumentationConfiguration(instrumentationConfiguration)
            , _instrumentedAssemblies(new std::set<xstring_t>())
            , _instrumentedFunctionNames(new std::set<xstring_t>())
            , _instrumentedTypes(new std::set<xstring_t>())
            , _helperInstrumentor(std::make_unique<HelperInstrumentor>())
            , _apiInstrumentor(std::make_unique<ApiInstrumentor>())
            , _defaultInstrumentor(std::make_unique<DefaultInstrumentor>())
            , _corePath(corePath)
        {
            Initialize();
        }

        void Initialize()
        {
            // We have to instrument mscorlib to add our hooks.  Yes, this is a little brittle
            // and it should probably live closer to the code that mucks with these methods.
            _instrumentedAssemblies->emplace(_X("mscorlib"));
            _instrumentedTypes->emplace(_X("System.CannotUnloadAppDomainException"));
            _instrumentedFunctionNames->emplace(_X("GetAppDomainBoolean"));
            _instrumentedFunctionNames->emplace(_X("GetThreadLocalBoolean"));
            _instrumentedFunctionNames->emplace(_X("SetThreadLocalBoolean"));
            _instrumentedFunctionNames->emplace(_X("GetMethodFromAppDomainStorageOrReflectionOrThrow"));
            _instrumentedFunctionNames->emplace(_X("GetMethodFromAppDomainStorage"));
            _instrumentedFunctionNames->emplace(_X("GetMethodViaReflectionOrThrow"));
            _instrumentedFunctionNames->emplace(_X("GetTypeViaReflectionOrThrow"));
            _instrumentedFunctionNames->emplace(_X("LoadAssemblyOrThrow"));
            _instrumentedFunctionNames->emplace(_X("StoreMethodInAppDomainStorageOrThrow"));

            auto instrumentationPoints = _instrumentationConfiguration->GetInstrumentationPoints();

            for (auto instrumentationPoint : *instrumentationPoints) {

                _instrumentedAssemblies->emplace(instrumentationPoint->AssemblyName);
                _instrumentedFunctionNames->emplace(instrumentationPoint->MethodName);
                _instrumentedTypes->emplace(instrumentationPoint->ClassName);
            }
        }

        virtual ~MethodRewriter()
        {
        }

        Configuration::InstrumentationConfigurationPtr GetInstrumentationConfiguration()
        {
            return _instrumentationConfiguration;
        }

        std::set<Configuration::InstrumentationPointPtr> GetAssemblyInstrumentation(xstring_t assemblyName)
        {
            std::set<Configuration::InstrumentationPointPtr> set;
            for (auto instrumentationPoint : *_instrumentationConfiguration->GetInstrumentationPoints().get()) {
                if (assemblyName == instrumentationPoint->AssemblyName) {
                    set.emplace(instrumentationPoint);
                }
            }
            return set;
        }

        bool ShouldInstrumentAssembly(xstring_t assemblyName)
        {
            return InSet(_instrumentedAssemblies, assemblyName);
        }

        bool ShouldInstrumentType(xstring_t typeName)
        {
            return InSet(_instrumentedTypes, typeName);
        }

        bool ShouldInstrumentFunction(xstring_t functionName)
        {
            return InSet(_instrumentedFunctionNames, functionName);
        }

        // instrument the provided method (if necessary)
        void Instrument(IFunctionPtr function)
        {
            LogTrace("Possibly instrumenting: ", function->ToString());

            InstrumentationSettingsPtr instrumentationSettings = std::make_shared<InstrumentationSettings>(_instrumentationConfiguration, _corePath);

            if (_helperInstrumentor->Instrument(function, instrumentationSettings) || _apiInstrumentor->Instrument(function, instrumentationSettings) || _defaultInstrumentor->Instrument(function, instrumentationSettings)) {
            }
        }

    private:
        xstring_t _corePath;
        Configuration::InstrumentationConfigurationPtr _instrumentationConfiguration;
        std::shared_ptr<std::set<xstring_t>> _instrumentedAssemblies;
        std::shared_ptr<std::set<xstring_t>> _instrumentedTypes;
        std::shared_ptr<std::set<xstring_t>> _instrumentedFunctionNames;

        std::unique_ptr<HelperInstrumentor> _helperInstrumentor;
        std::unique_ptr<ApiInstrumentor> _apiInstrumentor;
        std::unique_ptr<DefaultInstrumentor> _defaultInstrumentor;

        static bool InSet(std::shared_ptr<std::set<xstring_t>> set, xstring_t value)
        {
            return set.get()->find(value) != set.get()->end();
        }
    };
    typedef std::shared_ptr<MethodRewriter> MethodRewriterPtr;

}}}
