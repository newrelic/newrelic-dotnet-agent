/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../../Common/xplat.h"
#include <vector>
#include <memory>
#include <stdint.h>

namespace sicily
{
    namespace codegen
    {
        typedef std::vector<uint8_t> ByteVector;
        typedef std::shared_ptr<ByteVector> ByteVectorPtr;

        class ITokenizer
        {
        public:
            virtual uint32_t GetAssemblyRefToken(const xstring_t& assemblyName) = 0;
            virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& fullyQualifiedName) = 0;
            virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& name, const xstring_t& namespaceName) = 0;
            virtual uint32_t GetTypeDefToken(const xstring_t& fullName) = 0;
            virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) = 0;
            virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const xstring_t& methodName, const ByteVector& signature) = 0;
            virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const xstring_t& name, const ByteVector& signature) = 0;
            virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const xstring_t& name) = 0;
            virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) = 0;
            virtual uint32_t GetStringToken(const xstring_t& string) = 0;
            virtual ~ITokenizer() {}
        };

        typedef std::shared_ptr<ITokenizer> ITokenizerPtr;
    }
}
