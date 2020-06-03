#pragma once
#include <string>
#include <iomanip>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "../SignatureParser/Types.h"
#include "../Common/Macros.h"

using namespace NewRelic::Profiler::SignatureParser;

namespace Microsoft { namespace VisualStudio { namespace CppUnitTestFramework
{
    template<> static std::wstring ToString<CorCallingConvention>(const CorCallingConvention& t)
    {
        switch (t)
        {
        case CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT: return L"DEFAULT";
        case CorCallingConvention::IMAGE_CEE_CS_CALLCONV_VARARG: return L"VARARG";
        case CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC: return L"GENERIC";
        case CorUnmanagedCallingConvention::IMAGE_CEE_CS_CALLCONV_C: return L"C";
        case CorUnmanagedCallingConvention::IMAGE_CEE_CS_CALLCONV_STDCALL: return L"STDCALL";
        case CorUnmanagedCallingConvention::IMAGE_CEE_CS_CALLCONV_THISCALL: return L"THISCALL";
        case CorUnmanagedCallingConvention::IMAGE_CEE_CS_CALLCONV_FASTCALL: return L"FASTCALL";
            default: return L"Unknown CallingConvention.";
        }
    }

    template <> static std::wstring ToString<NewRelic::Profiler::ByteVector>(const NewRelic::Profiler::ByteVector& t)
    {
        std::wstringstream stream;
        for (uint8_t byte : t)
        {
            stream << std::hex << std::internal << std::setw(2) << std::setfill(L'0') << byte << L' ';
        }
        return stream.str();
    }
}}}

namespace NewRelic { namespace Profiler { namespace SignatureParser { namespace Test
{
    static inline void UseUnreferencedTestTemplates()
    {
        std::wstring (*callingConventionFunc)(const CorCallingConvention&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
        (void)callingConventionFunc;

        std::wstring (*byteVectorFunc)(const ByteVector&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
        (void)byteVectorFunc;
    }
}}}}
