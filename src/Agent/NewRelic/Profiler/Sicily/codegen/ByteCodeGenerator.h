/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include "../Exceptions.h"
#include "ITokenizer.h"
#include "../ast/Types.h"

namespace sicily { namespace codegen
{
    struct UnhandledTypeKindException : BytecodeGeneratorException
    {
        UnhandledTypeKindException(ast::Type::Kind kind) : kind_(kind) {}
        ast::Type::Kind kind_;
    };

    struct UnknownClassKindException : BytecodeGeneratorException
    {
        UnknownClassKindException(ast::ClassType::ClassKind kind) : kind_(kind) {}
        ast::ClassType::ClassKind kind_;
    };

    struct DataTooLargeToCompressException : BytecodeGeneratorException
    {};

    struct UnableToDecompressDataException : BytecodeGeneratorException
    {
        UnableToDecompressDataException(const std::vector<uint8_t>::const_iterator& bytes) : bytes_(bytes) {}
        std::vector<uint8_t>::const_iterator bytes_;
    };

    class ByteCodeGenerator
    {
    public:
        ByteCodeGenerator(std::shared_ptr<ITokenizer> tokenizer) : tokenizer(tokenizer) {}
        virtual ~ByteCodeGenerator() {}

        uint32_t TypeToToken(ast::TypePtr type)
        {
            switch (type->GetKind())
            {
                case ast::Type::Kind::kMETHOD:
                {
                    return TypeToTokenSpecific(std::dynamic_pointer_cast<ast::MethodType>(type));
                }
                case ast::Type::Kind::kFIELD:
                {
                    return TypeToTokenSpecific(std::dynamic_pointer_cast<ast::FieldType>(type));
                }
                case ast::Type::Kind::kCLASS:
                {
                    return TypeToTokenSpecific(std::dynamic_pointer_cast<ast::ClassType, ast::Type>(type));
                }
                case ast::Type::Kind::kGENERICCLASS:
                {
                    return TypeToTokenSpecific(std::dynamic_pointer_cast<ast::GenericType, ast::Type>(type));
                }
                case ast::Type::Kind::kARRAY:
                {
                    return TypeToTokenSpecific(std::dynamic_pointer_cast<ast::ArrayType, ast::Type>(type));
                }
                default:
                {
                    throw UnhandledTypeKindException(type->GetKind());
                }
            }
        }

