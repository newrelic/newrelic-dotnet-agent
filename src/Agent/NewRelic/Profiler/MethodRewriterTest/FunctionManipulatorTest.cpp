// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../Common/Macros.h"
#include "MockFunction.h"
#include "MockSystemCalls.h"
#include "../MethodRewriter/FunctionManipulator.h"
#include "../MethodRewriter/InstrumentFunctionManipulator.h"
#include "../MethodRewriter/ApiFunctionManipulator.h"
#include "../MethodRewriter/HelperFunctionManipulator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(FunctionManipulatorTest)
    {
    public:
        TEST_METHOD(construction)
        {
            auto function = std::make_shared<MockFunction>();
            FunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::InAgentCache);
        }

        TEST_METHOD(instrument_api_method_netframework_inagentcache)
        {
            auto function = std::make_shared<MockFunction>();
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_api_method_netframework_appdomaincache)
        {
            auto function = std::make_shared<MockFunction>();
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::AppDomainCache);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_api_method_netframework_reflection)
        {
            auto function = std::make_shared<MockFunction>();
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::Reflection);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_api_method_coreclr_inagentcache)
        {
            auto function = std::make_shared<MockFunction>();
            function->_isCoreClr = true;
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_api_method_coreclr_appdomaincache)
        {
            auto function = std::make_shared<MockFunction>();
            function->_isCoreClr = true;
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::AppDomainCache);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_api_method_coreclr_reflection)
        {
            auto function = std::make_shared<MockFunction>();
            function->_isCoreClr = true;
            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::Reflection);

            manipulator.InstrumentApi();
        }

        TEST_METHOD(instrument_minimal_method_netframework_inagentcache)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::InAgentCache);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(instrument_minimal_method_netframework_appdomaincache)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::AppDomainCache);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(instrument_minimal_method_netframework_reflection)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), false, AgentCallStyle::Strategy::Reflection);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(instrument_minimal_method_coreclr_inagentcache)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::InAgentCache);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(instrument_minimal_method_coreclr_appdomaincache)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::AppDomainCache);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(instrument_minimal_method_coreclr_reflection)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("")), true, AgentCallStyle::Strategy::Reflection);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        TEST_METHOD(helper_method_unsupported_method)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("ThisMethodShouldNotExist");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_LoadAssemblyOrThrow)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("LoadAssemblyOrThrow");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetTypeViaReflectionOrThrow)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetTypeViaReflectionOrThrow");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetMethodViaReflectionOrThrow)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetMethodViaReflectionOrThrow");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_StoreMethodInAppDomainStorageOrThrow)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("StoreMethodInAppDomainStorageOrThrow");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetMethodFromAppDomainStorage)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetMethodFromAppDomainStorage");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetMethodFromAppDomainStorageOrReflectionOrThrow)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetMethodFromAppDomainStorageOrReflectionOrThrow");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_EnsureInitialized)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("EnsureInitialized");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetMethodCacheLookupMethod)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetMethodCacheLookupMethod");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        TEST_METHOD(helper_method_GetMethodInfoFromAgentCache)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetMethodInfoFromAgentCache");
            HelperFunctionManipulator manipulator(function, true, AgentCallStyle::Strategy::InAgentCache);

            manipulator.InstrumentHelper();
        }

        //TEST_METHOD(test_method_with_no_code)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_no_extra_sections)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_invalid_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_simple_method)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_exceptions)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_multiple_returns)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_local_variables)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_one_extra_section)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_multiple_extra_sections)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_tiny_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_fat_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_fat_header_migration)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(load_argument_and_box_test)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(has_signature_test)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(locals_are_appended)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(local_offsets_are_correct)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(default_instrumentation_is_correct)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(max_local_variables)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(border_local_variables_byte_count)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

    private:
        Configuration::InstrumentationPointPtr CreateInstrumentationPointThatMatchesFunction(IFunctionPtr function)
        {
            Configuration::InstrumentationPointPtr instrumentationPoint(new Configuration::InstrumentationPoint());
            instrumentationPoint->AssemblyName = function->GetAssemblyName();
            instrumentationPoint->ClassName = function->GetTypeName();
            instrumentationPoint->MethodName = function->GetFunctionName();
            return instrumentationPoint;
        }
    };
}}}}
