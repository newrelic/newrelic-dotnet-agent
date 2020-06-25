/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <stdint.h>
#include <memory>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
    class ITokenResolver
    {
    public:
        virtual xstring_t GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t typeDefOrRefOrSPecToken) = 0;
        virtual uint32_t GetTypeGenericArgumentCount(uint32_t typeDefOrMethodDefToken) = 0;
    };
    typedef std::shared_ptr<ITokenResolver> ITokenResolverPtr;
}}}
