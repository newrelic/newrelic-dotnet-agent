// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "NullTokenizer.h"
#include "TestTemplates.h"
#include "../codegen/ByteCodeGenerator.h"
#include "../ast/Types.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

#define BYTEVECTOR(variableName, ...)\
    unsigned char myTempBytes[] = {##__VA_ARGS__};\
    ::sicily::codegen::ByteVector variableName(myTempBytes, myTempBytes + sizeof(myTempBytes) / sizeof(unsigned char));

namespace sicily
{
    namespace codegen
    {
        namespace Test
        {
            TEST_CLASS(ByteCodeGeneratorTest)
            {
            private:
                ByteCodeGenerator CreateBadFoodByteCodeGenerator()
                {
                    NullTokenizerPtr tokenizer(new NullTokenizer());
                    return ByteCodeGenerator(tokenizer);
                }

                void TestPrimitive(ast::PrimitiveType::PrimitiveKind kind, unsigned char expectedByte)
                {
                    ast::TypePtr type(new ast::PrimitiveType(kind, false));
                    ByteVector actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    BYTEVECTOR(expectedBytes, {expectedByte});
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

            public:
                TEST_METHOD(TestPrimitiveVoidTypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kVOID, 0x01);
                }

                TEST_METHOD(TestPrimitiveBoolTypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kBOOL, 0x02);
                }

                TEST_METHOD(TestPrimitiveCharTypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kCHAR, 0x03);
                }

                TEST_METHOD(TestPrimitiveI1TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kI1, 0x04);
                }

                TEST_METHOD(TestPrimitiveU1TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kU1, 0x05);
                }

                TEST_METHOD(TestPrimitiveI2TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kI2, 0x06);
                }

                TEST_METHOD(TestPrimitiveU2TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kU2, 0x07);
                }

                TEST_METHOD(TestPrimitiveI4TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kI4, 0x08);
                }

                TEST_METHOD(TestPrimitiveU4TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kU4, 0x09);
                }

                TEST_METHOD(TestPrimitiveI8TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kI8, 0x0a);
                }

                TEST_METHOD(TestPrimitiveU8TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kU8, 0x0b);
                }

                TEST_METHOD(TestPrimitiveR4TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kR4, 0x0c);
                }

                TEST_METHOD(TestPrimitiveR8TypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kR8, 0x0d);
                }

                TEST_METHOD(TestPrimitiveStringTypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kSTRING, 0x0e);
                }

                TEST_METHOD(TestPrimitiveObjectTypeToBytes)
                {
                    TestPrimitive(ast::PrimitiveType::PrimitiveKind::kOBJECT, 0x1c);
                }

                TEST_METHOD(TestArrayTypeToBytes)
                {
                    ast::TypePtr innerType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    ast::TypePtr type(new ast::ArrayType(innerType));
                    ByteVector actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // bool[] == 0x1d 0x02
                    BYTEVECTOR(expectedBytes, 0x1d, 0x02);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestClassTypeToBytes)
                {
                    ast::TypePtr type(new ast::ClassType(L"MyClass", L"MyAssembly"));
                    ByteVector actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // class <token> == 0x12 <compressed 0> == 0x12 0x00
                    BYTEVECTOR(expectedBytes, 0x12, 0x00);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestGenericTypeToBytes)
                {
                    ast::PrimitiveTypePtr objectType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    ast::ArrayTypePtr objectArrayType(new ast::ArrayType(objectType));
                    ast::TypeListPtr genericParamTypes(new ast::TypeList());
                    genericParamTypes->Add(objectArrayType);
                    ast::TypePtr type(new ast::GenericType(L"MyClass", L"MyAssembly", genericParamTypes));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // genericinst <type> <type-arg-count> <type*> == 0x15 class <compressed token> 0x01 <object[]> == 0x15 0x12 0xc2af37bc 0x01 0x1d 0x1c
                    BYTEVECTOR(expectedBytes, 0x15, 0x12, 0x00, 0x01, 0x1d, 0x1c);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestMethodTypeToBytes)
                {
                    ast::ClassTypePtr targetType(new ast::ClassType(L"MyClass", L"MyAssembly"));
                    ast::PrimitiveTypePtr returnType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kVOID, false));
                    ast::TypeListPtr argTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr arg1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kBOOL, false));
                    argTypes->Add(arg1Type);
                    ast::TypeListPtr genericTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr generic1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kOBJECT, false));
                    genericTypes->Add(generic1Type);
                    ast::TypePtr type(new ast::MethodType(targetType, L"MyMethod", returnType, true, argTypes, genericTypes));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // HASTHIS GENERIC GenParamCount ParamCount RetType Param*
                    // (0x20 | 0x10) 0x01 0x01 0x01 0x02
                    BYTEVECTOR(expectedBytes, 0x30, 0x01, 0x01, 0x01, 0x02);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestMethodTypeToBytesWithRefParam)
                {
                    ast::ClassTypePtr targetType(new ast::ClassType(L"MyClass", L"MyAssembly"));
                    ast::PrimitiveTypePtr returnType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kVOID, false));
                    ast::TypeListPtr argTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr arg1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kBOOL, true));
                    argTypes->Add(arg1Type);
                    ast::TypeListPtr genericTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr generic1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kOBJECT, false));
                    genericTypes->Add(generic1Type);
                    ast::TypePtr type(new ast::MethodType(targetType, L"MyMethod", returnType, true, argTypes, genericTypes));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // HASTHIS GENERIC GenParamCount ParamCount RetType Param*
                    // (0x20 | 0x10) 0x01 0x01 0x01 0x02
                    BYTEVECTOR(expectedBytes, 0x30, 0x01, 0x01, 0x01, 0x02, 0x10);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestFieldTypeToBytes)
                {
                    ast::ClassTypePtr targetType(new ast::ClassType(L"MyClass", L"MyAssembly"));
                    ast::PrimitiveTypePtr returnType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kI4, false));
                    ast::TypePtr type(new ast::FieldType(targetType, L"MyField", returnType));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().TypeToBytes(type);
                    // RetType 
                    // 0x08
                    BYTEVECTOR(expectedBytes, 0x08);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestGenericMethodInstantiationToSignature)
                {
                    ast::ClassTypePtr targetType(new ast::ClassType(L"MyClass", L"MyAssembly"));
                    ast::PrimitiveTypePtr returnType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kVOID, false));
                    ast::TypeListPtr argTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr arg1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kBOOL, false));
                    argTypes->Add(arg1Type);
                    ast::TypeListPtr genericTypes(new ast::TypeList());
                    ast::PrimitiveTypePtr generic1Type(new ast::PrimitiveType(ast::PrimitiveType::PrimitiveKind::kOBJECT, false));
                    genericTypes->Add(generic1Type);
                    ast::MethodTypePtr type(new ast::MethodType(targetType, L"MyMethod", returnType, true, argTypes, genericTypes));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().GenericMethodInstantiationToSignature(type);
                    // GENERICINST GenArgCount Type+
                    // 0x0a 0x01 0x1c
                    BYTEVECTOR(expectedBytes, 0x0a, 0x01, 0x1c);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(TestGenericClassInstantiationToSignature)
                {
                    ast::PrimitiveTypePtr objectType(new ast::PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    ast::ArrayTypePtr objectArrayType(new ast::ArrayType(objectType));
                    ast::TypeListPtr genericParamTypes(new ast::TypeList());
                    genericParamTypes->Add(objectArrayType);
                    ast::GenericTypePtr type(new ast::GenericType(L"MyClass", L"MyAssembly", genericParamTypes));

                    auto actualBytes = CreateBadFoodByteCodeGenerator().GenericClassInstantiationToSignature(type);
                    // GENERICINST CLASS TypeDefOrRefOrSpecEncoded GenArgCount Type+
                    // 0x15 0x12 <compressed token> 0x01 SZARRAY Type
                    // 0x15 0x12 0x00 0x01 0x1d 0x1c
                    BYTEVECTOR(expectedBytes, 0x15, 0x12, 0x00, 0x01, 0x1d, 0x1c);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                void TestCompressAndUncompress(uint32_t number)
                {
                    auto bytes = ByteCodeGenerator::CorSigCompressData(number);
                    #pragma warning (suppress: 4239)
                    auto result = ByteCodeGenerator::CorSigUncompressData(bytes.begin(), bytes.end());

                    Assert::AreEqual(number, result);
                }

                TEST_METHOD(test_compress_and_uncompress_0)
                {
                    TestCompressAndUncompress(0);
                }

                TEST_METHOD(test_compress_and_uncompress_1)
                {
                    TestCompressAndUncompress(1);
                }

                TEST_METHOD(test_compress_and_uncompress_first_edge_case)
                {
                    TestCompressAndUncompress(0x7f);
                }

                TEST_METHOD(test_compress_and_uncompress_second_edge_case)
                {
                    TestCompressAndUncompress(0x80);
                }

                TEST_METHOD(test_compress_and_uncompress_third_edge_case)
                {
                    TestCompressAndUncompress(0x3fff);
                }

                TEST_METHOD(test_compress_and_uncompress_fourth_edge_case)
                {
                    TestCompressAndUncompress(0x4000);
                }

                TEST_METHOD(test_compress_and_uncompress_compression_max)
                {
                    TestCompressAndUncompress(0x1FFFFFFF);
                }

                TEST_METHOD(test_error_on_compress_uint32_max)
                {
                    try
                    {
                        ByteCodeGenerator::CorSigCompressData(UINT32_MAX);
                        Assert::Fail(L"Expected an exception but one was not thrown.");
                    }
                    catch (const DataTooLargeToCompressException&) {}
                    catch (...)
                    {
                        Assert::Fail(L"Expected DataTooLargeToCompressException but caught something else.");
                    }
                }

                TEST_METHOD(test_compress_0x03)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x03);
                    BYTEVECTOR(expectedBytes, 0x03);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x7f)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x7f);
                    BYTEVECTOR(expectedBytes, 0x7f);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x80)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x80);
                    BYTEVECTOR(expectedBytes, 0x80, 0x80);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x2e57)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x2e57);
                    BYTEVECTOR(expectedBytes, 0xae, 0x57);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x3fff)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x3fff);
                    BYTEVECTOR(expectedBytes, 0xbf, 0xff);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x4000)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x4000);
                    BYTEVECTOR(expectedBytes, 0xc0, 0x00, 0x40, 0x00);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }

                TEST_METHOD(test_compress_0x1fffffff)
                {
                    auto actualBytes = ByteCodeGenerator::CorSigCompressData(0x1fffffff);
                    BYTEVECTOR(expectedBytes, 0xdf, 0xff, 0xff, 0xff);
                    Assert::AreEqual(expectedBytes, actualBytes);
                }
            };
        }
    }
}
