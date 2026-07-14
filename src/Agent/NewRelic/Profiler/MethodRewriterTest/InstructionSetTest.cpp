// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#include "CppUnitTest.h"
#include "../MethodRewriter/InstructionSet.h"
#include "MockTokenizer.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(InstrumentationSetTest)
    {
    public:
        TEST_METHOD(append_short)
        {
            auto instructionSet = InstructionSet(nullptr, nullptr);

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
            auto instructionSet = InstructionSet(nullptr, nullptr);

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
            auto instructionSet = InstructionSet(nullptr, nullptr);

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

        TEST_METHOD(append_ldsfld)
        {
            auto tokenizer = std::make_shared<MockTokenizer>();
            tokenizer->_fieldDefinitionToken = 0xDEADBEEF;
            auto instructionSet = InstructionSet(tokenizer, nullptr);

            instructionSet.Append(_X("ldsfld object __NRInitializer__::_agentShimMethodInfo"));

            BYTEVECTOR(expectedBytes,
                CEE_LDSFLD,
                0xEF,
                0xBE,
                0xAD,
                0xDE
            );
            auto actualBytes = instructionSet.GetBytes();

            VerifyBytes(expectedBytes, actualBytes);
        }

        TEST_METHOD(append_stsfld)
        {
            auto tokenizer = std::make_shared<MockTokenizer>();
            tokenizer->_fieldDefinitionToken = 0x01020304;
            auto instructionSet = InstructionSet(tokenizer, nullptr);

            instructionSet.Append(_X("stsfld object __NRInitializer__::_agentShimFunc"));

            BYTEVECTOR(expectedBytes,
                CEE_STSFLD,
                0x04,
                0x03,
                0x02,
                0x01
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
