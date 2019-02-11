#pragma once

#include <stdint.h>
#include "Exceptions.h"
#include "../Logging/Logger.h"

// turns a failed HRESULT into a log line and a Win32Exception
#define ThrowOnError(function, ...) \
{ \
	static_assert(std::is_same<HRESULT,decltype(function(__VA_ARGS__))>::value, "Not an HRESULT"); \
	HRESULT result = function(__VA_ARGS__); \
	if (result == CORPROF_E_UNSUPPORTED_CALL_SEQUENCE) { \
		LogError("Win32 function call failed.  Function: " #function "  HRESULT: CORPROF_E_UNSUPPORTED_CALL_SEQUENCE"); \
		throw NewRelic::Profiler::Win32Exception(result); \
	} \
	else if (FAILED(result)) \
	{ \
		LogError("Win32 function call failed.  Function: " #function "  HRESULT: ", \
				std::hex, std::showbase, result, std::resetiosflags(std::ios_base::showbase|std::ios_base::basefield)); \
		throw NewRelic::Profiler::Win32Exception(result); \
	} \
}

// turns a failed HANDLE into a log line and a Win32Exception
#define ThrowOnNullHandle(function, ...) \
{ \
	void* result = function(__VA_ARGS__); \
	if (result == nullptr) \
	{ \
		LogError("Win32 function call failed.  Function: " #function); \
		throw NewRelic::Profiler::Win32NullHandleException(); \
	} \
}

inline xstring_t ToStdWString(LPWSTR pString)
{
	return pString;
}

inline std::unique_ptr<xstring_t> ToStdWStringUniquePtr(LPWSTR pString)
{
	return std::unique_ptr<xstring_t>(new xstring_t(pString));
}

inline LPCWSTR ToWindowsString(const xstring_t& str)
{
	return str.c_str();
}