        ByteVector TypeToBytes(ast::TypePtr type)
        {
            switch (type->GetKind())
            {
                case ast::Type::Kind::kPRIMITIVE:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::PrimitiveType, ast::Type>(type));
                }
                case ast::Type::Kind::kARRAY:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::ArrayType>(type));
                }
                case ast::Type::Kind::kMETHOD:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::MethodType>(type));
                }
                case ast::Type::Kind::kFIELD:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::FieldType>(type));
                }
                case ast::Type::Kind::kCLASS:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::ClassType, ast::Type>(type));
                }
                case ast::Type::Kind::kGENERICCLASS:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::GenericType, ast::Type>(type));
                }
                case ast::Type::Kind::kGENERICPARAM:
                {
                    return TypeToBytesSpecific(std::dynamic_pointer_cast<ast::GenericParamType, ast::Type>(type));
                }
                default:
                {
                    throw ast::UnknownTypeKindException(type->GetKind());
                }
            }
        }

        ByteVector GenericMethodInstantiationToSignature(ast::MethodTypePtr type)
        {
            ByteVector bytes;

            // GENRICINST (misspelling intentional, see ECMA-335 II.23.2.15, different from GENERICINST)
            bytes.push_back(0x0a);
            // GenArgCount
            auto genericArgumentCount = type->GetGenericTypes()->GetSize();
            auto compressedGenericArgumentCount = CorSigCompressData(genericArgumentCount);
            bytes.insert(bytes.end(), compressedGenericArgumentCount.begin(), compressedGenericArgumentCount.end());
            // Type+
            for (uint16_t i = 0; i < genericArgumentCount; ++i)
            {
                auto genericArgumentType = type->GetGenericTypes()->GetItem(i);
                auto genericArgumentTypeBytes = TypeToBytes(genericArgumentType);
                bytes.insert(bytes.end(), genericArgumentTypeBytes.begin(), genericArgumentTypeBytes.end());
            }

            return bytes;
        }

        ByteVector GenericClassInstantiationToSignature(ast::GenericTypePtr type)
        {
            ByteVector bytes;
            // GENERICINST
            bytes.push_back(0x15);
            // (CLASS | VALUETYPE) TypeDefOrRefOrSpecEncoded
            auto uninstantiatedTypeBytes = TypeToBytesSpecific(std::static_pointer_cast<ast::ClassType>(type));
            bytes.insert(bytes.end(), uninstantiatedTypeBytes.begin(), uninstantiatedTypeBytes.end());
            // GenArgCount
            auto compressedGenericArgumentCount = CorSigCompressData(type->GetGenericTypes()->GetSize());
            bytes.insert(bytes.end(), compressedGenericArgumentCount.begin(), compressedGenericArgumentCount.end());
            // Type+
            for (uint16_t i = 0; i < type->GetGenericTypes()->GetSize(); ++i)
            {
                auto genericArgument = type->GetGenericTypes()->GetItem(i);
                auto genericArgumentBytes = TypeToBytes(genericArgument);
                bytes.insert(bytes.end(), genericArgumentBytes.begin(), genericArgumentBytes.end());
            }
            
            return bytes;
        }

        static ByteVector CorSigCompressData(uint32_t dataToCompress)
        {
            if (dataToCompress <= 0x7F)
            {
                ByteVector result;
                result.push_back((unsigned char)dataToCompress);
                return result;
            }

            if (dataToCompress <= 0x3FFF)
            {
                ByteVector result;
                result.push_back((unsigned char)((dataToCompress >> 8) | 0x80));
                result.push_back((unsigned char)(dataToCompress & 0xFF));
                return result;
            }

            if (dataToCompress <= 0x1FFFFFFF)
            {
                ByteVector result;
                result.push_back((unsigned char)((dataToCompress >> 24) | 0xC0));
                result.push_back((unsigned char)((dataToCompress >> 16) & 0xFF));
                result.push_back((unsigned char)((dataToCompress >> 8) & 0xFF));
                result.push_back((unsigned char)(dataToCompress & 0xFF));
                return result;
            }

            throw DataTooLargeToCompressException();
        }

        static ByteVector CorSigCompressToken(uint32_t tokenToCompress)
        {
            uint32_t lowBits = tokenToCompress & 0x00ffffff;
            uint8_t highBits = tokenToCompress >> 24;

            // TypeDef is encoded with low bits 0x02000000
            // TypeRef is encoded with low bits 0x01000000
            // TypeSpec is encoded with low bits 0x1b000000
            // BaseType is encoded with low bits 0x72000000

            uint32_t result = (lowBits << 2);
            if (highBits == 0x02) result |= 0x0;
            else if (highBits == 0x01) result |= 0x1;
            else if (highBits == 0x1b) result |= 0x2;
            else if (highBits == 0x72) result |= 0x3;

            return CorSigCompressData(result);
        }

        static uint32_t CorSigUncompressData(ByteVector::const_iterator& bytes, const ByteVector::const_iterator& end)
        {
            if (bytes == end) throw UnableToDecompressDataException(bytes);

            // get the high bit and find out if this is a single or multibyte number
            auto highBit = bytes[0] & 0x80;
            if (highBit == 0)
            {
                uint32_t result = bytes[0];
                bytes += 1;
                return result;
            }
            
            if (bytes + 1 == end) throw UnableToDecompressDataException(bytes);

            // get the second highest bit and find out if this is a 2 byte or 4 byte number
            auto secondHighestBit = bytes[0] & 0x40;
            if (secondHighestBit == 0)
            {
                uint32_t result = 0;
                result |= (bytes[0] & 0x7F) << 8;
                result |= bytes[1];
                bytes += 2;
                return result;
            }

            if (bytes + 2 == end || bytes + 3 == end) throw UnableToDecompressDataException(bytes);

            uint32_t result = 0;
            result |= (bytes[0] & 0x3F) << 24;
            result |= bytes[1] << 16;
            result |= bytes[2] << 8;
            result |= bytes[3];
            bytes += 4;
            return result;
        }

        static uint32_t CorSigUncompressToken(ByteVector::const_iterator& bytes, const ByteVector::const_iterator& end)
        {
            uint32_t data = CorSigUncompressData(bytes, end);

            auto lowCompressedBits = data & 0x3;
            uint32_t highResultBits = 0;
            if (lowCompressedBits == 0x0) highResultBits = 0x02;
            else if (lowCompressedBits == 0x1) highResultBits = 0x01;
            else if (lowCompressedBits == 0x2) highResultBits = 0x1b;
            else if (lowCompressedBits == 0x3) highResultBits = 0x72;

            return ((highResultBits << 24) | (data >> 2));
        }

    private:
        std::shared_ptr<ITokenizer> tokenizer;

        ByteVector TypeToBytesSpecific(ast::PrimitiveTypePtr type)
        {
            ByteVector bytes;

            // BOOLEAN | CHAR | I1 | U1 | I2 | U2 | I4 | U4 | I8 | U8 | R4 | R8 | I | U
            bytes.push_back((unsigned char)(type->GetPrimitiveKind()));

            //Add the byref flag if necessary (CorElementType::ELEMENT_TYPE_BYREF)
            if (type->GetByRef()) {
                bytes.push_back(0x10);
            }
            
            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::ClassTypePtr type)
        {
            ByteVector bytes;
            
            // CLASS | VALUETYPE
            switch (type->GetClassKind())
            {
            case ast::ClassType::ClassKind::VALUETYPE:
                bytes.push_back(0x11);
                break;
            case ast::ClassType::ClassKind::CLASS:
                bytes.push_back(0x12);
                break;
            default:
                throw UnknownClassKindException(type->GetClassKind());
            }
            // TypeDefOrRefOrSpecEncoded
            auto typeToken = TypeToTokenSpecific(type);
            auto compressedTypeToken = CorSigCompressToken(typeToken);
            bytes.insert(bytes.end(), compressedTypeToken.begin(), compressedTypeToken.end());

            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::GenericTypePtr type)
        {
            ByteVector bytes;

            // GENERICINST
            bytes.push_back(0x15);
            // (CLASS | VALUETYPE) TypeDefOrRefOrSpecEncdoded
            auto uninstantiatedTypeBytes = TypeToBytesSpecific(std::static_pointer_cast<ast::ClassType, ast::GenericType>(type));
            bytes.insert(bytes.end(), uninstantiatedTypeBytes.begin(), uninstantiatedTypeBytes.end());
            // GenArgCount
            auto genericArgumentCount = type->GetGenericTypes()->GetSize();
            auto compressedGenericArgCount = CorSigCompressData(genericArgumentCount);
            bytes.insert(bytes.end(), compressedGenericArgCount.begin(), compressedGenericArgCount.end());
            // Type*
            for (uint16_t i = 0; i < genericArgumentCount; ++i)
            {
                auto genericArgumentType = type->GetGenericTypes()->GetItem(i);
                auto genericArgumentTypeBytes = TypeToBytes(genericArgumentType);
                bytes.insert(bytes.end(), genericArgumentTypeBytes.begin(), genericArgumentTypeBytes.end());
            }

            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::ArrayTypePtr type)
        {
            ByteVector bytes;

            // SZARRAY
            bytes.push_back(0x1d);
            // Type
            auto elementTypeBytes = TypeToBytes(type->GetElementType());
            bytes.insert(bytes.end(), elementTypeBytes.begin(), elementTypeBytes.end());
            
            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::MethodTypePtr type)
        {
            ByteVector bytes;
            
            // first byte (HASTHIS, EXPLICITTHIS, DEFAULT, VARARG, GENERIC ORed together)
            unsigned char firstByte = 0x00;
            if (type->IsInstanceMethod()) firstByte |= 0x20;
            if (type->GetGenericTypes()->GetSize() != 0) firstByte |= 0x10;
            bytes.push_back(firstByte);

            // generic type count
            if (type->GetGenericTypes()->GetSize() > 0)
            {
                auto genericCount = type->GetGenericTypes()->GetSize();
                auto genericCountBytes = CorSigCompressData(genericCount);
                bytes.insert(bytes.end(), genericCountBytes.begin(), genericCountBytes.end());
            }

            // parameter count
            auto paramCount = type->GetArgTypes()->GetSize();
            auto paramCountBytes = CorSigCompressData(paramCount);
            bytes.insert(bytes.end(), paramCountBytes.begin(), paramCountBytes.end());

            // return type
            auto returnType = type->GetReturnType();
            auto returnBytes = TypeToBytes(returnType);
            bytes.insert(bytes.end(), returnBytes.begin(), returnBytes.end());

            // parameters
            for (uint16_t i = 0; i < paramCount; ++i)
            {
                auto paramBytes = TypeToBytes(type->GetArgTypes()->GetItem(i));
                bytes.insert(bytes.end(), paramBytes.begin(), paramBytes.end());
            }

            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::FieldTypePtr type)
        {
            ByteVector bytes;

            // return type
            auto returnType = type->GetReturnType();
            auto returnBytes = TypeToBytes(returnType);
            bytes.insert(bytes.end(), returnBytes.begin(), returnBytes.end());

            return bytes;
        }

        ByteVector TypeToBytesSpecific(ast::GenericParamTypePtr type)
        {
            ByteVector bytes;

            // MVAR | VAR
            bytes.push_back(uint8_t(type->GetGenericParamKind()));
            // Number
            auto numberBytes = CorSigCompressData(type->GetNumber());
            bytes.insert(bytes.end(), numberBytes.begin(), numberBytes.end());

            return bytes;
        }

        uint32_t TypeToTokenSpecific(ast::MethodTypePtr type)
        {
            auto signatureBytes = TypeToBytes(type);
            auto targetTypeToken = TypeToToken(type->GetTargetType());
            auto methodToken = tokenizer->GetMemberRefOrDefToken(targetTypeToken, type->GetMethodName(), signatureBytes);
            
            if (type->GetGenericTypes()->GetSize() > 0)
            {
                methodToken = tokenizer->GetMethodSpecToken(methodToken, GenericMethodInstantiationToSignature(type));
            }

            return methodToken;
        }

        uint32_t TypeToTokenSpecific(ast::FieldTypePtr type)
        {
            auto targetTypeToken = TypeToToken(type->GetTargetType());
            auto fieldToken = tokenizer->GetFieldDefinitionToken(targetTypeToken, type->GetFieldName());

            return fieldToken;
        }

        uint32_t TypeToTokenSpecific(ast::ClassTypePtr type)
        {
            auto typeName = xstring_t();
            typeName += type->GetName();
            if (type->GetKind() == ast::Type::Kind::kGENERICCLASS)
            {
                auto genericType = std::dynamic_pointer_cast<ast::GenericType, ast::ClassType>(type);
                typeName.push_back('`');
                typeName += to_xstring((unsigned)genericType->GetGenericTypes()->GetSize());
            }

            auto assemblyName = type->GetAssembly();
            if (assemblyName.empty())
            {
                return tokenizer->GetTypeDefToken(typeName);
            }
            else
            {
                return tokenizer->GetTypeRefToken(type->GetAssembly(), typeName);
            }
        }

        uint32_t TypeToTokenSpecific(ast::GenericTypePtr type)
        {
            return tokenizer->GetTypeSpecToken(GenericClassInstantiationToSignature(type));
        }

        uint32_t TypeToTokenSpecific(ast::ArrayTypePtr type)
        {
            return tokenizer->GetTypeSpecToken(TypeToBytesSpecific(type));
        }
    };
}}
