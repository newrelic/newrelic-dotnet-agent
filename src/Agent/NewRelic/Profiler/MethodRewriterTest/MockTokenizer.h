// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../sicily/codegen/ITokenizer.h"

using namespace sicily::codegen;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test 
{
    //typedef std::vector<uint8_t> ByteVector;

    struct MockTokenizer : public sicily::codegen::ITokenizer
    {
        MockTokenizer() :
            _assemblyRefToken(0),
            _typeRefToken(0),
            _typeDefToken(0),
            _typeSpecToken(0),
            _memberRefOrDefToken(0),
            _methodDefinitionToken(0),
            _fieldDefinitionToken(0),
            _methodSpecToken(0),
            _stringToken(0),
            _instantiationSignature(nullptr),
            _methodBytes(nullptr)
        {
        }

        uint32_t _assemblyRefToken;
        virtual uint32_t GetAssemblyRefToken(const std::wstring& assemblyName) override
        {
            assemblyName;
            return _assemblyRefToken;
        }

        uint32_t _typeRefToken;
        virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& fullyQualifiedName) override
        {
            assemblyName;
            fullyQualifiedName;
            return _typeRefToken;
        }

        
        virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& name, const std::wstring& namespaceName) override
        {
            assemblyName;
            name;
            namespaceName;
            return _typeRefToken;
        }

        uint32_t _typeDefToken;
        virtual uint32_t GetTypeDefToken(const std::wstring& fullName) override
        {
            fullName;
            return _typeDefToken;
        }

        ByteVectorPtr _instantiationSignature;
        uint32_t _typeSpecToken;
        virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) override
        {
            _instantiationSignature = std::make_shared<ByteVector>(instantiationSignature);
            return _typeSpecToken;
        }

        ByteVectorPtr _methodBytes;
        uint32_t _memberRefOrDefToken;
        virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const std::wstring& methodName, const ByteVector& signature) override
        {
            parent;
            methodName;
            _methodBytes = std::make_shared<ByteVector>(signature);
            return _memberRefOrDefToken;
        }

        uint32_t _methodDefinitionToken;
        virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name, const ByteVector& signature) override
        {
            typeDefinitionToken;
            name;
            signature;
            return _methodDefinitionToken;
        }

        uint32_t _fieldDefinitionToken;
        virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name) override
        {
            typeDefinitionToken;
            name;
            return _fieldDefinitionToken;
        }

        uint32_t _methodSpecToken;
        virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) override
        {
            methodDefOrRefOrSpecToken;
            instantiationSignature;
            return _methodSpecToken;
        }

        uint32_t _stringToken;
        virtual uint32_t GetStringToken(const std::wstring& string) override
        {
            string;
            return _stringToken;
        }
    };
}}}}
