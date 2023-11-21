// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#include "CppUnitTest.h"
#include "../MethodRewriter/InstructionSet.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(InstrumentationSetTest)
    {
    public:
        TEST_METHOD(append_short)
        {
            auto instructionSet = InstructionSet(nullptr, nullptr, false);

            instructionSet.Append(CEE_LDNULL, (uint16_t)0xDEAD);

            BYTEVECTOR(expectedBytes,
                CEE_LDNULL,
                0xAD,
                0xDE
            );
            auto actualBytes = instructionSet.GetBytes();

            VerifyBytes(expectedBytes, actualBytes);
        }

        TEST_METHOD(append_integer)
        {
            auto instructionSet = InstructionSet(nullptr, nullptr, false);

            instructionSet.Append(CEE_LDC_I4, (uint32_t)0xDEADBEEF);
            
            BYTEVECTOR(expectedBytes,
                CEE_LDC_I4,                
                0xEF,
                0xBE,
                0xAD,
                0xDE
            );
            auto actualBytes = instructionSet.GetBytes();

            VerifyBytes(expectedBytes, actualBytes);
        }

        TEST_METHOD(append_long)
        {
            auto instructionSet = InstructionSet(nullptr, nullptr, false);

            instructionSet.Append(CEE_LDC_I8, (uint64_t)0xBEEBDEADBEEFABBE);

            BYTEVECTOR(expectedBytes,
                CEE_LDC_I8,
                0xBE,
                0xAB,
                0xEF,
                0xBE,
                0xAD,
                0xDE,
                0xEB,
                0XBE
            );
            auto actualBytes = instructionSet.GetBytes();

            VerifyBytes(expectedBytes, actualBytes);
        }

    private:
        static void VerifyBytes(std::vector<uint8_t> expected, ByteVector actual)
        {
            Assert::AreEqual((size_t)expected.size(), actual.size());
            for (int i = 0; i < (int)expected.size(); i++)
            {
                Assert::AreEqual(expected[i], actual[i]);
            }
        }

    };
}}}}
