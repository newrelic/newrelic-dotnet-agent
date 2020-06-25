/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <vector>
#include "../Common/CorStandIn.h"
#include "../Common/Macros.h"
#include "Exceptions.h"
#include "Types.h"
#include "../Logging/Logger.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
    struct SignatureParser
    {
        static uint32_t UncompressData(ByteVector::const_iterator& bytes, const ByteVector::const_iterator& end)
        {
            if (bytes == end)
            {
                LogError(L"Attempted to read past the end of the signature while decompressing an integer.");
                throw SignatureParserException();
            }

            // get the high bit and find out if this is a single or multibyte number
            auto highBit = bytes[0] & 0x80;
            if (highBit == 0)
            {
                uint32_t result = bytes[0];
                bytes += 1;
                return result;
            }
            
            if (bytes + 1 == end)
            {
                LogError(L"Attempted to read past the end of the signature while decompressing an integer.");
                throw SignatureParserException();
            }

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

            if (bytes + 2 == end || bytes + 3 == end)
            {
                LogError(L"Attempted to read past the end of the signature while decompressing an integer.");
                throw SignatureParserException();
            }

            uint32_t result = 0;
            result |= (bytes[0] & 0x3F) << 24;
            result |= bytes[1] << 16;
            result |= bytes[2] << 8;
            result |= bytes[3];
            bytes += 4;
            return result;
        }

        static uint32_t UncompressToken(ByteVector::const_iterator& bytes, const ByteVector::const_iterator& end)
        {
            uint32_t data = UncompressData(bytes, end);

            auto lowCompressedBits = data & 0x3;
            uint32_t highResultBits = 0;
            if (lowCompressedBits == 0x0) highResultBits = 0x02;
            else if (lowCompressedBits == 0x1) highResultBits = 0x01;
            else if (lowCompressedBits == 0x2) highResultBits = 0x1b;
            else if (lowCompressedBits == 0x3) highResultBits = 0x72;

            return ((highResultBits << 24) | (data >> 2));
        }
        
        static MethodSignaturePtr ParseMethodSignature(const ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            ByteVector::const_iterator nonConstIterator = iterator;
            return ParseMethodSignature(nonConstIterator, end);
        }

        static MethodSignaturePtr ParseMethodSignature(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while parsing a method signature.");
                throw SignatureParserException();
            }
            uint8_t firstByte = *iterator++;
            bool hasThis = (firstByte & CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS) ? true : false;
            bool explicitThis = (firstByte & CorCallingConvention::IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS) ? true : false;
            uint8_t callingConvention = (firstByte & (CorCallingConvention::IMAGE_CEE_CS_CALLCONV_MASK | CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC));
            bool isGeneric = (firstByte & CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC) ? true : false;
            
            uint32_t genericParamCount = isGeneric ? UncompressData(iterator, end) : 0;
            auto paramCount = UncompressData(iterator, end);
            auto returnType = ParseReturnType(iterator, end);
            auto parameters = ParseParameters(paramCount, iterator, end);

            return MethodSignaturePtr(new MethodSignature(hasThis, explicitThis, callingConvention, returnType, parameters, genericParamCount));
        }

        static bool TryParseByRef(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while checking for a ByRef token.");
                throw SignatureParserException();
            }

            if (*iterator == ELEMENT_TYPE_BYREF)
            {
                ++iterator;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool TryParseVoid(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while checking for a void token.");
                throw SignatureParserException();
            }

            if (*iterator == ELEMENT_TYPE_VOID)
            {
                ++iterator;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool TryParseTypedByRef(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while checking for a TypedByRef token.");
                throw SignatureParserException();
            }

            if (*iterator == ELEMENT_TYPE_TYPEDBYREF)
            {
                ++iterator;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool TryParseCustomMod(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while checking for a custom mod.");
                throw SignatureParserException();
            }

            if (*iterator == ELEMENT_TYPE_CMOD_OPT || *iterator == ELEMENT_TYPE_CMOD_REQD)
            {
                ++iterator;
                /*auto typeToken =*/ UncompressToken(iterator, end);
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool TryParseSentinel(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while checking for a sentinel token.");
                throw SignatureParserException();
            }

            if (*iterator == ELEMENT_TYPE_SENTINEL)
            {
                ++iterator;
                return true;
            }
            else
            {
                return false;
            }
        }

        static TypePtr ParseType(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            if (iterator == end)
            {
                LogError(L"Attempted to read past the end of the signature while parsing a type.");
                throw SignatureParserException();
            }

            //Managed C++ and C++/CLI compilers can generate methods that have CustomMods in
            //unexpected locations as defined by the grammar in the ECMA standard. See
            //https://github.com/dotnet/corefx/blob/master/src/System.Reflection.Metadata/specs/Ecma-335-Issues.md
            //for more details.
            //This is not part of the switch statement so that we can better reuse the existing code for parsing custom
            //modifiers and their associated additional byte(s) of data.
            while (TryParseCustomMod(iterator, end));

            auto token = *iterator++;
            switch(token)
            {
                case ELEMENT_TYPE_BOOLEAN: return std::make_shared<BooleanType>();
                case ELEMENT_TYPE_CHAR: return std::make_shared<CharType>();
                case ELEMENT_TYPE_I1: return std::make_shared<SByteType>();
                case ELEMENT_TYPE_U1: return std::make_shared<ByteType>();
                case ELEMENT_TYPE_I2: return std::make_shared<Int16Type>();
                case ELEMENT_TYPE_U2: return std::make_shared<UInt16Type>();
                case ELEMENT_TYPE_I4: return std::make_shared<Int32Type>();
                case ELEMENT_TYPE_U4: return std::make_shared<UInt32Type>();
                case ELEMENT_TYPE_I8: return std::make_shared<Int64Type>();
                case ELEMENT_TYPE_U8: return std::make_shared<UInt64Type>();
                case ELEMENT_TYPE_R4: return std::make_shared<SingleType>();
                case ELEMENT_TYPE_R8: return std::make_shared<DoubleType>();
                case ELEMENT_TYPE_I: return std::make_shared<IntPtrType>();
                case ELEMENT_TYPE_U: return std::make_shared<UIntPtrType>();
                case ELEMENT_TYPE_OBJECT: return std::make_shared<ObjectType>();
                case ELEMENT_TYPE_STRING: return std::make_shared<StringType>();
                case ELEMENT_TYPE_ARRAY:
                {
                    auto elementType = ParseType(iterator, end);
                    auto dimensionCount = UncompressData(iterator, end);
                    auto sizeCount = UncompressData(iterator, end);
                    std::vector<uint32_t> sizes;
                    sizes.reserve(sizeCount);
                    for (uint32_t i = 0; i < sizeCount; ++i)
                    {
                        auto size = UncompressData(iterator, end);
                        sizes.push_back(size);
                    }
                    auto lowerBoundCount = UncompressData(iterator, end);
                    std::vector<uint32_t> lowerBounds;
                    lowerBounds.reserve(lowerBoundCount);
                    for (uint32_t i = 0; i < lowerBoundCount; ++i)
                    {
                        auto lowerBound = UncompressData(iterator, end);
                        lowerBounds.push_back(lowerBound);
                    }

                    return std::make_shared<ArrayType>(elementType, dimensionCount, sizes, lowerBounds);
                }
                case ELEMENT_TYPE_CLASS:
                {
                    auto typeToken = UncompressToken(iterator, end);
                    return std::make_shared<ClassType>(typeToken);
                }
                case ELEMENT_TYPE_FNPTR:
                {
                    auto methodSignature = ParseMethodSignature(iterator, end);
                    return std::make_shared<FunctionPointerType>(methodSignature);
                }
                case ELEMENT_TYPE_GENERICINST:
                {
                    auto type = ParseType(iterator, end);
                    auto genericArgumentCount = UncompressData(iterator, end);
                    auto genericArgumentTypes = std::make_shared<Types>();
                    for (uint32_t i = 0; i < genericArgumentCount; ++i)
                    {
                        genericArgumentTypes->push_back(ParseType(iterator, end));
                    }
                    return std::make_shared<GenericType>(type, genericArgumentTypes);
                }
                case ELEMENT_TYPE_MVAR:
                {
                    auto number = UncompressData(iterator, end);
                    return std::make_shared<MvarType>(number);
                }
                case ELEMENT_TYPE_PTR:
                {
                    while (TryParseCustomMod(iterator, end));
                    bool isVoid = TryParseVoid(iterator, end);
                    if (isVoid) return std::make_shared<VoidPointerType>();
                    
                    auto type = ParseType(iterator, end);
                    return std::make_shared<PointerType>(type);
                }
                case ELEMENT_TYPE_SZARRAY:
                {
                    while (TryParseCustomMod(iterator, end));
                    auto type = ParseType(iterator, end);
                    return std::make_shared<SingleDimensionArrayType>(type);
                }
                case ELEMENT_TYPE_VALUETYPE:
                {
                    auto typeToken = UncompressToken(iterator, end);
                    return std::make_shared<ValueTypeType>(typeToken);
                }
                case ELEMENT_TYPE_VAR:
                {
                    auto number = UncompressData(iterator, end);
                    return std::make_shared<VarType>(number);
                }
                default:
                {
                    LogError("Unhandled token encountered while parsing the type.  Token: " , std::hex, std::showbase, token, std::resetiosflags(std::ios_base::basefield|std::ios_base::showbase));
                    throw SignatureParserException();
                }
            }
        }

        static ReturnTypePtr ParseReturnType(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            while (TryParseCustomMod(iterator, end));
            
            bool isVoid = TryParseVoid(iterator, end);
            if (isVoid) return std::make_shared<VoidReturnType>();
            
            bool isTypedByRef = TryParseTypedByRef(iterator, end);
            if (isTypedByRef) return std::make_shared<TypedByRefReturnType>();
            
            bool isByRef = TryParseByRef(iterator, end);
            TypePtr type = ParseType(iterator, end);
            return std::make_shared<TypedReturnType>(type, isByRef);
        }

        static ParametersPtr ParseParameters(uint32_t paramCount, ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            auto parameters = std::make_shared<Parameters>();
            for (uint32_t i = 0; i < paramCount; ++i)
            {
                parameters->push_back(ParseParameter(iterator, end));
            }
            return parameters;
        }

        static ParameterPtr ParseParameter(ByteVector::const_iterator& iterator, const ByteVector::const_iterator& end)
        {
            while (TryParseCustomMod(iterator, end));

            bool isTypedByRef = TryParseTypedByRef(iterator, end);
            if (isTypedByRef) return std::make_shared<TypedByRefParameter>();

            bool isSentinel = TryParseSentinel(iterator, end);
            if (isSentinel) return std::make_shared<SentinelParameter>();

            bool isByRef = TryParseByRef(iterator, end);
            TypePtr type = ParseType(iterator, end);
            return std::make_shared<TypedParameter>(type, isByRef);
        }
    };
    typedef std::shared_ptr<SignatureParser> SignatureParserPtr;
}}}
