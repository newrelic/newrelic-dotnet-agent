/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "Exceptions.h"
#include "../Logging/Logger.h"
#include "../Common/Macros.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
    static ByteVectorPtr CompressData(uint32_t dataToCompress)
    {
        if (dataToCompress <= 0x7F)
        {
            ByteVectorPtr result(new ByteVector());
            result->push_back((unsigned char)dataToCompress);
            return result;
        }

        if (dataToCompress <= 0x3FFF)
        {
            ByteVectorPtr result(new ByteVector());
            result->push_back((unsigned char)((dataToCompress >> 8) | 0x80));
            result->push_back((unsigned char)(dataToCompress & 0xFF));
            return result;
        }

        if (dataToCompress <= 0x1FFFFFFF)
        {
            ByteVectorPtr result(new ByteVector());
            result->push_back((unsigned char)((dataToCompress >> 24) | 0xC0));
            result->push_back((unsigned char)((dataToCompress >> 16) & 0xFF));
            result->push_back((unsigned char)((dataToCompress >> 8) & 0xFF));
            result->push_back((unsigned char)(dataToCompress & 0xFF));
            return result;
        }

        LogError(L"Data too large to compress. " , std::hex, std::showbase, dataToCompress, std::resetiosflags(std::ios_base::basefield|std::ios_base::showbase));
        throw SignatureParserException(_X("Data too large to compress."));
    }

    static ByteVectorPtr CompressToken(uint32_t tokenToCompress)
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

        return CompressData(result);
    }
}}}
