#include "stdafx.h"
#include "../Configuration/Configuration.h"
#include "CppUnitTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test {
	TEST_CLASS(ShouldInstrumentTest) {
	public :
		TEST_METHOD(netCore_dotnet_exe_invocations_not_instrumented) {
			auto processPath = _X("processPath");
			auto appPoolId = _X("appPoolId");
			Configuration configuration(true);

			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("DotNet Run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet.exe run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("\"dotnet.exe\" run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("\"c:\\program files\\dotnet.exe\" run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("\"c:\\program files\\dotnet.exe\"   run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet publish")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet restore")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet run -p c:\\test\\test.csproj")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet run -p \"c:\\program files\\test.csproj\"")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet run -p ~/test.csproj")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet.exe exec \"c:\\program files\\MSBuild.dll\" -maxcpucount")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet.exe exec c:\\test\\msbuild.dll")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet new console")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet new mvc")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet\" run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("\"dotnet\" run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("app1.exe | dotnet run")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet Kudu.Services.Web.dll")));


			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnetXexe restore")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("\"c:\\program files\\dotnet.exe\"run")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet exec test.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet exec publish.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet publish.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet run.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet restore.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet.exerun publish.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("IpublishedThis.exe")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet new.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet exec new.dll")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet myapp.dll run thisapp")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet/run/IpublishedThis.exe")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("run dotnet")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("IpublishedThis.exe \"dotnet run\"")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("IpublishedThis.exe 'dotnet run'")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet exec IpublishedThis.dll dotnet run")));
			Assert::IsTrue(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("dotnet exec IpublishedThis.dll \"dotnet run \"")));

			//These will incorrectly not be instrumented, but they are edge cases.  We are documenting them here.
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("IpublishedThis.exe \"dotnet run \"")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("'IpublishedThis.exe' \"dotnet run \"")));
			Assert::IsFalse(configuration.ShouldInstrumentNetCore(processPath, appPoolId, _X("IpublishedThis.exe dotnet run")));
		}
	};
}}}}
