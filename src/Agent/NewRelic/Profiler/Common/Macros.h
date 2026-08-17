/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <vector>
#include <memory>
#include <stdint.h>

namespace NewRelic { namespace Profiler
{
    typedef std::vector<uint8_t> ByteVector;
    typedef std::shared_ptr<ByteVector> ByteVectorPtr;

    #define BYTEVECTOR(variableName, ...)\
        unsigned char myTempBytes##variableName[] = {__VA_ARGS__};\
        std::vector<uint8_t> variableName(myTempBytes##variableName, myTempBytes##variableName + sizeof(myTempBytes##variableName) / sizeof(unsigned char));

    #define lengthof(x) sizeof(x)/sizeof(x[0])

    // this macro gets us intellisense on opcodes found in opcode.def
    #define OPDEF(id, name, pop, push, operand, type, len, OpCode1, OpCode2, cf) id,
    enum ILCODE
    {
    #include <opcode.def>
    };
    #undef OPDEF
}}
