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

namespace NewRelic { namespace Profiler { namespace MethodRewriter {
    namespace Test {
        TEST_CLASS(MethodRewriterTest){
            public :
                /*
        TEST_METHOD(request_function_name_callback)
        {
            // setup a default method rewriter and function to instrument
            auto function = std::make_shared<MockFunction>();
            auto system = std::make_shared<MockSystemCalls>();
            auto configuration = std::make_shared<Configuration::Configuration>();
            auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
            instrumentationSet->insert(function->GetInstrumentationPoint());
            auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationSet);
            auto methodRewriter = std::make_shared<MethodRewriter>(configuration, instrumentation);

            // instrument a function
            methodRewriter->Instrument(function, system);

            ValidateDefaultMockFunctionCallback();
        }*/

        TEST_METHOD(overloads_are_instrumented){
                    // ARRANGE
                    auto overload1 = std::make_shared<MockFunction>();
        BYTEVECTOR(signature1Bytes,
            0x00, // default calling convention
            0x01, // 1 parameter
            0x01, // void return
            0x12, // parameter 1 class
            0x49 // class token (compressed 0x01000012)
        );
        overload1->_signature = std::make_shared<ByteVector>(signature1Bytes);

        auto overload2 = std::make_shared<MockFunction>();
        BYTEVECTOR(signature2Bytes,
            0x00, // default calling convention
            0x01, // 1 parameter
            0x01, // void return
            0x0e // parameter 1 string
        );
        overload1->_signature = std::make_shared<ByteVector>(signature2Bytes);

        auto instrumentationSet = std::make_shared<Configuration::InstrumentationPointSet>();
        instrumentationSet->insert(overload1->GetInstrumentationPoint());
        instrumentationSet->insert(overload2->GetInstrumentationPoint());
        auto instrumentation = std::make_shared<Configuration::InstrumentationConfiguration>(instrumentationSet);
        auto methodRewriter = std::make_shared<MethodRewriter>(instrumentation, _X(""));

        uint8_t overload1CallCount = 0;
        overload1->_writeMethodHandler = [&overload1CallCount](const ByteVector&) {
            ++overload1CallCount;
        };

        uint8_t overload2CallCount = 0;
        overload2->_writeMethodHandler = [&overload2CallCount](const ByteVector&) {
            ++overload2CallCount;
        };

        // ACT
        methodRewriter->Instrument(overload1);
        methodRewriter->Instrument(overload2);

        // ASSERT
        Assert::AreEqual((uint8_t)1, overload1CallCount, L"Function should have been instrumented 1 time!");
        Assert::AreEqual((uint8_t)1, overload2CallCount, L"Function should have been instrumented 1 time!");
    }

private:
    /*
        static void ValidateDefaultMockFunctionCallback()
        {
            auto callbackFunction = [](uintptr_t actualFunctionId, const wchar_t* actualClassName, const wchar_t* actualMethodName)
            {
                Assert::AreEqual(uintptr_t(0x12345678), actualFunctionId);
                Assert::AreEqual(L"MyNamespace.MyClass", actualClassName);
                Assert::AreEqual(L"MyMethod", actualMethodName);
                // throw an exception so we can know it was actually called, there is no other easy way to get information out of this method.
                throw std::exception();
            };

            try
            {
                // request the name of the function from its function id
                ::NewRelic::Profiler::MethodRewriter::RequestFunctionNames(0x12345678, 1, callbackFunction);
                // detect exception as a way to identify that the method was called
                Assert::Fail(L"Request callback was never called.");
            }
            catch (...) { }
        }
        */
};
}}
}
}
