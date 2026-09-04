// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#include <vector>
#include "CppUnitTest.h"
#include "../MethodRewriter/InstructionSet.h"
#include "../SignatureParser/Types.h"
#include "MockTokenizer.h"
#include "RecordingTokenizer.h"

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

            instructionSet.Append(_X("ldsfld object __NRInitializer__::_agentShimFunc"));

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

        TEST_METHOD(constructor_selects_core_lib_assembly_name_for_framework_and_core)
        {
            auto frameworkInstructionSet = InstructionSet(nullptr, nullptr, false);
            Assert::AreEqual(std::wstring(_X("mscorlib")), frameworkInstructionSet.GetCoreLibAssemblyName());

            auto coreInstructionSet = InstructionSet(nullptr, nullptr, true);
            Assert::AreEqual(std::wstring(_X("System.Private.CoreLib")), coreInstructionSet.GetCoreLibAssemblyName());
        }

        TEST_METHOD(append_type_of_argument_tokenizes_each_primitive_type_against_core_lib)
        {
            struct PrimitiveTypeCase
            {
                SignatureParser::TypePtr type;
                xstring_t expectedTypeName;
            };

            std::vector<PrimitiveTypeCase> cases
            {
                { std::make_shared<SignatureParser::BooleanType>(), _X("System.Boolean") },
                { std::make_shared<SignatureParser::CharType>(), _X("System.Char") },
                { std::make_shared<SignatureParser::SByteType>(), _X("System.SByte") },
                { std::make_shared<SignatureParser::ByteType>(), _X("System.Byte") },
                { std::make_shared<SignatureParser::Int16Type>(), _X("System.Int16") },
                { std::make_shared<SignatureParser::UInt16Type>(), _X("System.UInt16") },
                { std::make_shared<SignatureParser::Int32Type>(), _X("System.Int32") },
                { std::make_shared<SignatureParser::UInt32Type>(), _X("System.UInt32") },
                { std::make_shared<SignatureParser::Int64Type>(), _X("System.Int64") },
                { std::make_shared<SignatureParser::UInt64Type>(), _X("System.UInt64") },
                { std::make_shared<SignatureParser::SingleType>(), _X("System.Single") },
                { std::make_shared<SignatureParser::DoubleType>(), _X("System.Double") },
                { std::make_shared<SignatureParser::IntPtrType>(), _X("System.IntPtr") },
                { std::make_shared<SignatureParser::UIntPtrType>(), _X("System.UIntPtr") },
                { std::make_shared<SignatureParser::ObjectType>(), _X("System.Object") },
            };

            // drives InstructionSet::GetTypeTokenForType once per primitive kind and asserts
            // that the matching System.<T> name was requested from the core library assembly
            for (const auto& testCase : cases)
            {
                auto tokenizer = std::make_shared<RecordingTokenizer>();
                auto instructionSet = InstructionSet(tokenizer, nullptr);
                SignatureParser::ParameterPtr parameter = std::make_shared<SignatureParser::TypedParameter>(testCase.type, false);

                instructionSet.AppendTypeOfArgument(parameter);

                Assert::IsTrue(tokenizer->TypeRefTokenized(instructionSet.GetCoreLibAssemblyName(), testCase.expectedTypeName));
            }
        }

        TEST_METHOD(append_type_of_argument_tokenizes_a_by_ref_parameter_as_typed_reference)
        {
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructionSet = InstructionSet(tokenizer, nullptr);
            SignatureParser::ParameterPtr parameter = std::make_shared<SignatureParser::TypedByRefParameter>();

            instructionSet.AppendTypeOfArgument(parameter);

            Assert::IsTrue(tokenizer->TypeRefTokenized(instructionSet.GetCoreLibAssemblyName(), _X("System.TypedReference")));
        }

        TEST_METHOD(append_load_local_and_box_tokenizes_a_by_ref_return_type_as_typed_reference)
        {
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructionSet = InstructionSet(tokenizer, nullptr);
            SignatureParser::ReturnTypePtr returnType = std::make_shared<SignatureParser::TypedByRefReturnType>();

            instructionSet.AppendLoadLocalAndBox(0, returnType);

            Assert::IsTrue(tokenizer->TypeRefTokenized(instructionSet.GetCoreLibAssemblyName(), _X("System.TypedReference")));
        }

        TEST_METHOD(append_type_of_argument_tokenizes_a_by_ref_return_type_as_typed_reference)
        {
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructionSet = InstructionSet(tokenizer, nullptr);
            SignatureParser::ReturnTypePtr returnType = std::make_shared<SignatureParser::TypedByRefReturnType>();

            instructionSet.AppendTypeOfArgument(returnType);

            Assert::IsTrue(tokenizer->TypeRefTokenized(instructionSet.GetCoreLibAssemblyName(), _X("System.TypedReference")));
        }

        TEST_METHOD(append_type_of_argument_throws_for_a_by_ref_return_type)
        {
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructionSet = InstructionSet(tokenizer, nullptr);
            SignatureParser::ReturnTypePtr returnType = std::make_shared<SignatureParser::TypedReturnType>(std::make_shared<SignatureParser::Int32Type>(), true);

            std::function<void(void)> func = [&instructionSet, &returnType]() {
                instructionSet.AppendTypeOfArgument(returnType);
                };

            Assert::ExpectException<InstructionSetException>(func, L"A by-ref return type should not be tokenizable.");
        }

        TEST_METHOD(append_type_of_argument_throws_for_an_unrecognized_return_type_kind)
        {
            // SignatureParser::ReturnType::Kind has exactly three legitimate values, all handled
            // by the switch in InstructionSet::GetTypeTokenForReturnType. The default branch is
            // otherwise unreachable, so this fake subclass forces an out-of-range Kind through
            // the base ReturnType constructor to exercise it.
            struct UnrecognizedReturnType : SignatureParser::ReturnType
            {
                UnrecognizedReturnType() : SignatureParser::ReturnType(static_cast<SignatureParser::ReturnType::Kind>(-1)) {}

                virtual xstring_t ToString(SignatureParser::ITokenResolverPtr) const override
                {
                    return _X("unrecognized");
                }

                virtual ByteVectorPtr ToBytes() const override
                {
                    return std::make_shared<ByteVector>();
                }
            };

            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructionSet = InstructionSet(tokenizer, nullptr);
            SignatureParser::ReturnTypePtr returnType = std::make_shared<UnrecognizedReturnType>();

            std::function<void(void)> func = [&instructionSet, &returnType]() {
                instructionSet.AppendTypeOfArgument(returnType);
                };

            Assert::ExpectException<MethodRewriterException>(func, L"An unrecognized return type kind should not be tokenizable.");
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
