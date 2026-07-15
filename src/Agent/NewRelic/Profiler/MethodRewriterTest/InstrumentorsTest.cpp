// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <functional>
#include <memory>
#include <stdint.h>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include "CppUnitTest.h"
#include "MockFunction.h"
#include "UnreferencedFunctions.h"
#include "../MethodRewriter/Instrumentors.h"
#include "../MethodRewriter/InstrumentationSettings.h"
#include "../Configuration/InstrumentationConfiguration.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(DefaultInstrumentorTest)
    {
    public:
        TEST_METHOD(default_no_instrumentation_point_no_trace_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            // func->_shouldTrace defaults to false, no matching instrumentation point
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_should_trace_returns_true)
        {
            auto func = std::make_shared<MockFunction>();
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(default_should_trace_calls_write_method)
        {
            auto func = std::make_shared<MockFunction>();
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            DefaultInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(writeMethodCalled);
        }

        TEST_METHOD(default_instrumentation_point_returns_true)
        {
            auto func = std::make_shared<MockFunction>();
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(default_instrumentation_point_calls_write_method)
        {
            auto func = std::make_shared<MockFunction>();
            auto settings = MakeMatchingSettings(func);
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            DefaultInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(writeMethodCalled);
        }

        TEST_METHOD(default_sequential_layout_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            // tdSequentialLayout = 0x00000008
            func->_classAttributes = 0x00000008;
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_special_name_non_ctor_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            // mdSpecialName = 0x0800, function name is not .ctor
            func->_methodAttributes = 0x0800;
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_special_name_ctor_continues_instrumentation)
        {
            auto func = std::make_shared<MockFunction>();
            // mdSpecialName = 0x0800, but function IS .ctor so special name check is skipped
            func->_methodAttributes = 0x0800;
            func->_functionName = L".ctor";
            auto settings = MakeMatchingCtorSettings();
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(default_pinvoke_impl_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            // mdPinvokeImpl = 0x2000
            func->_methodAttributes = 0x2000;
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_unmanaged_export_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            // mdUnmanagedExport = 0x0008
            func->_methodAttributes = 0x0008;
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_should_inject_method_instrumentation_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            func->_shouldInjectMethodInstrumentation = true;
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_invalid_method_returns_false)
        {
            auto func = std::make_shared<MockFunction>();
            func->_isValid = false;
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_no_write_method_when_no_match_no_trace)
        {
            auto func = std::make_shared<MockFunction>();
            auto settings = MakeSettings(false);
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            DefaultInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(writeMethodCalled);
        }

    private:
        static InstrumentationSettingsPtr MakeSettings(bool addMatchingPoint)
        {
            auto points = std::make_shared<Configuration::InstrumentationPointSet>();
            if (addMatchingPoint)
            {
                auto func = std::make_shared<MockFunction>();
                points->insert(func->GetInstrumentationPoint());
            }
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(points, nullptr);
            return std::make_shared<InstrumentationSettings>(instrumentation, _X(""));
        }

        static InstrumentationSettingsPtr MakeMatchingSettings(std::shared_ptr<MockFunction> func)
        {
            auto points = std::make_shared<Configuration::InstrumentationPointSet>();
            points->insert(func->GetInstrumentationPoint());
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(points, nullptr);
            return std::make_shared<InstrumentationSettings>(instrumentation, _X(""));
        }

        // Creates a settings object with an instrumentation point for a .ctor method
        static InstrumentationSettingsPtr MakeMatchingCtorSettings()
        {
            auto ip = std::make_shared<Configuration::InstrumentationPoint>();
            ip->AssemblyName = _X("MyAssembly");
            ip->ClassName = _X("MyNamespace.MyClass");
            ip->MethodName = _X(".ctor");
            auto points = std::make_shared<Configuration::InstrumentationPointSet>();
            points->insert(ip);
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(points, nullptr);
            return std::make_shared<InstrumentationSettings>(instrumentation, _X(""));
        }
    };


    TEST_CLASS(ApiInstrumentorTest)
    {
    public:
        TEST_METHOD(api_wrong_type_returns_false)
        {
            // default MockFunction type is "MyNamespace.MyClass", not NewRelic.Api.Agent.NewRelic
            auto func = std::make_shared<MockFunction>();
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(api_newrelic_type_cctor_returns_false)
        {
            auto func = MakeApiFunc(L".cctor");
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(api_newrelic_type_get_agent_returns_false)
        {
            auto func = MakeApiFunc(L"GetAgent");
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(api_newrelic_type_method_returns_true)
        {
            auto func = MakeApiFunc(L"NoticeError");
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(api_newrelic_type_method_calls_write_method)
        {
            auto func = MakeApiFunc(L"NoticeError");
            auto settings = MakeEmptySettings();
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            ApiInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(writeMethodCalled);
        }

        TEST_METHOD(api_newrelic_type_set_transaction_name_returns_true)
        {
            auto func = MakeApiFunc(L"SetTransactionName");
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(api_newrelic_type_add_custom_attribute_returns_true)
        {
            auto func = MakeApiFunc(L"AddCustomAttribute");
            auto settings = MakeEmptySettings();
            ApiInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(api_newrelic_type_no_write_method_when_cctor)
        {
            auto func = MakeApiFunc(L".cctor");
            auto settings = MakeEmptySettings();
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            ApiInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(writeMethodCalled);
        }

    private:
        static std::shared_ptr<MockFunction> MakeApiFunc(const wchar_t* funcName)
        {
            auto func = std::make_shared<MockFunction>();
            func->_typeName = L"NewRelic.Api.Agent.NewRelic";
            func->_functionName = funcName;
            return func;
        }

        static InstrumentationSettingsPtr MakeEmptySettings()
        {
            auto points = std::make_shared<Configuration::InstrumentationPointSet>();
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(points, nullptr);
            return std::make_shared<InstrumentationSettings>(instrumentation, _X(""));
        }
    };


    TEST_CLASS(HelperInstrumentorTest)
    {
    public:
        TEST_METHOD(helper_fire_count_starts_at_zero)
        {
            HelperInstrumentor instr;
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_wrong_module_returns_false)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            // default module "MyModule" does not end with mscorlib.dll
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = L"GetThreadLocalBoolean";
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_coreclr_wrong_module_returns_false)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            // isCoreClr=true but module is mscorlib.dll, not System.Private.CoreLib.dll
            func->_moduleName = L"mscorlib.dll";
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = L"GetThreadLocalBoolean";
            bool result = instr.Instrument(func, nullptr, true, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_coreclr_correct_module_fires)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetThreadLocalBoolean", true);
            bool result = instr.Instrument(func, nullptr, true, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_wrong_type_returns_false)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = L"mscorlib.dll";
            // wrong type -- not System.CannotUnloadAppDomainException
            func->_typeName = L"System.Object";
            func->_functionName = L"GetThreadLocalBoolean";
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_unknown_function_returns_false)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = L"mscorlib.dll";
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = L"NotAHelperMethod";
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetThreadLocalBoolean_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetThreadLocalBoolean");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_SetThreadLocalBoolean_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"SetThreadLocalBoolean");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetAppDomainBoolean_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetAppDomainBoolean");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_SetAppDomainBoolean_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"SetAppDomainBoolean");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_LoadAssemblyOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"LoadAssemblyOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetTypeViaReflectionOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetTypeViaReflectionOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetMethodViaReflectionOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetMethodViaReflectionOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetMethodFromAppDomainStorage_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetMethodFromAppDomainStorage");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetMethodFromAppDomainStorageOrReflectionOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetMethodFromAppDomainStorageOrReflectionOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_StoreMethodInAppDomainStorageOrThrow_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"StoreMethodInAppDomainStorageOrThrow");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetAgentShimFinishTracerDelegateFunc_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetAgentShimFinishTracerDelegateFunc");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_StoreAgentShimFinishTracerDelegateFunc_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"StoreAgentShimFinishTracerDelegateFunc");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_InvokeAgentMethodInvokerFunc_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"InvokeAgentMethodInvokerFunc");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_GetAgentMethodInvokerObject_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetAgentMethodInvokerObject");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_StoreAgentMethodInvokerFunc_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"StoreAgentMethodInvokerFunc");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_EnsureInitialized_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"EnsureInitialized");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_cctor_fires_and_returns_false)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L".cctor");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_fire_count_accumulates_across_multiple_calls)
        {
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetThreadLocalBoolean");
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::AreEqual((uint64_t)3, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_fire_count_not_incremented_on_wrong_module)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = L"SomeOther.dll";
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = L"GetThreadLocalBoolean";
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_fire_count_not_incremented_on_wrong_type)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = L"mscorlib.dll";
            func->_typeName = L"System.Exception";
            func->_functionName = L"GetThreadLocalBoolean";
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_fire_count_not_incremented_on_unknown_function)
        {
            HelperInstrumentor instr;
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = L"mscorlib.dll";
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = L"UnknownHelperMethod";
            instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
        }

        TEST_METHOD(helper_return_value_is_always_false_not_true)
        {
            // HelperInstrumentor always returns false, even when it matches and instruments
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L"GetThreadLocalBoolean");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result, L"HelperInstrumentor must always return false");
        }

        TEST_METHOD(helper_isCoreClr_true_uses_system_private_corelib_module)
        {
            HelperInstrumentor instr;
            // With isCoreClr=true, the expected module name is System.Private.CoreLib.dll
            auto func = MakeHelperFunc(L"SetThreadLocalBoolean", true);
            bool result = instr.Instrument(func, nullptr, true, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)1, instr.GetHelperFireCount());
        }

    private:
        static std::shared_ptr<MockFunction> MakeHelperFunc(const wchar_t* funcName, bool isCoreClr = false)
        {
            auto func = std::make_shared<MockFunction>();
            func->_moduleName = isCoreClr ? L"System.Private.CoreLib.dll" : L"mscorlib.dll";
            func->_typeName = L"System.CannotUnloadAppDomainException";
            func->_functionName = funcName;
            return func;
        }
    };
}}}}
