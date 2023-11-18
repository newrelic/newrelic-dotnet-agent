// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "MockFunction.h"
#include "../MethodRewriter/Instrumentors.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(InstrumentatorsTest)
    {
    public:
        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionShouldNotBeTraced)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("NotInstrumentedMethod");
            function->_shouldTrace = false;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldTraceWhenFunctionShouldBeTraced)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("NotInstrumentedMethod");
            function->_shouldTrace = true;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsTrue(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionHasTdSequentialLayout)
        {
            auto function = std::make_shared<MockFunction>();
            function->_classAttributes = CorTypeAttr::tdSequentialLayout;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionHasMdSpecialName)
        {
            auto function = std::make_shared<MockFunction>();
            function->_methodAttributes = CorMethodAttr::mdSpecialName;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldTraceWhenFunctionHasMdSpecialNameAndIsConstructor)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X(".ctor");
            function->_methodAttributes = CorMethodAttr::mdSpecialName | CorMethodAttr::mdRTSpecialName;
            function->_shouldTrace = true;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsTrue(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionHasMdPInvokeImpl)
        {
            auto function = std::make_shared<MockFunction>();
            function->_methodAttributes = CorMethodAttr::mdPinvokeImpl;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionHasMdUnmanagedExport)
        {
            auto function = std::make_shared<MockFunction>();
            function->_methodAttributes = CorMethodAttr::mdUnmanagedExport;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionShouldInjectInstrumentation)
        {
            auto function = std::make_shared<MockFunction>();
            // This flag is confusing because it seems to do the opposite of what the name suggests but it is used
            // to track the difference between an initial JIT and a ReJIT. We inject the instrumentation on a ReJIT.
            function->_shouldInjectMethodInstrumentation = true;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(DefaultInstrumentor_ShouldNotTraceWhenFunctionIsNotValid)
        {
            auto function = std::make_shared<MockFunction>();
            function->_isValid = false;

            DefaultInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(ApiInstrumentor_ShouldNotInstrumentWhenTypeIsNotTheStubApiType)
        {
            auto function = std::make_shared<MockFunction>();
            function->_typeName = _X("NotTheApi");

            ApiInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(ApiInstrumentor_ShouldInstrumentWhenTypeIsTheStubApiType)
        {
            auto function = std::make_shared<MockFunction>();
            function->_typeName = _X("NewRelic.Api.Agent.NewRelic");

            ApiInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsTrue(result);
        }

        TEST_METHOD(ApiInstrumentor_ShouldNotInstrumentWhenMethodIsTheStaticConstructor)
        {
            auto function = std::make_shared<MockFunction>();
            function->_typeName = _X("NewRelic.Api.Agent.NewRelic");
            function->_functionName = _X(".cctor");

            ApiInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(ApiInstrumentor_ShouldNotInstrumentWhenMethodIsGetAgent)
        {
            auto function = std::make_shared<MockFunction>();
            function->_typeName = _X("NewRelic.Api.Agent.NewRelic");
            function->_functionName = _X("GetAgent");

            ApiInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInCoreClrWhenAssemblyIsMscorlib)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = _X("mscorlib.dll");

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), true, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInCoreClrWhenAssemblyIsNotSystemPrivateCoreLib)
        {
            auto function = std::make_shared<MockFunction>();

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), true, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInNetFrameworkWhenAssemblyIsNotMscorlib)
        {
            auto function = std::make_shared<MockFunction>();

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInCoreClrForTheWrongType)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = _X("System.Private.CoreLib.dll");

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), true, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInNetFrameworkForTheWrongType)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = _X("mscorlib.dll");

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInCoreClrForNonHelperMethods)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = _X("System.Private.CoreLib.dll");
            function->_typeName = _X("System.CannotUnloadAppDomainException");
            function->_functionName = _X(".ctor");

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), true, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_ShouldNotInstrumentInNetFrameworkForNonHelperMethods)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = _X("mscorlib.dll");
            function->_typeName = _X("System.CannotUnloadAppDomainException");
            function->_functionName = _X(".ctor");

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), false, AgentCallStyle::Strategy::InAgentCache);

            Assert::IsFalse(result);
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetThreadLocalBoolean)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetThreadLocalBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_SetThreadLocalBoolean)
        {
            AssertHelperMethodIsInstrumented(true, _X("SetThreadLocalBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetAppDomainBoolean)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetAppDomainBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_SetAppDomainBoolean)
        {
            AssertHelperMethodIsInstrumented(true, _X("SetAppDomainBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_LoadAssemblyOrThrow)
        {
            AssertHelperMethodIsInstrumented(true, _X("LoadAssemblyOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetTypeViaReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetTypeViaReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetMethodViaReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetMethodViaReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetMethodFromAppDomainStorage)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetMethodFromAppDomainStorage"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetMethodFromAppDomainStorageOrReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_StoreMethodInAppDomainStorageOrThrow)
        {
            AssertHelperMethodIsInstrumented(true, _X("StoreMethodInAppDomainStorageOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_InvokeAgentMethodInvokerFunc)
        {
            AssertHelperMethodIsInstrumented(true, _X("InvokeAgentMethodInvokerFunc"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetAgentShimFinishTracerDelegateFunc)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetAgentShimFinishTracerDelegateFunc"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_GetAgentMethodInvokerObject)
        {
            AssertHelperMethodIsInstrumented(true, _X("GetAgentMethodInvokerObject"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_StoreAgentMethodInvokerFunc)
        {
            AssertHelperMethodIsInstrumented(true, _X("StoreAgentMethodInvokerFunc"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_StoreAgentShimFinishTracerDelegateFunc)
        {
            AssertHelperMethodIsInstrumented(true, _X("StoreAgentShimFinishTracerDelegateFunc"));
        }

        TEST_METHOD(HelperInstrumentor_CoreClr_ShouldInstrument_EnsureInitialized)
        {
            AssertHelperMethodIsInstrumented(true, _X("EnsureInitialized"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetThreadLocalBoolean)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetThreadLocalBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_SetThreadLocalBoolean)
        {
            AssertHelperMethodIsInstrumented(false, _X("SetThreadLocalBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetAppDomainBoolean)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetAppDomainBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_SetAppDomainBoolean)
        {
            AssertHelperMethodIsInstrumented(false, _X("SetAppDomainBoolean"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_LoadAssemblyOrThrow)
        {
            AssertHelperMethodIsInstrumented(false, _X("LoadAssemblyOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetTypeViaReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetTypeViaReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetMethodViaReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetMethodViaReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetMethodFromAppDomainStorage)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetMethodFromAppDomainStorage"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetMethodFromAppDomainStorageOrReflectionOrThrow)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_StoreMethodInAppDomainStorageOrThrow)
        {
            AssertHelperMethodIsInstrumented(false, _X("StoreMethodInAppDomainStorageOrThrow"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_InvokeAgentMethodInvokerFunc)
        {
            AssertHelperMethodIsInstrumented(false, _X("InvokeAgentMethodInvokerFunc"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetAgentShimFinishTracerDelegateFunc)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetAgentShimFinishTracerDelegateFunc"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_GetAgentMethodInvokerObject)
        {
            AssertHelperMethodIsInstrumented(false, _X("GetAgentMethodInvokerObject"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_StoreAgentMethodInvokerFunc)
        {
            AssertHelperMethodIsInstrumented(false, _X("StoreAgentMethodInvokerFunc"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_StoreAgentShimFinishTracerDelegateFunc)
        {
            AssertHelperMethodIsInstrumented(false, _X("StoreAgentShimFinishTracerDelegateFunc"));
        }

        TEST_METHOD(HelperInstrumentor_NetFramework_ShouldInstrument_EnsureInitialized)
        {
            AssertHelperMethodIsInstrumented(false, _X("EnsureInitialized"));
        }

    private:
        InstrumentationSettingsPtr GetInstrumentationSettings() const
        {
            Configuration::InstrumentationXmlSetPtr xmlSet(new Configuration::InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            Configuration::InstrumentationConfigurationPtr instrumentation(new Configuration::InstrumentationConfiguration(xmlSet));
            return std::make_shared<InstrumentationSettings>(instrumentation, _X(""));
        }

        void AssertHelperMethodIsInstrumented(const bool isCoreClr, const xstring_t& functionName)
        {
            auto function = std::make_shared<MockFunction>();
            function->_assemblyName = function->_moduleName = isCoreClr ? _X("System.Private.CoreLib.dll") : _X("mscorlib.dll");
            function->_typeName = _X("System.CannotUnloadAppDomainException");
            function->_functionName = functionName;

            HelperInstrumentor instrumentor;

            auto result = instrumentor.Instrument(function, GetInstrumentationSettings(), isCoreClr, AgentCallStyle::Strategy::InAgentCache);

            // The method returns false even in the success case
            Assert::IsFalse(result);
        }
    };
}}}}
