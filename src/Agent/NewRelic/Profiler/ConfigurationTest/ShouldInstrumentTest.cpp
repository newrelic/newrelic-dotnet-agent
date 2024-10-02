// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "stdafx.h"
#include "../Logging/Logger.h"
#include "../Configuration/Configuration.h"
#include "../LoggingTest/DefaultFileLogLocationTest.cpp"
#include "CppUnitTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test {
    TEST_CLASS(ShouldInstrumentTest) {
    public :
        TEST_METHOD(netCore_dotnet_exe_invocations_not_instrumented) {
            auto processPath = _X("processPath");
            auto appPoolId = _X("appPoolId");
            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();

            Configuration configuration(true, Logger::Level::LEVEL_INFO, ProcessesPtr(new Processes()),ApplicationPoolsPtr(new ApplicationPools()),ApplicationPoolsPtr(new ApplicationPools()), true, false, false, systemCalls);

            auto isCoreClr = true;

            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("DotNet Run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet.exe run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("\"dotnet.exe\" run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("\"c:\\program files\\dotnet.exe\" run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("\"c:\\program files\\dotnet.exe\"   run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet publish"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet restore"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet run -p c:\\test\\test.csproj"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet run -p \"c:\\program files\\test.csproj\""), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet run -p ~/test.csproj"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet.exe exec \"c:\\program files\\MSBuild.dll\" -maxcpucount"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet.exe exec c:\\test\\msbuild.dll"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet new console"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet new mvc"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet\" run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("\"dotnet\" run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("app1.exe | dotnet run"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet Kudu.Services.Web.dll"), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("/opt/Kudu/Kudu.Services.Web"), isCoreClr));

            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnetXexe restore"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("\"c:\\program files\\dotnet.exe\"run"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet exec test.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet exec publish.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet publish.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet run.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet restore.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet.exerun publish.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("IpublishedThis.exe"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet new.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet exec new.dll"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet myapp.dll run thisapp"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet/run/IpublishedThis.exe"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("run dotnet"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("IpublishedThis.exe \"dotnet run\""), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("IpublishedThis.exe 'dotnet run'"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet exec IpublishedThis.dll dotnet run"), isCoreClr));
            Assert::IsTrue(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("dotnet exec IpublishedThis.dll \"dotnet run \""), isCoreClr));

            //These will incorrectly not be instrumented, but they are edge cases.  We are documenting them here.
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("IpublishedThis.exe \"dotnet run \""), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("'IpublishedThis.exe' \"dotnet run \""), isCoreClr));
            Assert::IsFalse(configuration.ShouldInstrument(processPath, L"", appPoolId, _X("IpublishedThis.exe dotnet run"), isCoreClr));
        }
    };
}}}}
