// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../codegen/ITokenizer.h"

namespace sicily
{
    namespace codegen
    {
        class NullTokenizer : public ITokenizer
        {
        public:
            NullTokenizer() {}
            virtual uint32_t GetAssemblyRefToken(const std::wstring& /*assemblyName*/) override final { return 0; }
            virtual uint32_t GetTypeRefToken(const std::wstring& /*assemblyName*/, const std::wstring& /*fullyQualifiedName*/) override final { return 0; }
            virtual uint32_t GetTypeRefToken(const std::wstring& /*assemlbyName*/, const std::wstring& /*name*/, const std::wstring& /*namespaceName*/) override final { return 0; }
            virtual uint32_t GetTypeDefToken(const std::wstring& /*fullName*/) override final { return 0; }
            virtual uint32_t GetTypeSpecToken(const ByteVector& /*instantiationSignature*/) override final { return 0; }
            virtual uint32_t GetMemberRefOrDefToken(uint32_t /*parent*/, const std::wstring& /*methodName*/, const ByteVector& /*signature*/) override final { return 0; }
            virtual uint32_t GetMethodDefinitionToken(const uint32_t& /*typeDefinitionToken*/, const std::wstring& /*name*/, const ByteVector& /*signature*/) { return 0; }
            virtual uint32_t GetFieldDefinitionToken(const uint32_t& /*typeDefinitionToken*/, const std::wstring& /*name*/) { return 0; }
            virtual uint32_t GetMethodSpecToken(uint32_t /*methodDefOrRefOrSpecToken*/, const ByteVector& /*instantiationSignature*/) override final { return 0; }
            virtual uint32_t GetStringToken(const std::wstring& /*string*/) override final { return 0; }
            virtual ~NullTokenizer() {}
        };

        typedef std::shared_ptr<NullTokenizer> NullTokenizerPtr;
    }
}
