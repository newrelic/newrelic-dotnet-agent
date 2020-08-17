// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../SignatureParser/ITokenResolver.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser { namespace Test
{
    class MockTokenResolver : public ITokenResolver
    {
        virtual std::wstring GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t typeDefOrRefOrSPecToken) override
        {
            return L"MyNamespace1.MyNamespace2.MyClass";
        }

        uint32_t _typeGenericArgumentCount;
        virtual uint32_t GetTypeGenericArgumentCount(uint32_t /*typeDefOrMethodDefToken*/)
        {
            return _typeGenericArgumentCount;
        }
    };
}}}}
