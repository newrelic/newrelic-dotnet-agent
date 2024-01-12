// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <random>
#include <algorithm>
#include <sstream>
#include <iomanip>

#include "CppUnitTest.h"

#include "../ast/Types.h"
#include "../codegen/ITokenizer.h"

namespace Microsoft
{
    namespace VisualStudio
    {
        namespace CppUnitTestFramework
        {
            using namespace sicily::ast;
            using namespace sicily::codegen;

            //template<> std::wstring ToString<uint16_t>(const uint16_t& t)
            //{
            //    RETURN_WIDE_STRING(t);
            //}

            template<> std::wstring ToString<Type::Kind>(const Type::Kind& t)
            {
                switch (t)
                {
                    case Type::Kind::kARRAY: return L"Array";
                    case Type::Kind::kCLASS: return L"Class";
                    case Type::Kind::kGENERICCLASS: return L"Generic Class";
                    case Type::Kind::kMETHOD: return L"Method";
                    case Type::Kind::kPRIMITIVE: return L"Primitive";
                    default: return L"Unknown Type Kind.";
                }
            }

            template <> std::wstring ToString<PrimitiveType::PrimitiveKind>(const PrimitiveType::PrimitiveKind& t)
            {
                switch (t)
                {
                    case PrimitiveType::PrimitiveKind::kBOOL: return L"bool";
                    case PrimitiveType::PrimitiveKind::kCHAR: return L"char";
                    case PrimitiveType::PrimitiveKind::kI1: return L"SInt8";
                    case PrimitiveType::PrimitiveKind::kI2: return L"SInt16";
                    case PrimitiveType::PrimitiveKind::kI4: return L"SInt32";
                    case PrimitiveType::PrimitiveKind::kI8: return L"SInt64";
                    case PrimitiveType::PrimitiveKind::kINTPTR: return L"IntPtr";
                    case PrimitiveType::PrimitiveKind::kOBJECT: return L"object";
                    case PrimitiveType::PrimitiveKind::kR4: return L"float";
                    case PrimitiveType::PrimitiveKind::kR8: return L"double";
                    case PrimitiveType::PrimitiveKind::kSTRING: return L"string";
                    case PrimitiveType::PrimitiveKind::kU1: return L"UInt8";
                    case PrimitiveType::PrimitiveKind::kU2: return L"UInt16";
                    case PrimitiveType::PrimitiveKind::kU4: return L"UInt32";
                    case PrimitiveType::PrimitiveKind::kU8: return L"UInt64";
                    case PrimitiveType::PrimitiveKind::kUINTPTR: return L"UIntPtr";
                    case PrimitiveType::PrimitiveKind::kVOID: return L"void";
                    default: return L"Unknown PrimitiveType Kind.";
                }
            }

            template <> std::wstring ToString<GenericParamType::GenericParamKind>(const GenericParamType::GenericParamKind& t)
            {
                switch (t)
                {
                    case GenericParamType::GenericParamKind::kTYPE: return L"!";
                    case GenericParamType::GenericParamKind::kMETHOD: return L"!!";
                    default: return L"Unknown GenericParamType Kind.";
                }
            }

            template <> std::wstring ToString<ByteVector>(const ByteVector& t)
            {
                std::wstringstream stream;
                for (uint8_t byte : t)
                {
                    stream << std::hex << std::internal << std::setw(2) << std::setfill(L'0') << byte << L' ';
                }
                return stream.str();
            }

            static inline void UseUnreferenced()
            {
                std::wstring (*shortFunc)(const uint16_t&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
                (void)shortFunc;

                std::wstring (*kindFunc)(const Type::Kind&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
                (void)kindFunc;

                std::wstring (*primitiveKindFunc)(const PrimitiveType::PrimitiveKind&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
                (void)primitiveKindFunc;

                std::wstring (*genericKindFunc)(const GenericParamType::GenericParamKind&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
                (void)genericKindFunc;

                std::wstring (*byteVectorFunc)(const ByteVector&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
                (void)byteVectorFunc;
            }
        }
    }
}
