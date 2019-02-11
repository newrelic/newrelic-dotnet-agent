#pragma once
#include <string>
#include "../Logging/Logger.h"

using namespace NewRelic::Profiler::Logger;

namespace Microsoft { namespace VisualStudio { namespace CppUnitTestFramework
{
	template<> std::wstring ToString<Level>(const Level& t)
	{
		switch (t)
		{
			case Level::LEVEL_DEBUG: return L"DEBUG";
			case Level::LEVEL_ERROR: return L"ERROR";
			case Level::LEVEL_TRACE: return L"TRACE";
			case Level::LEVEL_INFO: return L"INFO";
			case Level::LEVEL_WARN: return L"WARN";
			default: return L"Unknown Level.";
		}
	}
}}}

static inline void UseUnreferencedLoggingTestTemplates()
{
	std::wstring (*LevelFunc)(const Level&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
	(void)LevelFunc;
}
