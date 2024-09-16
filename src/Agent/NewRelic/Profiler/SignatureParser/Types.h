/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <string>
#include <sstream>
#include <vector>
#include "../Common/Macros.h"
#include "ITokenResolver.h"
#include "../Common/CorStandIn.h"
#include "ByteVectorManipulator.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
    // pre-declared up here since some types (FunctionPointer) can refer to it
    struct MethodSignature;
    typedef std::shared_ptr<MethodSignature> MethodSignaturePtr;

    struct Type
    {
        enum Kind
        {
            BOOLEAN,
            CHAR,
            SBYTE,
            BYTE,
            INT16,
            UINT16,
            INT32,
            UINT32,
            INT64,
            UINT64,
            SINGLE,
            DOUBLE,
            INTPTR,
            UINTPTR,
            OBJECT,
            STRING,
            ARRAY,
            CLASS,
            VALUETYPE,
            FUNCTIONPOINTER,
            GENERIC,
            MVAR,
            VAR,
            POINTER,
            VOIDPOINTER,
            SINGLEDIMENSIONARRAY,
        } _kind;

        Type(Kind kind) : _kind(kind) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const = 0;
        virtual xstring_t ToBaseTypeString(ITokenResolverPtr tokenResolver) const
        {
            return ToString(tokenResolver);
        }

        virtual ByteVectorPtr ToBytes() const = 0;
    };
    typedef std::shared_ptr<Type> TypePtr;
    typedef std::vector<TypePtr> Types;
    typedef std::shared_ptr<Types> TypesPtr;

    struct BooleanType : Type
    {
        BooleanType() : Type(Kind::BOOLEAN) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Boolean");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_BOOLEAN);
            return bytes;
        }
    };

    struct CharType : Type
    {
        CharType() : Type(Kind::CHAR) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Char");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_CHAR);
            return bytes;
        }
    };

    struct SByteType : Type
    {
        SByteType() : Type(Kind::SBYTE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.SByte");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_I1);
            return bytes;
        }
    };

    struct ByteType : Type
    {
        ByteType() : Type(Kind::BYTE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Byte");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_U1);
            return bytes;
        }
    };

    struct Int16Type : Type
    {
        Int16Type() : Type(Kind::INT16) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Int16");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_I2);
            return bytes;
        }
    };

    struct UInt16Type : Type
    {
        UInt16Type() : Type(Kind::UINT16) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.UInt16");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_U2);
            return bytes;
        }
    };

    struct Int32Type : Type
    {
        Int32Type() : Type(Kind::INT32) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Int32");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_I4);
            return bytes;
        }
    };

    struct UInt32Type : Type
    {
        UInt32Type() : Type(Kind::UINT32) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.UInt32");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_U4);
            return bytes;
        }
    };

    struct Int64Type : Type
    {
        Int64Type() : Type(Kind::INT64) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Int64");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_I8);
            return bytes;
        }
    };

    struct UInt64Type : Type
    {
        UInt64Type() : Type(Kind::UINT64) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.UInt64");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_U8);
            return bytes;
        }
    };

    struct SingleType : Type
    {
        SingleType() : Type(Kind::SINGLE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Single");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_R4);
            return bytes;
        }
    };

    struct DoubleType : Type
    {
        DoubleType() : Type(Kind::DOUBLE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Double");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_R8);
            return bytes;
        }
    };

    struct IntPtrType : Type
    {
        IntPtrType() : Type(Kind::INTPTR) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.IntPtr");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_I);
            return bytes;
        }
    };

    struct UIntPtrType : Type
    {
        UIntPtrType() : Type(Kind::UINTPTR) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.UIntPtr");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_U);
            return bytes;
        }
    };

    struct ObjectType : Type
    {
        ObjectType() : Type(Kind::OBJECT) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.Object");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_OBJECT);
            return bytes;
        }
    };

    struct StringType : Type
    {
        StringType() : Type(Kind::STRING) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.String");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_STRING);
            return bytes;
        }
    };

    struct ArrayType : Type
    {
        TypePtr _type;
        uint32_t _dimensions;
        std::vector<uint32_t> _sizes;
        std::vector<uint32_t> _lowerBounds;

        ArrayType(TypePtr type, uint32_t dimensions, std::vector<uint32_t> sizes, std::vector<uint32_t> lowerBounds) :
            Type(Kind::ARRAY),
            _type(type),
            _dimensions(dimensions),
            _sizes(sizes),
            _lowerBounds(lowerBounds)
        {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            xstring_t stream(_type->ToString(tokenResolver));
            stream.push_back('[');
            bool first = true;
            for (uint32_t i = 0; i < _dimensions; ++i)
            {
                if (first) first = false;
                else stream.push_back(',');

                if (_sizes.size() <= i) continue;

                if (_lowerBounds.size() <= i)
                {
                    stream += _X("0...") + to_xstring(_sizes[i] - 1);
                }
                else
                {
                    stream += to_xstring(_lowerBounds[i]) + _X("...") + to_xstring(_lowerBounds[i] + _sizes[i] - 1);
                }
            }
            stream.push_back(']');
            return stream;
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_ARRAY);
            auto typeBytes = _type->ToBytes();
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            auto compressedDimensions = CompressData(_dimensions);
            bytes->insert(bytes->end(), compressedDimensions->begin(), compressedDimensions->end());
            auto compressedSizeCount = CompressData(uint32_t(_sizes.size()));
            bytes->insert(bytes->end(), compressedSizeCount->begin(), compressedSizeCount->end());
            for (auto size : _sizes)
            {
                auto compressedSize = CompressData(size);
                bytes->insert(bytes->end(), compressedSize->begin(), compressedSize->end());
            }
            auto compressedLowerBoundCount = CompressData(uint32_t(_lowerBounds.size()));
            bytes->insert(bytes->end(), compressedLowerBoundCount->begin(), compressedLowerBoundCount->end());
            for (auto lowerBound : _lowerBounds)
            {
                auto compressedLowerBound = CompressData(lowerBound);
                bytes->insert(bytes->end(), compressedLowerBound->begin(), compressedLowerBound->end());
            }
            return bytes;
        }
    };

    struct ClassType : Type
    {
        uint32_t _typeToken;

        ClassType(uint32_t typeToken) : Type(Kind::CLASS), _typeToken(typeToken) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return tokenResolver->GetTypeStringsFromTypeDefOrRefOrSpecToken(_typeToken);
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto compressedToken = CompressToken(_typeToken);
            bytes->push_back(ELEMENT_TYPE_CLASS);
            bytes->insert(bytes->end(), compressedToken->begin(), compressedToken->end());
            return bytes;
        }
    };

    struct ValueTypeType : Type
    {
        uint32_t _typeToken;

        ValueTypeType(uint32_t typeToken) : Type(Kind::VALUETYPE), _typeToken(typeToken) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return tokenResolver->GetTypeStringsFromTypeDefOrRefOrSpecToken(_typeToken);
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto compressedToken = CompressToken(_typeToken);
            bytes->push_back(ELEMENT_TYPE_VALUETYPE);
            bytes->insert(bytes->end(), compressedToken->begin(), compressedToken->end());
            return bytes;
        }
    };

    struct FunctionPointerType : Type
    {
        MethodSignaturePtr _methodSignature;

        FunctionPointerType(MethodSignaturePtr methodSignature) : Type(Kind::FUNCTIONPOINTER), _methodSignature(methodSignature) {}

        // has to be defined later since it uses MethodSignature which isn't defined until later
        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override;

        // has to be defined later since it uses MethodSignature which isn't defined until later
        virtual ByteVectorPtr ToBytes() const override;
    };

    struct GenericType : Type
    {
        TypePtr _type;
        TypesPtr _genericArgumentTypes;
        
        GenericType(TypePtr type, TypesPtr genericArgumentTypes) : Type(Kind::GENERIC), _type(type), _genericArgumentTypes(genericArgumentTypes) {}

        virtual xstring_t ToBaseTypeString(ITokenResolverPtr tokenResolver) const override
        {
            return _type->ToString(tokenResolver);
        }

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            auto stream = xstring_t();
            stream += _type->ToString(tokenResolver);
            stream.push_back('[');
            bool first = true;
            for (auto genericArgumentType : *_genericArgumentTypes)
            {
                if (first) first = false;
                else stream.push_back(',');
                stream += genericArgumentType->ToString(tokenResolver);
            }
            stream.push_back(']');
            return stream;
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto typeBytes = _type->ToBytes();
            auto compressedArgCount = CompressData(uint32_t(_genericArgumentTypes->size()));
            bytes->push_back(ELEMENT_TYPE_GENERICINST);
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            bytes->insert(bytes->end(), compressedArgCount->begin(), compressedArgCount->end());
            for (auto argumentType : *_genericArgumentTypes)
            {
                auto argumentTypeBytes = argumentType->ToBytes();
                bytes->insert(bytes->end(), argumentTypeBytes->begin(), argumentTypeBytes->end());
            }
            return bytes;
        }
    };

    typedef std::shared_ptr<GenericType> GenericTypePtr;

    struct MvarType : Type
    {
        uint32_t _number;

        MvarType(uint32_t number) : Type(Kind::MVAR), _number(number) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return _X("!!") + to_xstring(_number);
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto compressedNumber= CompressData(_number);
            bytes->push_back(ELEMENT_TYPE_MVAR);
            bytes->insert(bytes->end(), compressedNumber->begin(), compressedNumber->end());
            return bytes;
        }
    };

    struct VarType : Type
    {
        uint32_t _number;

        VarType(uint32_t number) : Type(Kind::VAR), _number(number) {}
        
        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return _X("!") + to_xstring(_number);
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto compressedNumber = CompressData(_number);
            bytes->push_back(ELEMENT_TYPE_VAR);
            bytes->insert(bytes->end(), compressedNumber->begin(), compressedNumber->end());
            return bytes;
        }
    };

    struct PointerType : Type
    {
        TypePtr _type;

        PointerType(TypePtr type) : Type(Kind::POINTER), _type(type) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            xstring_t stream(_type->ToString(tokenResolver));
            stream.push_back('*');
            return stream;
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto typeBytes = _type->ToBytes();
            bytes->push_back(ELEMENT_TYPE_PTR);
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            return bytes;
        }
    };

    struct VoidPointerType : Type
    {
        VoidPointerType() : Type(Kind::VOIDPOINTER) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return _X("void*");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_PTR);
            bytes->push_back(ELEMENT_TYPE_VOID);
            return bytes;
        }
    };

    struct SingleDimensionArrayType : Type
    {
        TypePtr _elementType;

        SingleDimensionArrayType(TypePtr elementType) : Type(Kind::SINGLEDIMENSIONARRAY), _elementType(elementType) {}
        
        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            return _elementType->ToString(tokenResolver) + _X("[]");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto typeBytes = _elementType->ToBytes();
            bytes->push_back(ELEMENT_TYPE_SZARRAY);
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            return bytes;
        }
    };

    struct CustomMod
    {
        bool _isRequired;
        uint32_t _typeDefOrRefOrSpecEncodedToken;
        CustomMod(bool isRequired, uint32_t token) : _isRequired(isRequired), _typeDefOrRefOrSpecEncodedToken(token) {}
    };
    typedef std::shared_ptr<CustomMod> CustomModPtr;

    struct ReturnType
    {
        enum Kind
        {
            TYPED_RETURN_TYPE,
            TYPED_BY_REF_RETURN_TYPE,
            VOID_RETURN_TYPE,
        } _kind;

        ReturnType(Kind kind) : _kind(kind) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const = 0;
        virtual xstring_t ToBaseTypeString(ITokenResolverPtr tokenResolver) const {
            return ToString(tokenResolver);
        }

        virtual ByteVectorPtr ToBytes() const = 0;
    };
    typedef std::shared_ptr<ReturnType> ReturnTypePtr;

    struct TypedReturnType : ReturnType
    {
        TypePtr _type;
        bool _isByRef;

        TypedReturnType(TypePtr type, bool isByRef) : ReturnType(ReturnType::Kind::TYPED_RETURN_TYPE), _type(type), _isByRef(isByRef) {}

        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            if (_isByRef) {
                return _type->ToString(tokenResolver) + _X("&");
            } else {
                return _type->ToString(tokenResolver);
            }
        }

        virtual xstring_t ToBaseTypeString(ITokenResolverPtr tokenResolver) const override {
            if (_isByRef) {
                return _type->ToBaseTypeString(tokenResolver) + _X("&");
            } else {
                return _type->ToBaseTypeString(tokenResolver);
            }
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto typeBytes = _type->ToBytes();
            if (_isByRef) bytes->push_back(ELEMENT_TYPE_BYREF);
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            return bytes;
        }
    };

    struct VoidReturnType : ReturnType
    {
        VoidReturnType() : ReturnType(ReturnType::Kind::VOID_RETURN_TYPE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("void");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_VOID);
            return bytes;
        }
    };

    struct TypedByRefReturnType : ReturnType
    {
        TypedByRefReturnType() : ReturnType(ReturnType::Kind::TYPED_BY_REF_RETURN_TYPE) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.TypedReference");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_TYPEDBYREF);
            return bytes;
        }
    };

    struct Parameter
    {
        enum Kind
        {
            TYPED_PARAMETER,
            TYPED_BY_REF_PARAMETER,
            SENTINEL_PARAMETER,
        } _kind;

        Parameter(Kind kind) : _kind(kind) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const = 0;

        virtual ByteVectorPtr ToBytes() const = 0;
    };
    typedef std::shared_ptr<Parameter> ParameterPtr;
    typedef std::vector<ParameterPtr> Parameters;
    typedef std::shared_ptr<Parameters> ParametersPtr;

    struct TypedParameter : Parameter
    {
        TypePtr _type;
        bool _isByRef;
        
        TypedParameter(TypePtr type, bool isByRef) : Parameter(Kind::TYPED_PARAMETER), _type(type), _isByRef(isByRef) {}
        
        virtual xstring_t ToString(ITokenResolverPtr tokenResolver) const override
        {
            if (_isByRef) {
                return _type->ToString(tokenResolver) + _X("&");
            } else {
                return _type->ToString(tokenResolver);
            }
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            auto typeBytes = _type->ToBytes();
            if (_isByRef) bytes->push_back(ELEMENT_TYPE_BYREF);
            bytes->insert(bytes->end(), typeBytes->begin(), typeBytes->end());
            return bytes;
        }
    };

    struct TypedByRefParameter : Parameter
    {
        TypedByRefParameter() : Parameter(Kind::TYPED_BY_REF_PARAMETER) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("System.TypedReference");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_TYPEDBYREF);
            return bytes;
        }
    };

    struct SentinelParameter : Parameter
    {
        SentinelParameter() : Parameter(Kind::SENTINEL_PARAMETER) {}

        virtual xstring_t ToString(ITokenResolverPtr /*tokenResolver*/) const override
        {
            return _X("...");
        }

        virtual ByteVectorPtr ToBytes() const override
        {
            ByteVectorPtr bytes(new ByteVector());
            bytes->push_back(ELEMENT_TYPE_SENTINEL);
            return bytes;
        }
    };

    struct MethodSignature
    {
        bool _hasThis;
        bool _explicitThis;
        uint8_t _callingConvention;
        ReturnTypePtr _returnType;
        ParametersPtr _parameters;
        uint32_t _genericParamCount;

        MethodSignature(bool hasThis, bool explicitThis, uint8_t callingConvention, ReturnTypePtr returnType, ParametersPtr parameters, uint32_t genericParamCount) :
            _hasThis(hasThis),
            _explicitThis(explicitThis),
            _callingConvention(callingConvention),
            _returnType(returnType),
            _parameters(parameters),
            _genericParamCount(genericParamCount)
        {}

        xstring_t ToString(ITokenResolverPtr tokenResolver)
        {
            auto stream = xstring_t();
            bool firstParam = true;
            for (auto parameter : *_parameters)
            {
                if (firstParam) firstParam = false;
                else stream.push_back(',');

                stream += parameter->ToString(tokenResolver);
            }
            return stream;
        }

        ByteVectorPtr ToBytes()
        {
            ByteVectorPtr bytes(new ByteVector());
            uint8_t firstByte = 0;
            if (_hasThis) firstByte |= CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS;
            if (_explicitThis) firstByte |= CorCallingConvention::IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS;
            firstByte |= _callingConvention;
            bytes->push_back(firstByte);

            if (_callingConvention == CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC)
            {
                auto compressedGenericParamCount = CompressData(_genericParamCount);
                bytes->insert(bytes->end(), compressedGenericParamCount->begin(), compressedGenericParamCount->end());
            }

            auto compressedParamCount = CompressData(uint32_t(_parameters->size()));
            bytes->insert(bytes->end(), compressedParamCount->begin(), compressedParamCount->end());

            auto returnTypeBytes = _returnType->ToBytes();
            bytes->insert(bytes->end(), returnTypeBytes->begin(), returnTypeBytes->end());

            for (auto parameter : *_parameters)
            {
                auto parameterBytes = parameter->ToBytes();
                bytes->insert(bytes->end(), parameterBytes->begin(), parameterBytes->end());
            }

            return bytes;
        }

        ByteVectorPtr GetGenericInstantiationSignature()
        {
            ByteVectorPtr bytes(new ByteVector());

            // if this isn't a generic method then just return
            if (_genericParamCount == 0) return bytes;

            // GENERICINST (0x0A)
            bytes->push_back(0x0a);

            // GenArgCount
            auto compressedArgumentCount = CompressData(_genericParamCount);
            bytes->insert(bytes->end(), compressedArgumentCount->begin(), compressedArgumentCount->end());

            // Type*
            for (uint32_t i = 0; i < _genericParamCount; ++i)
            {
                // MVAR
                bytes->push_back(0x1e);
                // MVAR #
                auto compressedArgumentNumber = CompressData(i);
                bytes->insert(bytes->end(), compressedArgumentNumber->begin(), compressedArgumentNumber->end());
            }

            return bytes;
        }
    };

    // must be defined after MethodSignature since it uses it
    inline xstring_t FunctionPointerType::ToString(ITokenResolverPtr tokenResolver) const
    {
        auto stream = xstring_t();
        stream.push_back('(');
        stream += _methodSignature->_returnType->ToString(tokenResolver) + _X(")(") + _methodSignature->ToString(tokenResolver);
        stream.push_back(')');
        return stream;
    }

    // must be defined after MethodSignature since it uses it
    inline ByteVectorPtr FunctionPointerType::ToBytes() const
    {
        ByteVectorPtr bytes(new ByteVector());
        auto methodBytes = _methodSignature->ToBytes();
        bytes->push_back(ELEMENT_TYPE_FNPTR);
        bytes->insert(bytes->end(), methodBytes->begin(), methodBytes->end());
        return bytes;
    }

}}}
