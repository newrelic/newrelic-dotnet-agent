/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"
#include "../Configuration/Strings.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration 
{
    TEST_CLASS(StringsTest)
    {
    public:

        TEST_METHOD(strings_compare)
        {
            std::wstring string1 = L"Foo";
            std::wstring string2 = L"Foo";

            Assert::IsTrue(Strings::AreEqualCaseInsensitive(string1, string2));
        }

        TEST_METHOD(string_case_insensitive_compare)
        {
            std::wstring string1 = L"Foo";
            std::wstring string2 = L"foO";

            Assert::IsTrue(Strings::AreEqualCaseInsensitive(string1, string2));
        }

        TEST_METHOD(strings_EndsWith_succeeds)
        {
            std::wstring processName = L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe";
            std::wstring token = L"W3WP.EXE";

            Assert::IsTrue(Strings::EndsWith(processName, token));
        }

        TEST_METHOD(strings_EndsWith_fails)
        {
            std::wstring processName = L"C:\\Windows\\SysWOW64\\inetsrv\\w3wp.exe";
            std::wstring token = L"W3WP.ESE";

            Assert::IsFalse(Strings::EndsWith(processName, token));
        }

    };
}}}
