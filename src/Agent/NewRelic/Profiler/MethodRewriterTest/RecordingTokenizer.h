// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <algorithm>
#include <memory>
#include <string>
#include <utility>
#include <vector>
#include "../sicily/codegen/ITokenizer.h"
#include "../sicily/SicilyTest/RealisticTokenizer.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    // Delegates every tokenizer call to a RealisticTokenizer so emitted IL keeps
    // realistic, non-zero tokens, while recording the member names that were
    // tokenized. Tests assert on those names instead of on exact byte counts,
    // which keeps them meaningful when unrelated IL sizes shift.
    struct RecordingTokenizer : public sicily::codegen::ITokenizer
    {
        RecordingTokenizer() :
            _inner(std::make_shared<sicily::codegen::RealisticTokenizer>())
        {}

        std::vector<std::wstring> _memberRefMethodNames;
        std::vector<std::pair<std::wstring, std::wstring>> _typeRefRequests;

        bool Tokenized(const std::wstring& methodName) const
        {
            return std::find(_memberRefMethodNames.begin(), _memberRefMethodNames.end(), methodName) != _memberRefMethodNames.end();
        }

        bool TypeRefTokenized(const std::wstring& assemblyName, const std::wstring& fullyQualifiedName) const
        {
            return std::find(_typeRefRequests.begin(), _typeRefRequests.end(), std::make_pair(assemblyName, fullyQualifiedName)) != _typeRefRequests.end();
        }

        virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const std::wstring& methodName, const ByteVector& signature) override
        {
            _memberRefMethodNames.push_back(methodName);
            return _inner->GetMemberRefOrDefToken(parent, methodName, signature);
        }

        virtual uint32_t GetAssemblyRefToken(const std::wstring& assemblyName) override
        {
            return _inner->GetAssemblyRefToken(assemblyName);
        }

        virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& fullyQualifiedName) override
        {
            _typeRefRequests.push_back(std::make_pair(assemblyName, fullyQualifiedName));
            return _inner->GetTypeRefToken(assemblyName, fullyQualifiedName);
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

        virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name, const ByteVector& signature) override
        {
            return _inner->GetMethodDefinitionToken(typeDefinitionToken, name, signature);
        }

        virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name) override
        {
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
        std::shared_ptr<sicily::codegen::RealisticTokenizer> _inner;
    };
}}}}
