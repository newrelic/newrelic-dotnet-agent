/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"
#include "../Common/Strings.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic {
    namespace Profiler {
        namespace Common
        {
            TEST_CLASS(StringsTest)
            {
            public:
                TEST_METHOD(strings_utf8tostring)
                {
                    std::string test = "test";
                    std::wstring wtest = ToWideString(test.c_str());
                    for (size_t i = 0; i < test.size(); ++i)
                        Assert::IsTrue(test.at(i) == wtest.at(i));
                }

                TEST_METHOD(strings_EndsWith_case_sensitivity_succeeds)
                {
                    std::wstring processName = L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe";
                    std::wstring token = L"w3wp.exe";

                    Assert::IsTrue(Strings::EndsWith(processName, token));
                }

                TEST_METHOD(strings_EndsWith_case_sensitivity_fails)
                {
                    std::wstring processName = L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe";
                    std::wstring token = L"W3WP.EXE";

                    Assert::IsFalse(Strings::EndsWith(processName, token));
                }

                TEST_METHOD(strings_EndsWith_different_string_fails)
                {
                    std::wstring processName = L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe";
                    std::wstring token = L"w3wp.ede";

                    Assert::IsFalse(Strings::EndsWith(processName, token));
                }

                TEST_METHOD(strings_AreEqualCaseInsensitive)
                {
                    Assert::IsTrue(Strings::AreEqualCaseInsensitive(L"test", L"TeSt"));
                }

                TEST_METHOD(strings_AreEqualCaseInsensitive_false)
                {
                    Assert::IsFalse(Strings::AreEqualCaseInsensitive(L"tester", L"TeSted"));
                }
            };
        }
    }
}
