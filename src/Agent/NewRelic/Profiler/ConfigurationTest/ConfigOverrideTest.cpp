// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <memory>
#include <string>
#include <set>
#include "../Common/xplat.h"
#include "../Common/Strings.h"
#include "../Logging/DefaultFileLogLocation.h"
#include "MockSystemCalls.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    TEST_CLASS(ConfigurationOverrideTest)
    {
    public:
        TEST_METHOD(test_logging_enabled)
        {
            // Environment variable takes priority
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_ENABLED"), _X("1"));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_ENABLED"), _X("true"));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_ENABLED"), _X("0"));
            Assert::IsFalse(_systemCalls.GetLoggingEnabled(false));
            Assert::IsFalse(_systemCalls.GetLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_ENABLED"), _X("false"));
            Assert::IsFalse(_systemCalls.GetLoggingEnabled(false));
            Assert::IsFalse(_systemCalls.GetLoggingEnabled(true));

            // Use fallback (the config file value in actual usage)
            _systemCalls.ResetEnvironmentVariables();

            Assert::IsFalse(_systemCalls.GetLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_ENABLED"), _X("*invalid*"));
            Assert::IsFalse(_systemCalls.GetLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetLoggingEnabled(true));
        }
        TEST_METHOD(test_console_logging_enabled)
        {
            // Environment variable takes priority
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_CONSOLE"), _X("1"));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_CONSOLE"), _X("true"));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_CONSOLE"), _X("0"));
            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_CONSOLE"), _X("false"));
            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(true));

            // Use fallback (the config file value in actual usage)
            _systemCalls.ResetEnvironmentVariables();

            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(true));

            _systemCalls.SetEnvironmentVariableW(_X("NEW_RELIC_LOG_CONSOLE"), _X("*invalid*"));
            Assert::IsFalse(_systemCalls.GetConsoleLoggingEnabled(false));
            Assert::IsTrue(_systemCalls.GetConsoleLoggingEnabled(true));
        }

    private:
        MockSystemCalls _systemCalls;
    };
}}}}
