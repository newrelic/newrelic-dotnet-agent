// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../Parser.h"
#include "../Scanner.h"
#include "../codegen/ByteCodeGenerator.h"
#include "RealisticTokenizer.h"
#include "TestTemplates.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

#define BYTEVECTOR(variableName, ...)\
    unsigned char myTempBytes##variableName[] = {##__VA_ARGS__};\
    ::sicily::codegen::ByteVector variableName(myTempBytes##variableName, myTempBytes##variableName + sizeof(myTempBytes##variableName) / sizeof(unsigned char));

namespace sicily
{
    namespace codegen
    {
        namespace Test
        {
            TEST_CLASS(BytecodeFromStringTest)
            {
            private:
                uint32_t GetMethodToken(std::wstring method, codegen::RealisticTokenizerPtr tokenizer)
                {
                    Scanner scanner(method);
                    Parser parser;
                    ast::TypePtr rootType = parser.Parse(scanner);
                    codegen::ByteCodeGenerator generator(tokenizer);
                    return generator.TypeToToken(rootType);
                }

            public:
                TEST_METHOD(TestSimpleMethod)
                {
                    // turn a CIL method into a token
                    std::wstring methodString(L"void [MyAssembly]MyNamespace.MyClass::MyMethod()");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto memberToken = GetMethodToken(methodString, tokenizer);
                    
                    // use the token to lookup the parts in the tokenizer
                    auto memberRef = tokenizer->GetMemberRef(memberToken);
                    auto typeRefToken = std::get<0>(memberRef);
                    auto methodName = std::get<1>(memberRef);
                    auto methodSignature = std::get<2>(memberRef);
                    auto typeRef = tokenizer->GetTypeRef(typeRefToken);
                    auto assemblyRefToken = std::get<0>(typeRef);
                    auto typeName = std::get<1>(typeRef);
                    auto typeNamespace = std::get<2>(typeRef);
                    auto assemblyRef = tokenizer->GetAssemblyRef(assemblyRefToken);
                    auto assemblyName = std::get<0>(assemblyRef);

                    // make sure the stuff in the tokenizer lines up with what we expect from the method string
                    Assert::AreEqual(std::wstring(L"MyAssembly"), assemblyName);
                    Assert::AreEqual(std::wstring(L"MyNamespace"), typeNamespace);
                    Assert::AreEqual(std::wstring(L"MyClass"), typeName);
                    Assert::AreEqual(std::wstring(L"MyMethod"), methodName);
                    BYTEVECTOR(expectedSignature, 0x00, 0x00, 0x01);
                    Assert::AreEqual(expectedSignature, methodSignature);
                }

                TEST_METHOD(TestField)
                {
                    // turn a CIL field into a token
                    std::wstring fieldString(L"int32 MyNamespace.MyClass::MyField");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto memberToken = GetMethodToken(fieldString, tokenizer);

                    // use the token to lookup the parts in the tokenizer
                    auto fieldDef = tokenizer->GetFieldDef(memberToken);
                    auto fieldName = std::get<1>(fieldDef);

                    // make sure the stuff in the tokenizer lines up with what we expect from the field string
                    Assert::AreEqual(std::wstring(L"MyField"), fieldName);
                }

                TEST_METHOD(TestComplexMethod1)
                {
                    std::wstring methodString(L"class [mscorlib]System.Tuple`2<!!0,!!1> [mscorlib]System.Tuple::Create<class [mscorlib]System.Action`1<object[]>,class [mscorlib]System.Action`1<object[]>>(!!0, !!1)");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto memberToken = GetMethodToken(methodString, tokenizer);

                    auto methodSpec = tokenizer->GetMethodSpec(memberToken);
                    auto memberRefToken = std::get<0>(methodSpec);
                    auto methodInstantiationSignature = std::get<1>(methodSpec);
                    auto memberRef = tokenizer->GetMemberRef(memberRefToken);
                    auto TypeRefToken = std::get<0>(memberRef);
                    auto methodName = std::get<1>(memberRef);
                    auto methodSignature = std::get<2>(memberRef);
                    auto typeRef = tokenizer->GetTypeRef(TypeRefToken);
                    auto assemblyRefToken = std::get<0>(typeRef);
                    auto typeName = std::get<1>(typeRef);
                    auto typeNamespace = std::get<2>(typeRef);
                    auto assemblyRef = tokenizer->GetAssemblyRef(assemblyRefToken);
                    auto assemblyName = std::get<0>(assemblyRef);

                    Assert::AreEqual(std::wstring(L"mscorlib"), assemblyName);
                    Assert::AreEqual(std::wstring(L"System"), typeNamespace);
                    Assert::AreEqual(std::wstring(L"Tuple"), typeName);
                    Assert::AreEqual(std::wstring(L"Create"), methodName);
                    // This test will break if the implementation of RealisticTokenizer changes since we are making assumptions about the class token embedded inside the signature (0x05 @ 6th byte)
                    BYTEVECTOR(expectedMethodSignature, 0x10, 0x02, 0x02, 0x15, 0x12, 0x05, 0x02, 0x1e, 0x00, 0x1e, 0x01, 0x1e, 0x00, 0x1e, 0x01);
                    Assert::AreEqual(expectedMethodSignature, methodSignature);
                    // This test will break if the implementation of RealisticTokenizer changes since we are making assumptions about the class token embedded inside the signature (0x0d @ 5th and 11th bytes)
                    BYTEVECTOR(expectedMethodInstantiationSignature, 0x0a, 0x02, 0x15, 0x12, 0x0d, 0x01, 0x1d, 0x1c, 0x15, 0x12, 0x0d, 0x01, 0x1d, 0x1c);
                    Assert::AreEqual(expectedMethodInstantiationSignature, methodInstantiationSignature);
                }

                TEST_METHOD(TestSimpleMethod1)
                {
                    std::wstring methodString(L"class [mscorlib]System.AppDomain [mscorlib]System.AppDomain::get_CurrentDomain()");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto memberToken = GetMethodToken(methodString, tokenizer);

                    // use the token to lookup the parts in the tokenizer
                    auto memberRef = tokenizer->GetMemberRef(memberToken);
                    auto typeRefToken = std::get<0>(memberRef);
                    auto methodName = std::get<1>(memberRef);
                    auto methodSignature = std::get<2>(memberRef);
                    auto typeRef = tokenizer->GetTypeRef(typeRefToken);
                    auto assemblyRefToken = std::get<0>(typeRef);
                    auto typeName = std::get<1>(typeRef);
                    auto typeNamespace = std::get<2>(typeRef);
                    auto assemblyRef = tokenizer->GetAssemblyRef(assemblyRefToken);
                    auto assemblyName = std::get<0>(assemblyRef);

                    // make sure the stuff in the tokenizer lines up with what we expect from the method string
                    Assert::AreEqual(std::wstring(L"mscorlib"), assemblyName);
                    Assert::AreEqual(std::wstring(L"System"), typeNamespace);
                    Assert::AreEqual(std::wstring(L"AppDomain"), typeName);
                    Assert::AreEqual(std::wstring(L"get_CurrentDomain"), methodName);
                    // This test will break if the implementation of RealisticTokenizer changes since we are making assumptions about the class token embedded inside the signature (0x05 @ 4th byte)
                    BYTEVECTOR(expectedSignature, 0x00, 0x00, 0x12, 0x05);
                    Assert::AreEqual(expectedSignature, methodSignature);
                }

                TEST_METHOD(TestMethodWithRefParam)
                {
                    std::wstring methodString(L"int32 [mscorlib]System.Threading.Interlocked::CompareExchange(int32&, int32, int32)");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto memberToken = GetMethodToken(methodString, tokenizer);

                    // use the token to lookup the parts in the tokenizer
                    auto memberRef = tokenizer->GetMemberRef(memberToken);
                    auto typeRefToken = std::get<0>(memberRef);
                    auto methodName = std::get<1>(memberRef);
                    auto methodSignature = std::get<2>(memberRef);
                    auto typeRef = tokenizer->GetTypeRef(typeRefToken);
                    auto assemblyRefToken = std::get<0>(typeRef);
                    auto typeName = std::get<1>(typeRef);
                    auto typeNamespace = std::get<2>(typeRef);
                    auto assemblyRef = tokenizer->GetAssemblyRef(assemblyRefToken);
                    auto assemblyName = std::get<0>(assemblyRef);

                    // make sure the stuff in the tokenizer lines up with what we expect from the method string
                    Assert::AreEqual(std::wstring(L"mscorlib"), assemblyName);
                    Assert::AreEqual(std::wstring(L"System.Threading"), typeNamespace);
                    Assert::AreEqual(std::wstring(L"Interlocked"), typeName);
                    Assert::AreEqual(std::wstring(L"CompareExchange"), methodName);
                    // Default, arg count, int(return type), int&, int, int
                    BYTEVECTOR(expectedSignature, 0x00, 0x03, 0x08, 0x08, 0x10, 0x08, 0x08);
                    Assert::AreEqual(expectedSignature, methodSignature);
                }

                TEST_METHOD(TestComplexType)
                {
                    std::wstring methodString(L"class [mscorlib]System.Tuple`2<class [mscorlib]System.Action`1<object[]>,class [mscorlib]System.Action`1<object[]>>");
                    codegen::RealisticTokenizerPtr tokenizer(new codegen::RealisticTokenizer());
                    auto typeSpecToken = GetMethodToken(methodString, tokenizer);

                    auto typeSpec = tokenizer->GetTypeSpec(typeSpecToken);
                    auto typeSignature = std::get<0>(typeSpec);
                    // This test will break if the implementation of RealisticTokenizer changes since we are making assumptions about the class tokens embedded inside the signature (0x05 & 0x09 @ 3rd, 7th and 13th bytes)
                    BYTEVECTOR(expectedSignature, 0x15, 0x12, 0x05, 0x02, 0x15, 0x12, 0x09, 0x01, 0x1d, 0x1c, 0x15, 0x12, 0x09, 0x01, 0x1d, 0x1c);
                    Assert::AreEqual(expectedSignature, typeSignature);

                    // 0x05 is dependent on the RealisticTokenizer implementation details.  If those change then this token will change.  It should match the token found in the signature above.
                    BYTEVECTOR(typeRefCompressedToken, 0x05);
                    #pragma warning (suppress: 4239)
                    auto typeRefToken = ByteCodeGenerator::CorSigUncompressToken(typeRefCompressedToken.begin(), typeRefCompressedToken.end());
                    auto typeRef = tokenizer->GetTypeRef(typeRefToken);
                    //auto assemblyRef = std::get<0>(typeRef);
                    auto typeName = std::get<1>(typeRef);
                    auto typeNamespace = std::get<2>(typeRef);
                    Assert::AreEqual(typeName, std::wstring(L"Tuple`2"));
                    Assert::AreEqual(typeNamespace, std::wstring(L"System"));
                }
            };
        }
    }
}
