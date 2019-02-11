#pragma once
#include <functional>
#include <string>
#include <memory>
#include "../MethodRewriter/ISystemCalls.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test {
	struct MockSystemCalls : ISystemCalls
	{
		std::function<std::unique_ptr<std::wstring>(const std::wstring&)> EnvironmentVariableResult;

		MockSystemCalls()
		{
			EnvironmentVariableResult = [](const std::wstring&)
			{
				return std::unique_ptr<std::wstring>(new std::wstring(L"C:\\foo\\bar"));
			};
		}

		virtual std::unique_ptr<std::wstring> TryGetEnvironmentVariable(const std::wstring& variableName)
		{
			return EnvironmentVariableResult(variableName);
		}

		virtual std::wstring GetNewRelicHomePath() override
		{
			return L"NEWRELIC_HOME_DIRECTORY";
		}

		virtual std::wstring GetNewRelicInstallPath() override
		{
			return L"NEWRELIC_INSTALL_PATH";
		}

		virtual bool FileExists(const xstring_t&) override
		{
			return true;
		}
	};
}}}}
