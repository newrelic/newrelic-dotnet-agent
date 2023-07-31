// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#define LOGGER_DEFINE_STDLOG
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"
#include "TestTemplates.h"
#include "ByteVectorMacro.h"
#include "../SignatureParser/SignatureParser.h"
#include "MockTokenResolver.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace SignatureParser { namespace Test
{

    TEST_CLASS(SignatureParserTest)
    {
    private:
        void AssertAreEqual(MethodSignaturePtr expectedSignature, MethodSignaturePtr actualSignature, ITokenResolverPtr tokenResolver)
        {
            Assert::AreEqual(expectedSignature->_callingConvention, actualSignature->_callingConvention);
            Assert::AreEqual(expectedSignature->_hasThis, actualSignature->_hasThis);
            Assert::AreEqual(expectedSignature->_explicitThis, actualSignature->_explicitThis);
            Assert::AreEqual(expectedSignature->_genericParamCount, actualSignature->_genericParamCount);
            Assert::AreEqual(expectedSignature->_returnType->ToString(tokenResolver), actualSignature->_returnType->ToString(tokenResolver));
            Assert::AreEqual(expectedSignature->_parameters->size(), actualSignature->_parameters->size());
            for (size_t i = 0; i < expectedSignature->_parameters->size(); ++i)
            {
                Assert::AreEqual(expectedSignature->_parameters->at(i)->ToString(tokenResolver), actualSignature->_parameters->at(i)->ToString(tokenResolver));
            }
        }

        void ParseAndVerifyMethodSignature(const ByteVector& signatureBytes, MethodSignaturePtr expectedSignature, bool validateBytes = true)
        {
            auto iterator = signatureBytes.begin();
            auto actualSignature = SignatureParser::ParseMethodSignature(iterator, signatureBytes.end());
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            Assert::IsFalse(iterator < signatureBytes.end(), L"The signature parser did not parse the entire signature.");
            Assert::IsFalse(iterator > signatureBytes.end(), L"The signature parser parsed past the end of the signature.");
            AssertAreEqual(expectedSignature, actualSignature, tokenResolver);

            // The bytes won't match for custom mods
            if (validateBytes)
            {
                auto actualBytes = actualSignature->ToBytes();
                Assert::AreEqual(signatureBytes, *actualBytes);
            }
        }

    public:
        
        TEST_METHOD(simple_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return type
                );
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false,  CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), std::make_shared<Parameters>(), 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }

        TEST_METHOD(simple_method_signature_to_bytes)
        {
            BYTEVECTOR(expectedBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return type
                );
            auto iterator = expectedBytes.begin();
            auto methodAst = SignatureParser::ParseMethodSignature(iterator, expectedBytes.end());
            auto actualBytes = methodAst->ToBytes();
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(generic_class_method_signature_to_bytes)
        {
            BYTEVECTOR(expectedBytes,
                0x30,
                0x01,
                0x01,
                0x01,
                0x1e,
                0x00
                );
            auto iterator = expectedBytes.begin();
            auto methodAst = SignatureParser::ParseMethodSignature(iterator, expectedBytes.end());
            auto actualBytes = methodAst->ToBytes();
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(instance_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x20, // HASTHIS, default calling convention
                0x00, // 0 parameters
                0x01, // void return type
                );
            MethodSignaturePtr expectedSignature(new MethodSignature(true, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), std::make_shared<Parameters>(), 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }

        TEST_METHOD(instance_explicit_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x20 | 0x40, // HASTHIS, EXPLICITTHIS, default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                0x12, 0x00, // type of "this" for explicit this (class with compressed classtoken of 0)
                );
            auto parameters = std::make_shared<Parameters>();
            parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
            MethodSignaturePtr expectedSignature(new MethodSignature(true, true, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), parameters, 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }

        TEST_METHOD(parameter_class_byref_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                0x10, 0x12, 0x00, // class (token 0) by ref
                );
            auto parameters = std::make_shared<Parameters>();
            parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), true));
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), parameters, 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }

        TEST_METHOD(parameter_class_byref_method_signature_to_bytes)
        {
            BYTEVECTOR(expectedBytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                0x10, 0x12, 0x00, // class (token 0) by ref
                );

            auto methodSignature = SignatureParser::ParseMethodSignature(expectedBytes.begin(), expectedBytes.end());
            auto actualBytes = methodSignature->ToBytes();
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(return_array_of_objects_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 1 parameter
                0x1d, 0x1c, // object[] return type
                );
            auto returnType = std::make_shared<TypedReturnType>(std::make_shared<SingleDimensionArrayType>(std::make_shared<ObjectType>()), false);
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, returnType, std::make_shared<Parameters>(), 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }

        TEST_METHOD(return_array_of_objects_method_signature_to_bytes)
        {
            BYTEVECTOR(expectedBytes,
                0x00, // default calling convention
                0x00, // 0 parameter
                0x1d, 0x1c, // object[] return type
                );

            auto methodSignature = SignatureParser::ParseMethodSignature(expectedBytes.begin(), expectedBytes.end());
            auto actualBytes = methodSignature->ToBytes();
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(multiple_custom_mods_on_parameter)
        {
            BYTEVECTOR(expectedBytes, 0x00, 0x01, 0x20, 0x09, 0x0f, 0x11, 0x7c, 0x20, 0x35, 0x20, 0x35, 0x0f, 0x11, 0x7c);
            auto methodSignature = SignatureParser::ParseMethodSignature(expectedBytes.begin(), expectedBytes.end());
            // this simply shouldn't throw an exception, can't assert on methodSignature->ToBytes() since we drop custom mods
        }

        TEST_METHOD(custom_mods_on_return_type)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x20, 0x11, // cmod_opt(token 0x11)
                0x03, // char return type
            );
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<TypedReturnType>(std::make_shared<CharType>(), false), std::make_shared<Parameters>(), 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature, false);
        }

        TEST_METHOD(custom_mods_on_ref_return_type)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x20, 0x11, // cmod_opt (token 0x11)
                0x10, // ByRef
                0x20, 0x02, // cmod_opt (token 0x02)
                0x1f, 0x04, // comd_req (token 0x04
                0x03, // char return type
            );
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<TypedReturnType>(std::make_shared<CharType>(), true), std::make_shared<Parameters>(), 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature, false);
        }

        TEST_METHOD(parameter_custom_mod_char_byref_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                0x20, 0x11, 0x10, 0x03, // simulates const ref char
            );
            auto parameters = std::make_shared<Parameters>();
            parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<CharType>(), true));
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), parameters, 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature, false);
        }

        TEST_METHOD(parameter_custom_mods_char_byref_method_signature)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                0x20, 0x11, 0x10, 0x20, 0x12, 0x1f, 0x14, 0x03, // simulates const ref char const
            );
            auto parameters = std::make_shared<Parameters>();
            parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<CharType>(), true));
            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), parameters, 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature, false);
        }

        MethodSignaturePtr TestArrayParameter(uint8_t type, uint32_t dimensions, const std::vector<uint32_t>& sizes, const std::vector<uint32_t>& lowerBounds)
        {
            ByteVector bytes;
            bytes.push_back(0x00); // default calling convention
            bytes.push_back(0x01); // 1 parameter
            bytes.push_back(0x01); // void return type
            bytes.push_back(0x14); // parameter 1 array
            bytes.push_back(type); // array element type
            // dimensions (rank)
            auto compressedDimensions = CompressData(dimensions);
            bytes.insert(bytes.end(), compressedDimensions->begin(), compressedDimensions->end());
            // size count
            auto compressedSizeCount = CompressData(uint32_t(sizes.size()));
            bytes.insert(bytes.end(), compressedSizeCount->begin(), compressedSizeCount->end());
            // sizes
            for (auto size : sizes)
            {
                auto compressedSize = CompressData(size);
                bytes.insert(bytes.end(), compressedSize->begin(), compressedSize->end());
            }
            // lower bound count
            auto compressedLowerBoundCount = CompressData(uint32_t(lowerBounds.size()));
            bytes.insert(bytes.end(), compressedLowerBoundCount->begin(), compressedLowerBoundCount->end());
            // lower bounds
            for (auto lowerBound : lowerBounds)
            {
                auto compressedLowerBound = CompressData(lowerBound);
                bytes.insert(bytes.end(), compressedLowerBound->begin(), compressedLowerBound->end());
            }

            auto methodSignature = SignatureParser::ParseMethodSignature(bytes.begin(), bytes.end());
            auto actualBytes = methodSignature->ToBytes();
            Assert::AreEqual(bytes, *actualBytes);
            return methodSignature;
        }

        TEST_METHOD(array_parameter_1)
        {
            std::vector<uint32_t> sizes;
            sizes.push_back(3);
            std::vector<uint32_t> lowerBounds;
            auto methodSignature = TestArrayParameter(ELEMENT_TYPE_I4, 1, sizes, lowerBounds);
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            auto expected = std::wstring(L"System.Int32[0...2]");
            auto actual = methodSignature->ToString(tokenResolver);
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(array_parameter_2)
        {
            std::vector<uint32_t> sizes;
            std::vector<uint32_t> lowerBounds;
            auto methodSignature = TestArrayParameter(ELEMENT_TYPE_I4, 7, sizes, lowerBounds);
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            auto expected = std::wstring(L"System.Int32[,,,,,,]");
            auto actual = methodSignature->ToString(tokenResolver);
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(array_parameter_3)
        {
            std::vector<uint32_t> sizes;
            sizes.push_back(4);
            sizes.push_back(3);
            std::vector<uint32_t> lowerBounds;
            lowerBounds.push_back(0);
            lowerBounds.push_back(0);
            auto methodSignature = TestArrayParameter(ELEMENT_TYPE_I4, 6, sizes, lowerBounds);
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            auto expected = std::wstring(L"System.Int32[0...3,0...2,,,,]");
            auto actual = methodSignature->ToString(tokenResolver);
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(array_parameter_4)
        {
            std::vector<uint32_t> sizes;
            sizes.push_back(2);
            sizes.push_back(3);
            std::vector<uint32_t> lowerBounds;
            lowerBounds.push_back(1);
            lowerBounds.push_back(6);
            auto methodSignature = TestArrayParameter(ELEMENT_TYPE_I4, 2, sizes, lowerBounds);
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            auto expected = std::wstring(L"System.Int32[1...2,6...8]");
            auto actual = methodSignature->ToString(tokenResolver);
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(array_parameter_5)
        {
            std::vector<uint32_t> sizes;
            sizes.push_back(5);
            sizes.push_back(3);
            std::vector<uint32_t> lowerBounds;
            lowerBounds.push_back(0);
            lowerBounds.push_back(3);
            auto methodSignature = TestArrayParameter(ELEMENT_TYPE_I4, 4, sizes, lowerBounds);
            auto tokenResolver = std::make_shared<MockTokenResolver>();
            auto expected = std::wstring(L"System.Int32[0...4,3...5,,]");
            auto actual = methodSignature->ToString(tokenResolver);
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(simple_type_parameter)
        {
            TestBasicParameter(ELEMENT_TYPE_CHAR, std::make_shared<CharType>());
            TestBasicParameter(ELEMENT_TYPE_BOOLEAN, std::make_shared<BooleanType>());
            TestBasicParameter(ELEMENT_TYPE_I1, std::make_shared<SByteType>());
            TestBasicParameter(ELEMENT_TYPE_U1, std::make_shared<ByteType>());
            TestBasicParameter(ELEMENT_TYPE_I2, std::make_shared<Int16Type>());
            TestBasicParameter(ELEMENT_TYPE_U2, std::make_shared<UInt16Type>());
            TestBasicParameter(ELEMENT_TYPE_I4, std::make_shared<Int32Type>());
            TestBasicParameter(ELEMENT_TYPE_U4, std::make_shared<UInt32Type>());
            TestBasicParameter(ELEMENT_TYPE_I8, std::make_shared<Int64Type>());
            TestBasicParameter(ELEMENT_TYPE_U8, std::make_shared<UInt64Type>());
            TestBasicParameter(ELEMENT_TYPE_R4, std::make_shared<SingleType>());
            TestBasicParameter(ELEMENT_TYPE_R8, std::make_shared<DoubleType>());
        }


        void TestBasicParameter(uint8_t typeValue, TypePtr type_ptr)
        {
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x01, // 1 parameter
                0x01, // void return type
                typeValue,
                );

            auto parameters = std::make_shared<Parameters>();
            parameters->push_back(std::make_shared<TypedParameter>(type_ptr, false));

            MethodSignaturePtr expectedSignature(new MethodSignature(false, false, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT, std::make_shared<VoidReturnType>(), parameters, 0));
            ParseAndVerifyMethodSignature(signatureBytes, expectedSignature);
        }
    };
}}}}
