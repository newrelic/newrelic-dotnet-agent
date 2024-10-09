// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#define WIN32_LEAN_AND_MEAN
#include <unordered_map>
#include <Windows.h>
#include "CppUnitTest.h"
#include "MockSystemCalls.h"
#include "../MethodRewriter/CustomInstrumentation.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(SystemCallsTests)
    {
    public:
        TEST_METHOD(GetForceProfiling_UsesNewEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_FORCE_PROFILING"] = L"1";

            Assert::IsTrue(_systemCalls.GetForceProfiling());
        }

        TEST_METHOD(GetForceProfiling_UsesOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEWRELIC_FORCE_PROFILING"] = L"1";

            Assert::IsTrue(_systemCalls.GetForceProfiling());
        }

        TEST_METHOD(GetForceProfiling_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_FORCE_PROFILING"] = L"1";
            _systemCalls.environmentVariables[L"NEWRELIC_FORCE_PROFILING"] = L"0";

            Assert::IsTrue(_systemCalls.GetForceProfiling());
        }


        TEST_METHOD(GetNewRelicHomePath_UsesNewEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\NewRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicHomePath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicHomePath_UsesOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEWRELIC_HOME"] = L"C:\\NewRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicHomePath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicHomePath_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\NewRelic";
            _systemCalls.environmentVariables[L"NEWRELIC_HOME"] = L"C:\\OldRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicHomePath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicInstallPath_UsesNewEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_INSTALL_PATH"] = L"C:\\NewRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicInstallPath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicInstallPath_UsesOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEWRELIC_INSTALL_PATH"] = L"C:\\NewRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicInstallPath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicInstallPath_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_INSTALL_PATH"] = L"C:\\NewRelic";
            _systemCalls.environmentVariables[L"NEWRELIC_INSTALL_PATH"] = L"C:\\OldRelic";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetNewRelicInstallPath();
            Assert::AreEqual(L"C:\\NewRelic", basicString->c_str());
        }

        TEST_METHOD(GetProfilerDelay_UsesNewEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_PROFILER_DELAY_IN_SEC"] = L"10";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetProfilerDelay();
            Assert::AreEqual(L"10", basicString->c_str());
        }

        TEST_METHOD(GetProfilerDelay_UsesOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEWRELIC_PROFILER_DELAY_IN_SEC"] = L"10";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetProfilerDelay();
            Assert::AreEqual(L"10", basicString->c_str());
        }

        TEST_METHOD(GetProfilerDelay_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.ResetEnvironmentVariables();
            _systemCalls.environmentVariables[L"NEW_RELIC_PROFILER_DELAY_IN_SEC"] = L"10";
            _systemCalls.environmentVariables[L"NEWRELIC_PROFILER_DELAY_IN_SEC"] = L"20";

            std::unique_ptr<xstring_t> basicString = _systemCalls.GetProfilerDelay();
            Assert::AreEqual(L"10", basicString->c_str());
        }

        TEST_METHOD(GetNewRelicProfilerLogDirectory_UsesNewEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_PROFILER_LOG_DIRECTORY")] = _X("C:\\NewRelic\\Logs");

            auto logDir = _systemCalls.GetNewRelicProfilerLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\Logs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicProfilerLogDirectory_UsesOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEWRELIC_PROFILER_LOG_DIRECTORY")] = _X("C:\\NewRelic\\OldLogs");

            auto logDir = _systemCalls.GetNewRelicProfilerLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\OldLogs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicProfilerLogDirectory_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_PROFILER_LOG_DIRECTORY")] = _X("C:\\NewRelic\\Logs");
            _systemCalls.environmentVariables[_X("NEWRELIC_PROFILER_LOG_DIRECTORY")] = _X("C:\\NewRelic\\OldLogs");

            auto logDir = _systemCalls.GetNewRelicProfilerLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\Logs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicLogDirectory_UsesNewEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_LOG_DIRECTORY")] = _X("C:\\NewRelic\\Logs");

            auto logDir = _systemCalls.GetNewRelicLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\Logs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicLogDirectory_UsesOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEWRELIC_LOG_DIRECTORY")] = _X("C:\\NewRelic\\OldLogs");

            auto logDir = _systemCalls.GetNewRelicLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\OldLogs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicLogDirectory_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_LOG_DIRECTORY")] = _X("C:\\NewRelic\\Logs");
            _systemCalls.environmentVariables[_X("NEWRELIC_LOG_DIRECTORY")] = _X("C:\\NewRelic\\OldLogs");

            auto logDir = _systemCalls.GetNewRelicLogDirectory();
            Assert::IsNotNull(logDir.get());
            Assert::AreEqual(L"C:\\NewRelic\\Logs", logDir->c_str());
        }

        TEST_METHOD(GetNewRelicLogLevel_UsesNewEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_LOG_LEVEL")] = _X("info");

            auto logLevel = _systemCalls.GetNewRelicLogLevel();
            Assert::IsNotNull(logLevel.get());
            Assert::AreEqual(L"info", logLevel->c_str());
        }

        TEST_METHOD(GetNewRelicLogLevel_UsesOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEWRELIC_LOG_LEVEL")] = _X("debug");

            auto logLevel = _systemCalls.GetNewRelicLogLevel();
            Assert::IsNotNull(logLevel.get());
            Assert::AreEqual(L"debug", logLevel->c_str());
        }

        TEST_METHOD(GetNewRelicLogLevel_UsesNewOverOldEnvironmentVariable)
        {
            _systemCalls.environmentVariables[_X("NEW_RELIC_LOG_LEVEL")] = _X("info");
            _systemCalls.environmentVariables[_X("NEWRELIC_LOG_LEVEL")] = _X("debug");

            auto logLevel = _systemCalls.GetNewRelicLogLevel();
            Assert::IsNotNull(logLevel.get());
            Assert::AreEqual(L"info", logLevel->c_str());
        }

    private:
        MockSystemCalls _systemCalls;
    };

}}}}
