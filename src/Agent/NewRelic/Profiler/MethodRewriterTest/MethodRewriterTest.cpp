// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <exception>
#include <functional>
#include <memory>
#include <stdint.h>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#define LOGGER_DEFINE_STDLOG
#include "../MethodRewriter/MethodRewriter.h"
#include "CppUnitTest.h"
#include "MockFunction.h"
#include "UnreferencedFunctions.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(MethodRewriterTest)
    {
    public:
        TEST_METHOD(overloads_are_instrumented)
        {
            // ARRANGE
            auto overload1 = std::make_shared<MockFunction>();
            BYTEVECTOR(signature1Bytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return
                0x12, // parameter 1 class
                0x49  // class token (compressed 0x01000012)
            );
            overload1->_signature = std::make_shared<ByteVector>(signature1Bytes);

            auto overload2 = std::make_shared<MockFunction>();
            BYTEVECTOR(signature2Bytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return
                0x0e  // parameter 1 string
            );
            overload1->_signature = std::make_shared<ByteVector>(signature2Bytes);

            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            instrumentationSet->insert(overload1->GetInstrumentationPoint());
            instrumentationSet->insert(overload2->GetInstrumentationPoint());
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationSet, nullptr);
            auto methodRewriter = std::make_shared<MethodRewriter>(instrumentation, _X(""), false);

            uint8_t overload1CallCount = 0;
            overload1->_writeMethodHandler = [&overload1CallCount](const ByteVector&) {
                ++overload1CallCount;
            };

            uint8_t overload2CallCount = 0;
            overload2->_writeMethodHandler = [&overload2CallCount](const ByteVector&) {
                ++overload2CallCount;
            };

            // ACT
            methodRewriter->Instrument(overload1, AgentCallStyle::Strategy::AppDomainFallbackCache);
            methodRewriter->Instrument(overload2, AgentCallStyle::Strategy::AppDomainFallbackCache);

            // ASSERT
            Assert::AreEqual((uint8_t)1, overload1CallCount, L"Function should have been instrumented 1 time!");
            Assert::AreEqual((uint8_t)1, overload2CallCount, L"Function should have been instrumented 1 time!");
        }

        TEST_METHOD(ShouldInstrumentAssembly_mscorlib_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentAssembly(_X("mscorlib")));
        }

        TEST_METHOD(ShouldInstrumentAssembly_system_private_core_lib_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentAssembly(_X("System.Private.CoreLib")));
        }

        TEST_METHOD(ShouldInstrumentAssembly_unknown_assembly_returns_false)
        {
            auto rewriter = MakeRewriter();
            Assert::IsFalse(rewriter->ShouldInstrumentAssembly(_X("SomeRandomAssembly")));
        }

        TEST_METHOD(ShouldInstrumentAssembly_from_instrumentation_point_returns_true)
        {
            auto func = std::make_shared<MockFunction>();
            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            instrumentationSet->insert(func->GetInstrumentationPoint());
            auto rewriter = MakeRewriterWithPoints(instrumentationSet);
            Assert::IsTrue(rewriter->ShouldInstrumentAssembly(_X("MyAssembly")));
        }

        TEST_METHOD(ShouldInstrumentType_cannot_unload_app_domain_exception_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentType(_X("System.CannotUnloadAppDomainException")));
        }

        TEST_METHOD(ShouldInstrumentType_unknown_type_returns_false)
        {
            auto rewriter = MakeRewriter();
            Assert::IsFalse(rewriter->ShouldInstrumentType(_X("SomeRandomNamespace.SomeClass")));
        }

        TEST_METHOD(ShouldInstrumentType_from_instrumentation_point_returns_true)
        {
            auto func = std::make_shared<MockFunction>();
            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            instrumentationSet->insert(func->GetInstrumentationPoint());
            auto rewriter = MakeRewriterWithPoints(instrumentationSet);
            Assert::IsTrue(rewriter->ShouldInstrumentType(_X("MyNamespace.MyClass")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetAppDomainBoolean_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetAppDomainBoolean")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetThreadLocalBoolean_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetThreadLocalBoolean")));
        }

        TEST_METHOD(ShouldInstrumentFunction_SetThreadLocalBoolean_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("SetThreadLocalBoolean")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetMethodFromAppDomainStorageOrReflectionOrThrow_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetMethodFromAppDomainStorageOrReflectionOrThrow")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetMethodFromAppDomainStorage_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetMethodFromAppDomainStorage")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetMethodViaReflectionOrThrow_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetMethodViaReflectionOrThrow")));
        }

        TEST_METHOD(ShouldInstrumentFunction_GetTypeViaReflectionOrThrow_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("GetTypeViaReflectionOrThrow")));
        }

        TEST_METHOD(ShouldInstrumentFunction_LoadAssemblyOrThrow_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("LoadAssemblyOrThrow")));
        }

        TEST_METHOD(ShouldInstrumentFunction_StoreMethodInAppDomainStorageOrThrow_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("StoreMethodInAppDomainStorageOrThrow")));
        }

        TEST_METHOD(ShouldInstrumentFunction_cctor_returns_true)
        {
            auto rewriter = MakeRewriter();
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X(".cctor")));
        }

        TEST_METHOD(ShouldInstrumentFunction_unknown_function_returns_false)
        {
            auto rewriter = MakeRewriter();
            Assert::IsFalse(rewriter->ShouldInstrumentFunction(_X("SomeRandomMethod")));
        }

        TEST_METHOD(ShouldInstrumentFunction_from_instrumentation_point_returns_true)
        {
            auto func = std::make_shared<MockFunction>();
            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            instrumentationSet->insert(func->GetInstrumentationPoint());
            auto rewriter = MakeRewriterWithPoints(instrumentationSet);
            Assert::IsTrue(rewriter->ShouldInstrumentFunction(_X("MyMethod")));
        }

        TEST_METHOD(GetHelperFireCount_initial_returns_zero)
        {
            auto rewriter = MakeRewriter();
            Assert::AreEqual((uint64_t)0, rewriter->GetHelperFireCount());
        }

    private:
        static std::shared_ptr<MethodRewriter> MakeRewriter(bool isCoreClr = false)
        {
            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationSet, nullptr);
            return std::make_shared<MethodRewriter>(instrumentation, _X(""), isCoreClr);
        }

        static std::shared_ptr<MethodRewriter> MakeRewriterWithPoints(
            std::shared_ptr<Configuration::InstrumentationPointSet> points,
            bool isCoreClr = false)
        {
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(points, nullptr);
            return std::make_shared<MethodRewriter>(instrumentation, _X(""), isCoreClr);
        }
    };
}}}}
