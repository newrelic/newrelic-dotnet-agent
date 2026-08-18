// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <algorithm>
#include <memory>
#include <string>
#include <utility>
#include <vector>
#include "../codegen/ITokenizer.h"
#include "RealisticTokenizer.h"

namespace sicily
{
    namespace codegen
    {
        // Delegates every tokenizer call to a RealisticTokenizer so returned tokens stay
        // realistic and non-zero, while recording the (typeDefinitionToken, name) pairs
        // passed to GetFieldDefinitionToken. NullTokenizer cannot support this: it returns a
        // hardcoded zero for every call regardless of arguments, so it cannot tell "the right
        // arguments were threaded through" apart from "nothing was passed at all".
        class RecordingTokenizer : public ITokenizer
        {
        public:
            RecordingTokenizer() :
                _inner(std::make_shared<RealisticTokenizer>())
            {}

            // The token most recently handed out by GetTypeRefToken. Tests must compare against
            // this rather than re-requesting the same type, because RealisticTokenizer::GetToken is
            // NOT idempotent for the first entry in a table: it uses 0 as both "not found" and a
            // valid index, so a repeat lookup of index 0 pushes a duplicate and returns a new token.
            uint32_t LastTypeRefToken() const
            {
                return _lastTypeRefToken;
            }

            bool FieldDefinitionTokenized(uint32_t typeDefinitionToken, const std::wstring& name) const
            {
                return std::find(_fieldDefinitionRequests.begin(), _fieldDefinitionRequests.end(), std::make_pair(typeDefinitionToken, name)) != _fieldDefinitionRequests.end();
            }

            virtual uint32_t GetAssemblyRefToken(const std::wstring& assemblyName) override
            {
                return _inner->GetAssemblyRefToken(assemblyName);
            }

            virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& fullyQualifiedName) override
            {
                _lastTypeRefToken = _inner->GetTypeRefToken(assemblyName, fullyQualifiedName);
                return _lastTypeRefToken;
            }

            virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& name, const std::wstring& namespaceName) override
            {
                return _inner->GetTypeRefToken(assemblyName, name, namespaceName);
            }

            virtual uint32_t GetTypeDefToken(const std::wstring& fullName) override
            {
                return _inner->GetTypeDefToken(fullName);
            }

            virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) override
            {
                return _inner->GetTypeSpecToken(instantiationSignature);
            }

            virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const std::wstring& methodName, const ByteVector& signature) override
            {
                return _inner->GetMemberRefOrDefToken(parent, methodName, signature);
            }

            virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name, const ByteVector& signature) override
            {
                return _inner->GetMethodDefinitionToken(typeDefinitionToken, name, signature);
            }

            virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name) override
            {
                _fieldDefinitionRequests.push_back(std::make_pair(typeDefinitionToken, name));
                return _inner->GetFieldDefinitionToken(typeDefinitionToken, name);
            }

            virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) override
            {
                return _inner->GetMethodSpecToken(methodDefOrRefOrSpecToken, instantiationSignature);
            }

            virtual uint32_t GetStringToken(const std::wstring& string) override
            {
                return _inner->GetStringToken(string);
            }

        private:
            std::shared_ptr<RealisticTokenizer> _inner;
            std::vector<std::pair<uint32_t, std::wstring>> _fieldDefinitionRequests;
            uint32_t _lastTypeRefToken = 0;
        };

        typedef std::shared_ptr<RecordingTokenizer> RecordingTokenizerPtr;
    }
}
