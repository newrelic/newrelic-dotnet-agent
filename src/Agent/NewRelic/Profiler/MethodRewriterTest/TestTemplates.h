// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <CorHdr.h>
#include <CppUnitTest.h>
#include <string>
#include <sstream>
#include <iomanip>
#include <vector>
#include <stdint.h>

namespace Microsoft { namespace VisualStudio { namespace CppUnitTestFramework
{
    template<> std::wstring ToString<CorElementType>(const CorElementType& t)
    {
        switch (t)
        {
            case ELEMENT_TYPE_END: return L"ELEMENT_TYPE_END";
            case ELEMENT_TYPE_VOID: return L"ELEMENT_TYPE_VOID";
            case ELEMENT_TYPE_BOOLEAN: return L"ELEMENT_TYPE_BOOLEAN";
            case ELEMENT_TYPE_CHAR: return L"ELEMENT_TYPE_CHAR";
            case ELEMENT_TYPE_I1: return L"ELEMENT_TYPE_I1";
            case ELEMENT_TYPE_U1: return L"ELEMENT_TYPE_U1";
            case ELEMENT_TYPE_I2: return L"ELEMENT_TYPE_I2";
            case ELEMENT_TYPE_U2: return L"ELEMENT_TYPE_U2";
            case ELEMENT_TYPE_I4: return L"ELEMENT_TYPE_I4";
            case ELEMENT_TYPE_U4: return L"ELEMENT_TYPE_U4";
            case ELEMENT_TYPE_I8: return L"ELEMENT_TYPE_I8";
            case ELEMENT_TYPE_U8: return L"ELEMENT_TYPE_U8";
            case ELEMENT_TYPE_R4: return L"ELEMENT_TYPE_R4";
            case ELEMENT_TYPE_R8: return L"ELEMENT_TYPE_R8";
            case ELEMENT_TYPE_STRING: return L"ELEMENT_TYPE_STRING";
            case ELEMENT_TYPE_PTR: return L"ELEMENT_TYPE_PTR";
            case ELEMENT_TYPE_BYREF: return L"ELEMENT_TYPE_BYREF";
            case ELEMENT_TYPE_VALUETYPE: return L"ELEMENT_TYPE_VALUETYPE";
            case ELEMENT_TYPE_CLASS: return L"ELEMENT_TYPE_CLASS";
            case ELEMENT_TYPE_VAR: return L"ELEMENT_TYPE_VAR";
            case ELEMENT_TYPE_ARRAY: return L"ELEMENT_TYPE_ARRAY";
            case ELEMENT_TYPE_GENERICINST: return L"ELEMENT_TYPE_GENERICINST";
            case ELEMENT_TYPE_TYPEDBYREF: return L"ELEMENT_TYPE_TYPEDBYREF";
            case ELEMENT_TYPE_I: return L"ELEMENT_TYPE_I";
            case ELEMENT_TYPE_U: return L"ELEMENT_TYPE_U";
            case ELEMENT_TYPE_FNPTR: return L"ELEMENT_TYPE_FNPTR";
            case ELEMENT_TYPE_OBJECT: return L"ELEMENT_TYPE_OBJECT";
            case ELEMENT_TYPE_SZARRAY: return L"ELEMENT_TYPE_SZARRAY";
            case ELEMENT_TYPE_MVAR: return L"ELEMENT_TYPE_MVAR";
            case ELEMENT_TYPE_CMOD_REQD: return L"ELEMENT_TYPE_CMOD_REQD";
            case ELEMENT_TYPE_CMOD_OPT: return L"ELEMENT_TYPE_CMOD_OPT";
            case ELEMENT_TYPE_INTERNAL: return L"ELEMENT_TYPE_INTERNAL";
            case ELEMENT_TYPE_MAX: return L"ELEMENT_TYPE_MAX";
            case ELEMENT_TYPE_MODIFIER: return L"ELEMENT_TYPE_MODIFIER";
            case ELEMENT_TYPE_SENTINEL: return L"ELEMENT_TYPE_SENTINEL";
            case ELEMENT_TYPE_PINNED: return L"ELEMENT_TYPE_PINNED";
            default: return L"Unknown CorElementType.";
        }
    }

    template <> std::wstring ToString<CorCallingConvention>(const CorCallingConvention& t)
    {
        switch (t)
        {
            case IMAGE_CEE_CS_CALLCONV_DEFAULT: return L"IMAGE_CEE_CS_CALLCONV_DEFAULT";
            case IMAGE_CEE_CS_CALLCONV_VARARG: return L"IMAGE_CEE_CS_CALLCONV_VARARG";
            case IMAGE_CEE_CS_CALLCONV_FIELD: return L"IMAGE_CEE_CS_CALLCONV_FIELD";
            case IMAGE_CEE_CS_CALLCONV_LOCAL_SIG: return L"IMAGE_CEE_CS_CALLCONV_LOCAL_SIG";
            case IMAGE_CEE_CS_CALLCONV_PROPERTY: return L"IMAGE_CEE_CS_CALLCONV_PROPERTY";
            case IMAGE_CEE_CS_CALLCONV_UNMGD: return L"IMAGE_CEE_CS_CALLCONV_UNMGD";
            case IMAGE_CEE_CS_CALLCONV_GENERICINST: return L"IMAGE_CEE_CS_CALLCONV_GENERICINST";
            case IMAGE_CEE_CS_CALLCONV_NATIVEVARARG: return L"IMAGE_CEE_CS_CALLCONV_NATIVEVARARG";
            case IMAGE_CEE_CS_CALLCONV_MAX: return L"IMAGE_CEE_CS_CALLCONV_MAX";
            case IMAGE_CEE_CS_CALLCONV_MASK: return L"IMAGE_CEE_CS_CALLCONV_MASK";
            case IMAGE_CEE_CS_CALLCONV_HASTHIS: return L"IMAGE_CEE_CS_CALLCONV_HASTHIS";
            case IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS: return L"IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS";
            case IMAGE_CEE_CS_CALLCONV_GENERIC: return L"IMAGE_CEE_CS_CALLCONV_GENERIC";
            default: return L"Unknown CorCallingConvention.";
        }
    }

    //template<> std::wstring ToString<uint16_t> (const uint16_t& t) { RETURN_WIDE_STRING(t); }

    template <> std::wstring ToString<std::vector<uint8_t>>(const std::vector<uint8_t>& t)
    {
        std::wstringstream stream;
        for (uint8_t byte : t)
        {
            stream << std::hex << std::internal << std::setw(2) << std::setfill(L'0') << byte << L' ';
        }
        return stream.str();
    }

}}}

static inline void UseUnreferencedTestTemplates()
{
    std::wstring (*corElementTypeFunc)(const CorElementType&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)corElementTypeFunc;

    std::wstring (*CorCallingConventionFunc)(const CorCallingConvention&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)CorCallingConventionFunc;

    std::wstring (*uint16_tFunc)(const uint16_t&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)uint16_tFunc;

    std::wstring (*byteVectorFunc)(const std::vector<uint8_t>&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)byteVectorFunc;

}
