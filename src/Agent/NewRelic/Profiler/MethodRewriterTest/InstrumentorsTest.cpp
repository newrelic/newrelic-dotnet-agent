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

        // .NET 11 runtime-async methods (MethodImplAttributes.Async, 0x2000 in ImplFlags -- note
        // that mdPinvokeImpl above is also 0x2000, but in methodAttributes, a different field) do
        // not follow the return convention their signature declares: the body pushes nothing for
        // Task/ValueTask and an unwrapped T for Task<T>/ValueTask<T>. See NR-610232.
        //
        // The rewriter handles those four shapes (RuntimeAsyncReturnType.h). Anything else is
        // declined, because the Async flag can be set but inert and rewriting a method that really
        // uses the synchronous convention would inject the InvalidProgramException we are
        // preventing. These tests cover both sides of that split.

        // Makes MockFunction look like a method returning a task type: a 0-parameter signature whose
        // return type is ELEMENT_TYPE_CLASS with a token the resolver answers with typeName.
        static void MakeTaskReturning(std::shared_ptr<MockFunction> func, const wchar_t* typeName)
        {
            auto resolver = std::make_shared<MockTokenResolver>();
            resolver->_typeString = typeName;
            func->_tokenResolver = resolver;

            func->_signature = std::make_shared<ByteVector>();
            func->_signature->push_back(0x00);  // default calling convention
            func->_signature->push_back(0x00);  // 0 parameters
            func->_signature->push_back(0x12);  // return type: ELEMENT_TYPE_CLASS
            func->_signature->push_back(0x49);  // class token (compressed 0x01000012)
        }

        TEST_METHOD(default_runtime_async_unrecognized_return_type_returns_false)
        {
            // MockFunction's stock signature returns void, which no runtime-async method can --
            // exactly the inert-flag shape that must be declined rather than rewritten
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_runtime_async_unrecognized_return_type_does_not_call_write_method)
        {
            // the return value only says we declined; this proves no IL was actually rewritten
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            auto settings = MakeMatchingSettings(func);
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            DefaultInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(writeMethodCalled, L"an unrecognized runtime-async shape must not be rewritten");
        }

        TEST_METHOD(default_runtime_async_unrecognized_return_type_skipped_even_when_should_trace)
        {
            // no matching instrumentation point, so this takes the ShouldTrace branch that
            // synthesizes one -- the [Transaction]/[Trace] attribute path, most exposed today
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            func->_shouldTrace = true;
            auto settings = MakeSettings(false);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
        }

        TEST_METHOD(default_runtime_async_task_is_instrumented)
        {
            // the shape the attached repro hits: async Task, whose body pushes nothing
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            MakeTaskReturning(func, L"System.Threading.Tasks.Task");
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result, L"a runtime-async Task method is a shape the rewriter understands");
        }

        TEST_METHOD(default_runtime_async_task_calls_write_method)
        {
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            MakeTaskReturning(func, L"System.Threading.Tasks.Task");
            auto settings = MakeMatchingSettings(func);
            bool writeMethodCalled = false;
            func->_writeMethodHandler = [&writeMethodCalled](const ByteVector&) {
                writeMethodCalled = true;
            };
            DefaultInstrumentor instr;
            instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(writeMethodCalled, L"a runtime-async Task method must actually be rewritten");
        }

        TEST_METHOD(default_runtime_async_value_task_is_instrumented)
        {
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = true;
            MakeTaskReturning(func, L"System.Threading.Tasks.ValueTask");
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result, L"a runtime-async ValueTask method is a shape the rewriter understands");
        }

        TEST_METHOD(default_task_returning_method_without_the_flag_is_instrumented)
        {
            // the same signature without the Async impl flag is an ordinary Task-returning method
            // and must keep going down the unchanged synchronous path
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = false;
            MakeTaskReturning(func, L"System.Threading.Tasks.Task");
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
        }

        TEST_METHOD(default_not_runtime_async_still_instruments)
        {
            // guards against the runtime-async check being inverted
            auto func = std::make_shared<MockFunction>();
            func->_isRuntimeAsync = false;
            auto settings = MakeMatchingSettings(func);
            DefaultInstrumentor instr;
            bool result = instr.Instrument(func, settings, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsTrue(result);
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

        TEST_METHOD(helper_cctor_does_not_fire)
        {
            // .cctor is no longer a recognized helper -- it must not pass the allow-list.
            HelperInstrumentor instr;
            auto func = MakeHelperFunc(L".cctor");
            bool result = instr.Instrument(func, nullptr, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            Assert::IsFalse(result);
            Assert::AreEqual((uint64_t)0, instr.GetHelperFireCount());
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
