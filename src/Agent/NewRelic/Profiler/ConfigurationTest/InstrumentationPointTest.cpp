/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"
#include "../Configuration/Strings.h"
#include "../Configuration/InstrumentationPoint.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    TEST_CLASS(InstrumentationPointTest)
    {
    public:
        
        TEST_METHOD(verify_equality_operator_handles_empty_strings)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->AssemblyName = L"";
            p1->ClassName = L"";
            p1->MethodName = L"";
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->AssemblyName = L"";
            p2->ClassName = L"";
            p2->MethodName = L"";
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(verify_equality_operator_success_path)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->AssemblyName = L"System.Web";
            p1->ClassName = L"System.Web.HttpApplication";
            p1->MethodName = L"InitModulesCommon";
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->AssemblyName = L"System.Web";
            p2->ClassName = L"System.Web.HttpApplication";
            p2->MethodName = L"InitModulesCommon";
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));
            
            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(verify_equality_operator_handles_unassigned_string_on_one_side)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->AssemblyName = L"System.Web";
            p2->ClassName = L"System.Web.HttpApplication";
            p2->MethodName = L"InitModulesCommon";
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));
            
            Assert::IsFalse(*p1 == *p2);
        }

        TEST_METHOD(verify_equality_operator_handles_unassigned_string_on_both_sides)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            
            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(verify_equality_operator_compares_assembly_name_only_everything_else_unset)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->AssemblyName = L"Microsoft.Practices.EnterpriseLibrary.Data";
            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->AssemblyName = L"Microsoft.Practices.EnterpriseLibrary.Data";
            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_empty_parameters_then_right_side_matches_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_empty_parameters_then_right_side_does_not_match_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsFalse(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_null_parameters_then_right_side_matches_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = nullptr;

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_null_parameters_then_right_side_matches_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = nullptr;

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_void_parameters_then_right_side_matches_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_left_side_of_equality_operator_has_void_parameters_then_right_side_does_not_match_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsFalse(*p1 == *p2);
        }

        TEST_METHOD(when_right_side_of_equality_operator_has_empty_parameters_then_left_side_does_not_match_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsFalse(*p1 == *p2);
        }

        TEST_METHOD(when_right_side_of_equality_operator_has_null_parameters_then_left_side_matches_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = nullptr;

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_right_side_of_equality_operator_has_null_parameters_then_left_side_matches_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = nullptr;

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_right_side_of_equality_operator_has_void_parameters_then_left_side_matches_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L""));

            Assert::IsTrue(*p1 == *p2);
        }

        TEST_METHOD(when_right_side_of_equality_operator_has_void_parameters_then_left_side_does_not_match_non_empty_parameters)
        {
            InstrumentationPointPtr p1 = std::make_shared<InstrumentationPoint>();
            p1->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"void"));

            InstrumentationPointPtr p2 = std::make_shared<InstrumentationPoint>();
            p2->Parameters = std::unique_ptr<std::wstring>(new std::wstring(L"System.String"));

            Assert::IsFalse(*p1 == *p2);
        }

        //TEST_METHOD(verify_equality_operator_compares_assembly_and_class_name_only_everything_else_unset)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(verify_equality_operator_compares_assembly_class_and_method_name_only_everything_else_unset)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

    };
}}}}